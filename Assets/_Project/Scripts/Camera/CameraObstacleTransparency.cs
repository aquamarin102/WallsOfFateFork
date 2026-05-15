using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game
{
    [DefaultExecutionOrder(10001)]
    [RequireComponent(typeof(Camera))]
    public class CameraObstacleTransparency : MonoBehaviour
    {
        private enum OcclusionDetectionMode
        {
            PhysicsColliders,
            RendererBounds,
            PhysicsCollidersAndRendererBounds
        }

        private const string OcclusionShaderResourceName = "OcclusionRevealURP";
        private const int MaxObstacleHits = 96;
        private const int BodySampleCount = 3;
        private const float RestoreThreshold = 0.001f;

        private static readonly float[] BodySampleHeightFactors = { 0.45f, 0.75f, 1f };
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int OcclusionFadeId = Shader.PropertyToID("_OcclusionFade");
        private static readonly int OcclusionCenterId = Shader.PropertyToID("_OcclusionCenter");
        private static readonly int OcclusionRadiusId = Shader.PropertyToID("_OcclusionRadius");
        private static readonly int OcclusionFeatherId = Shader.PropertyToID("_OcclusionFeather");
        private static readonly int OcclusionAlphaId = Shader.PropertyToID("_OcclusionAlpha");
        private static readonly int OcclusionRimColorId = Shader.PropertyToID("_OcclusionRimColor");
        private static readonly int OcclusionRimStrengthId = Shader.PropertyToID("_OcclusionRimStrength");
        private static readonly int OcclusionNoiseScaleId = Shader.PropertyToID("_OcclusionNoiseScale");
        private static readonly int OcclusionNoiseSpeedId = Shader.PropertyToID("_OcclusionNoiseSpeed");
        private static readonly int OcclusionDitherStrengthId = Shader.PropertyToID("_OcclusionDitherStrength");

        [Header("Detection")]
        [SerializeField] private OcclusionDetectionMode detectionMode = OcclusionDetectionMode.PhysicsColliders;
        [SerializeField] private LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.01f)] private float probeRadius = 0.35f;
        [SerializeField, Min(0f)] private float distancePadding = 0.25f;
        [SerializeField, Min(0.05f)] private float rendererCacheRefreshInterval = 1f;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Header("Target")]
        [SerializeField, Min(0f)] private float targetHeight = 1.1f;

        [Header("Exclusions")]
        [SerializeField] private LayerMask ignoredRendererLayers = 1 << 14;
        [SerializeField] private bool ignoreNpcControllers = true;

        [Header("Visibility filters")]
        [SerializeField, Min(0f)] private float ignoreWhenCloserToTargetThan = 0.9f;
        [SerializeField, Min(0f)] private float ignoreWhenCloserToCameraThan = 0.15f;
        [SerializeField, Range(0f, 2f)] private float minOccluderTopHeightFromTarget = 1.71f;
        [SerializeField, Range(1, BodySampleCount)] private int requiredCoveredBodySamples = 2;
        [SerializeField, Range(0f, 0.1f)] private float screenBoundsPadding = 0.015f;
        [SerializeField] private bool requireScreenOverlap = true;

        [Header("Reveal")]
        [SerializeField, Min(0.01f)] private float revealRadius = 1.85f;
        [SerializeField, Min(0.01f)] private float revealFeather = 0.9f;
        [SerializeField, Range(0f, 1f)] private float transparentAlpha = 0.18f;
        [SerializeField, Min(0.01f)] private float fadeInSpeed = 7f;
        [SerializeField, Min(0.01f)] private float fadeOutSpeed = 4.5f;
        [SerializeField, Min(0.01f)] private float centerFollowSpeed = 12f;

        [Header("Style")]
        [SerializeField] private Color rimColor = new Color(1f, 0.42f, 0.12f, 1f);
        [SerializeField, Range(0f, 4f)] private float rimStrength = 1.15f;
        [SerializeField, Range(0f, 1f)] private float ditherStrength = 0.28f;
        [SerializeField, Min(0.01f)] private float noiseScale = 4f;
        [SerializeField, Min(0f)] private float noiseSpeed = 1f;

        [Header("Materials")]
        [SerializeField] private Shader occlusionShader;
        [SerializeField] private bool useRuntimeProxyMaterials = true;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxObstacleHits];
        private readonly HashSet<Renderer> _occludingRenderers = new HashSet<Renderer>();
        private readonly Dictionary<Renderer, RendererState> _rendererStates = new Dictionary<Renderer, RendererState>();
        private readonly List<Renderer> _rendererStateBuffer = new List<Renderer>();
        private readonly List<Renderer> _collectedRenderers = new List<Renderer>();
        private readonly List<Renderer> _rendererCandidates = new List<Renderer>();
        private readonly Vector3[] _bodySamplePositions = new Vector3[BodySampleCount];

        private Shader _resolvedOcclusionShader;
        private Camera _camera;
        private Transform _player;
        private float _nextRendererCacheRefreshTime;
        private bool _warnedMissingShader;

        [Inject]
        private void Construct([InjectOptional] PlayerMoveController player)
        {
            if (player != null)
            {
                SetTarget(player.transform);
            }
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            ResolveOcclusionShader();
        }

        private void LateUpdate()
        {
            if (_player == null)
            {
                _occludingRenderers.Clear();
                UpdateTrackedRenderers(Time.deltaTime);
                return;
            }

            UpdateOccludingRenderers(Time.deltaTime);
        }

        private void OnDisable()
        {
            RestoreAllRenderers();
        }

        private void OnDestroy()
        {
            RestoreAllRenderers();
        }

        private void UpdateOccludingRenderers(float deltaTime)
        {
            _occludingRenderers.Clear();

            Vector3 targetPosition = GetTargetPosition();
            Vector3 cameraPosition = transform.position;
            Vector3 toTarget = targetPosition - cameraPosition;
            float distance = toTarget.magnitude;
            if (distance <= Mathf.Epsilon)
            {
                UpdateTrackedRenderers(deltaTime);
                return;
            }

            Ray targetRay = new Ray(cameraPosition, toTarget / distance);
            if (UsesPhysicsDetection())
            {
                RegisterPhysicsOccluders(targetRay, targetPosition, distance);
            }

            if (UsesRendererBoundsDetection())
            {
                RegisterRendererBoundsOccluders(targetRay, targetPosition, distance);
            }

            UpdateTrackedRenderers(deltaTime);
        }

        private Vector3 GetTargetPosition()
        {
            return _player.position + Vector3.up * targetHeight;
        }

        private bool UsesPhysicsDetection()
        {
            return detectionMode == OcclusionDetectionMode.PhysicsColliders
                || detectionMode == OcclusionDetectionMode.PhysicsCollidersAndRendererBounds;
        }

        private bool UsesRendererBoundsDetection()
        {
            return detectionMode == OcclusionDetectionMode.RendererBounds
                || detectionMode == OcclusionDetectionMode.PhysicsCollidersAndRendererBounds;
        }

        private void RegisterPhysicsOccluders(Ray targetRay, Vector3 targetPosition, float distance)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                targetRay.origin,
                probeRadius,
                targetRay.direction,
                _hits,
                distance + distancePadding,
                obstacleMask,
                triggerInteraction);

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hits[index];
                if (hit.collider == null || ShouldIgnoreCollider(hit.collider))
                {
                    continue;
                }

                Vector3 revealCenter = GetRevealCenter(hit, targetPosition);
                float obstacleDistance = Mathf.Max(0f, hit.distance);
                CollectRenderers(hit.collider, revealCenter, targetRay.origin, targetPosition, distance, obstacleDistance);
            }
        }

        private void RegisterRendererBoundsOccluders(Ray targetRay, Vector3 targetPosition, float distance)
        {
            RefreshRendererCandidates();

            for (int index = 0; index < _rendererCandidates.Count; index++)
            {
                Renderer targetRenderer = _rendererCandidates[index];
                if (!CanFadeRenderer(targetRenderer))
                {
                    continue;
                }

                Bounds bounds = targetRenderer.bounds;
                Bounds expandedBounds = bounds;
                expandedBounds.Expand(probeRadius * 2f);

                float obstacleDistance;
                if (!expandedBounds.IntersectRay(targetRay, out obstacleDistance))
                {
                    continue;
                }

                if (obstacleDistance < 0f || obstacleDistance > distance + distancePadding)
                {
                    continue;
                }

                Vector3 revealCenter = targetRay.GetPoint(Mathf.Clamp(obstacleDistance, 0f, distance));
                RegisterRenderer(targetRenderer, revealCenter, targetRay.origin, targetPosition, distance, obstacleDistance);
            }
        }

        private void RefreshRendererCandidates()
        {
            if (_rendererCandidates.Count > 0 && Time.unscaledTime < _nextRendererCacheRefreshTime)
            {
                return;
            }

            _nextRendererCacheRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, rendererCacheRefreshInterval);
            _rendererCandidates.Clear();

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer candidate = renderers[index];
                if (CanFadeRenderer(candidate))
                {
                    _rendererCandidates.Add(candidate);
                }
            }
        }

        private Vector3 GetRevealCenter(RaycastHit hit, Vector3 targetPosition)
        {
            Vector3 revealCenter = hit.point;
            if ((hit.distance <= 0f || !IsValidVector(revealCenter)) && hit.collider != null)
            {
                revealCenter = hit.collider.ClosestPoint(targetPosition);
            }

            if (!IsValidVector(revealCenter))
            {
                revealCenter = targetPosition;
            }

            return revealCenter;
        }

        private void CollectRenderers(
            Collider obstacleCollider,
            Vector3 revealCenter,
            Vector3 cameraPosition,
            Vector3 targetPosition,
            float distance,
            float obstacleDistance)
        {
            _collectedRenderers.Clear();
            obstacleCollider.GetComponentsInChildren(false, _collectedRenderers);

            if (_collectedRenderers.Count == 0)
            {
                Renderer parentRenderer = obstacleCollider.GetComponentInParent<Renderer>();
                if (parentRenderer != null)
                {
                    _collectedRenderers.Add(parentRenderer);
                }
            }

            for (int index = 0; index < _collectedRenderers.Count; index++)
            {
                RegisterRenderer(
                    _collectedRenderers[index],
                    revealCenter,
                    cameraPosition,
                    targetPosition,
                    distance,
                    obstacleDistance);
            }

            _collectedRenderers.Clear();
        }

        private void RegisterRenderer(
            Renderer targetRenderer,
            Vector3 revealCenter,
            Vector3 cameraPosition,
            Vector3 targetPosition,
            float distance,
            float obstacleDistance)
        {
            if (!CanFadeRenderer(targetRenderer))
            {
                return;
            }

            if (!ShouldRevealRenderer(targetRenderer, cameraPosition, targetPosition, distance, obstacleDistance))
            {
                return;
            }

            RendererState rendererState = GetOrCreateState(targetRenderer);
            if (rendererState == null)
            {
                return;
            }

            rendererState.SetTargetCenter(revealCenter, targetPosition);
            _occludingRenderers.Add(targetRenderer);
        }

        private bool CanFadeRenderer(Renderer targetRenderer)
        {
            if (targetRenderer == null || !targetRenderer.enabled)
            {
                return false;
            }

            if (targetRenderer is ParticleSystemRenderer || targetRenderer is TrailRenderer || targetRenderer is LineRenderer)
            {
                return false;
            }

            if (ShouldIgnoreTransform(targetRenderer.transform))
            {
                return false;
            }

            return targetRenderer.sharedMaterials != null && targetRenderer.sharedMaterials.Length > 0;
        }

        private bool ShouldRevealRenderer(
            Renderer targetRenderer,
            Vector3 cameraPosition,
            Vector3 targetPosition,
            float distance,
            float obstacleDistance)
        {
            if (obstacleDistance < ignoreWhenCloserToCameraThan)
            {
                return false;
            }

            if (distance - obstacleDistance < ignoreWhenCloserToTargetThan)
            {
                return false;
            }

            Bounds bounds = targetRenderer.bounds;
            float minTopHeight = _player.position.y + targetHeight * minOccluderTopHeightFromTarget;
            if (bounds.max.y < minTopHeight)
            {
                return false;
            }

            if (!requireScreenOverlap)
            {
                return true;
            }

            return CoversRequiredBodySamples(bounds, cameraPosition, targetPosition);
        }

        private bool CoversRequiredBodySamples(Bounds bounds, Vector3 cameraPosition, Vector3 targetPosition)
        {
            Camera revealCamera = GetRevealCamera();
            if (revealCamera == null)
            {
                return true;
            }

            Rect boundsRect;
            if (!TryGetViewportRect(bounds, revealCamera, out boundsRect))
            {
                return false;
            }

            Vector3 toTarget = targetPosition - cameraPosition;
            int coveredSamples = 0;
            int requiredSamples = Mathf.Clamp(requiredCoveredBodySamples, 1, BodySampleCount);
            FillBodySamplePositions();

            for (int index = 0; index < BodySampleCount; index++)
            {
                Vector3 samplePosition = _bodySamplePositions[index];
                Vector3 viewportPosition = revealCamera.WorldToViewportPoint(samplePosition);
                if (viewportPosition.z <= revealCamera.nearClipPlane)
                {
                    continue;
                }

                Vector3 toSample = samplePosition - cameraPosition;
                if (Vector3.Dot(toSample, toTarget) <= 0f)
                {
                    continue;
                }

                if (boundsRect.Contains(new Vector2(viewportPosition.x, viewportPosition.y)))
                {
                    coveredSamples++;
                    if (coveredSamples >= requiredSamples)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void FillBodySamplePositions()
        {
            Vector3 basePosition = _player.position;
            for (int index = 0; index < BodySampleCount; index++)
            {
                _bodySamplePositions[index] = basePosition + Vector3.up * targetHeight * BodySampleHeightFactors[index];
            }
        }

        private bool TryGetViewportRect(Bounds bounds, Camera revealCamera, out Rect viewportRect)
        {
            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, 0f);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, 0f);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            bool hasVisibleCorner = false;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 viewportPoint = revealCamera.WorldToViewportPoint(corner);
                        if (viewportPoint.z <= revealCamera.nearClipPlane)
                        {
                            continue;
                        }

                        min.x = Mathf.Min(min.x, viewportPoint.x);
                        min.y = Mathf.Min(min.y, viewportPoint.y);
                        max.x = Mathf.Max(max.x, viewportPoint.x);
                        max.y = Mathf.Max(max.y, viewportPoint.y);
                        hasVisibleCorner = true;
                    }
                }
            }

            if (!hasVisibleCorner)
            {
                viewportRect = default;
                return false;
            }

            min.x -= screenBoundsPadding;
            min.y -= screenBoundsPadding;
            max.x += screenBoundsPadding;
            max.y += screenBoundsPadding;

            viewportRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return viewportRect.xMax >= 0f && viewportRect.xMin <= 1f
                && viewportRect.yMax >= 0f && viewportRect.yMin <= 1f;
        }

        private Camera GetRevealCamera()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            return _camera;
        }

        private bool ShouldIgnoreCollider(Collider obstacleCollider)
        {
            if (obstacleCollider == null)
            {
                return true;
            }

            Transform obstacleTransform = obstacleCollider.transform;
            if (obstacleTransform == transform || obstacleTransform.IsChildOf(transform))
            {
                return true;
            }

            if (ShouldIgnoreTransform(obstacleTransform))
            {
                return true;
            }

            GameObject playerObject;
            return PlayerObjectUtility.TryGetPlayerObject(obstacleCollider, out playerObject);
        }

        private bool ShouldIgnoreTransform(Transform targetTransform)
        {
            if (targetTransform == null)
            {
                return true;
            }

            if (_player != null && targetTransform.IsChildOf(_player))
            {
                return true;
            }

            if ((ignoredRendererLayers.value & (1 << targetTransform.gameObject.layer)) != 0)
            {
                return true;
            }

            return ignoreNpcControllers && targetTransform.GetComponentInParent<global::NPCController>() != null;
        }

        private RendererState GetOrCreateState(Renderer targetRenderer)
        {
            RendererState rendererState;
            if (_rendererStates.TryGetValue(targetRenderer, out rendererState))
            {
                return rendererState;
            }

            Shader shader = ResolveOcclusionShader();
            if (useRuntimeProxyMaterials && shader == null)
            {
                WarnMissingShader();
            }

            rendererState = new RendererState(targetRenderer, useRuntimeProxyMaterials ? shader : null);
            _rendererStates.Add(targetRenderer, rendererState);
            return rendererState;
        }

        private void UpdateTrackedRenderers(float deltaTime)
        {
            _rendererStateBuffer.Clear();
            foreach (Renderer rendererKey in _rendererStates.Keys)
            {
                _rendererStateBuffer.Add(rendererKey);
            }

            for (int index = 0; index < _rendererStateBuffer.Count; index++)
            {
                Renderer targetRenderer = _rendererStateBuffer[index];
                RendererState rendererState;
                if (!_rendererStates.TryGetValue(targetRenderer, out rendererState))
                {
                    continue;
                }

                if (targetRenderer == null)
                {
                    rendererState.ReleaseRuntimeMaterials();
                    _rendererStates.Remove(targetRenderer);
                    continue;
                }

                bool isOccluding = _occludingRenderers.Contains(targetRenderer);
                float targetFade = isOccluding ? 1f : 0f;
                float speed = isOccluding ? fadeInSpeed : fadeOutSpeed;
                rendererState.Fade = Mathf.MoveTowards(rendererState.Fade, targetFade, speed * deltaTime);
                rendererState.UpdateCenter(deltaTime, centerFollowSpeed);

                ApplyRendererState(rendererState);

                if (!isOccluding && rendererState.Fade <= RestoreThreshold)
                {
                    rendererState.RestoreOriginalMaterials();
                    _rendererStates.Remove(targetRenderer);
                }
            }

            _rendererStateBuffer.Clear();
        }

        private void ApplyRendererState(RendererState rendererState)
        {
            Renderer targetRenderer = rendererState.TargetRenderer;
            int materialCount = rendererState.MaterialCount;
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                MaterialPropertyBlock block = rendererState.PropertyBlock;
                targetRenderer.GetPropertyBlock(block, materialIndex);
                block.SetFloat(OcclusionFadeId, rendererState.Fade);
                block.SetVector(OcclusionCenterId, rendererState.CurrentCenter);
                block.SetFloat(OcclusionRadiusId, revealRadius);
                block.SetFloat(OcclusionFeatherId, revealFeather);
                block.SetFloat(OcclusionAlphaId, transparentAlpha);
                block.SetColor(OcclusionRimColorId, rimColor);
                block.SetFloat(OcclusionRimStrengthId, rimStrength);
                block.SetFloat(OcclusionNoiseScaleId, noiseScale);
                block.SetFloat(OcclusionNoiseSpeedId, noiseSpeed);
                block.SetFloat(OcclusionDitherStrengthId, ditherStrength);
                targetRenderer.SetPropertyBlock(block, materialIndex);
            }
        }

        private Shader ResolveOcclusionShader()
        {
            if (_resolvedOcclusionShader != null)
            {
                return _resolvedOcclusionShader;
            }

            _resolvedOcclusionShader = occlusionShader;
            if (_resolvedOcclusionShader == null)
            {
                _resolvedOcclusionShader = Resources.Load<Shader>(OcclusionShaderResourceName);
            }

            if (_resolvedOcclusionShader == null)
            {
                _resolvedOcclusionShader = Shader.Find("Game/OcclusionRevealURP");
            }

            return _resolvedOcclusionShader;
        }

        private void WarnMissingShader()
        {
            if (_warnedMissingShader)
            {
                return;
            }

            _warnedMissingShader = true;
            Debug.LogWarning("Occlusion reveal shader was not found. Obstacle fade will not be visible until OcclusionRevealURP is available.", this);
        }

        private void RestoreAllRenderers()
        {
            foreach (RendererState rendererState in _rendererStates.Values)
            {
                rendererState.RestoreOriginalMaterials();
            }

            _rendererStates.Clear();
            _occludingRenderers.Clear();
            _rendererStateBuffer.Clear();
            _collectedRenderers.Clear();
        }

        public void SetTarget(Transform target)
        {
            _player = target;
        }

        private static bool IsValidVector(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private static Material CreateRuntimeMaterial(Material sourceMaterial, Shader occlusionShader)
        {
            if (sourceMaterial == null || occlusionShader == null)
            {
                return null;
            }

            Material runtimeMaterial = new Material(occlusionShader);
            runtimeMaterial.name = sourceMaterial.name + " (Occlusion Reveal Runtime)";
            runtimeMaterial.enableInstancing = sourceMaterial.enableInstancing;
            runtimeMaterial.doubleSidedGI = sourceMaterial.doubleSidedGI;
            runtimeMaterial.renderQueue = 3000;

            CopyTexture(sourceMaterial, runtimeMaterial, BaseMapId, BaseMapId, MainTexId);
            CopyColor(sourceMaterial, runtimeMaterial, BaseColorId, BaseColorId, ColorId);
            CopyFloat(sourceMaterial, runtimeMaterial, MetallicId, MetallicId);
            CopyFloat(sourceMaterial, runtimeMaterial, SmoothnessId, SmoothnessId);
            CopyFloat(sourceMaterial, runtimeMaterial, CutoffId, CutoffId);
            CopyFloat(sourceMaterial, runtimeMaterial, CullId, CullId);
            return runtimeMaterial;
        }

        private static void CopyTexture(Material sourceMaterial, Material targetMaterial, int targetId, params int[] sourceIds)
        {
            if (!targetMaterial.HasProperty(targetId))
            {
                return;
            }

            for (int index = 0; index < sourceIds.Length; index++)
            {
                int sourceId = sourceIds[index];
                if (!sourceMaterial.HasProperty(sourceId))
                {
                    continue;
                }

                Texture texture = sourceMaterial.GetTexture(sourceId);
                if (texture == null)
                {
                    continue;
                }

                targetMaterial.SetTexture(targetId, texture);
                targetMaterial.SetTextureScale(targetId, sourceMaterial.GetTextureScale(sourceId));
                targetMaterial.SetTextureOffset(targetId, sourceMaterial.GetTextureOffset(sourceId));
                return;
            }
        }

        private static void CopyColor(Material sourceMaterial, Material targetMaterial, int targetId, params int[] sourceIds)
        {
            if (!targetMaterial.HasProperty(targetId))
            {
                return;
            }

            for (int index = 0; index < sourceIds.Length; index++)
            {
                int sourceId = sourceIds[index];
                if (sourceMaterial.HasProperty(sourceId))
                {
                    targetMaterial.SetColor(targetId, sourceMaterial.GetColor(sourceId));
                    return;
                }
            }
        }

        private static void CopyFloat(Material sourceMaterial, Material targetMaterial, int targetId, params int[] sourceIds)
        {
            if (!targetMaterial.HasProperty(targetId))
            {
                return;
            }

            for (int index = 0; index < sourceIds.Length; index++)
            {
                int sourceId = sourceIds[index];
                if (sourceMaterial.HasProperty(sourceId))
                {
                    targetMaterial.SetFloat(targetId, sourceMaterial.GetFloat(sourceId));
                    return;
                }
            }
        }

        private sealed class RendererState
        {
            public readonly Renderer TargetRenderer;
            public readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();
            public Vector3 CurrentCenter;
            public Vector3 TargetCenter;
            public float Fade;

            private readonly Material[] _originalMaterials;
            private readonly Material[] _runtimeMaterials;
            private readonly bool _usesRuntimeMaterials;
            private bool _centerInitialized;

            public RendererState(Renderer targetRenderer, Shader occlusionShader)
            {
                TargetRenderer = targetRenderer;
                _originalMaterials = targetRenderer.sharedMaterials;

                if (occlusionShader == null)
                {
                    return;
                }

                _runtimeMaterials = new Material[_originalMaterials.Length];
                for (int index = 0; index < _originalMaterials.Length; index++)
                {
                    _runtimeMaterials[index] = CreateRuntimeMaterial(_originalMaterials[index], occlusionShader);
                }

                TargetRenderer.sharedMaterials = _runtimeMaterials;
                _usesRuntimeMaterials = true;
            }

            public int MaterialCount
            {
                get { return TargetRenderer != null ? TargetRenderer.sharedMaterials.Length : 0; }
            }

            public void SetTargetCenter(Vector3 revealCenter, Vector3 playerTargetPosition)
            {
                if (_centerInitialized)
                {
                    float oldDistance = (TargetCenter - playerTargetPosition).sqrMagnitude;
                    float newDistance = (revealCenter - playerTargetPosition).sqrMagnitude;
                    if (newDistance > oldDistance)
                    {
                        return;
                    }
                }

                TargetCenter = revealCenter;
                if (!_centerInitialized)
                {
                    CurrentCenter = revealCenter;
                    _centerInitialized = true;
                }
            }

            public void UpdateCenter(float deltaTime, float followSpeed)
            {
                if (!_centerInitialized)
                {
                    return;
                }

                float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSpeed) * deltaTime);
                CurrentCenter = Vector3.Lerp(CurrentCenter, TargetCenter, t);
            }

            public void RestoreOriginalMaterials()
            {
                if (TargetRenderer != null && _originalMaterials != null)
                {
                    TargetRenderer.sharedMaterials = _originalMaterials;
                }

                ReleaseRuntimeMaterials();
            }

            public void ReleaseRuntimeMaterials()
            {
                if (!_usesRuntimeMaterials || _runtimeMaterials == null)
                {
                    return;
                }

                for (int index = 0; index < _runtimeMaterials.Length; index++)
                {
                    Material material = _runtimeMaterials[index];
                    if (material == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Object.Destroy(material);
                    }
                    else
                    {
                        Object.DestroyImmediate(material);
                    }

                    _runtimeMaterials[index] = null;
                }
            }
        }
    }
}
