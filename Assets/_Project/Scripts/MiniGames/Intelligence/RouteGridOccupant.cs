using System.Collections;
using UnityEngine;

namespace Game
{


    public class RouteGridOccupant : MonoBehaviour
    {
        public Vector2Int GridPosition;
        public RouteCellType CellType = RouteCellType.Argument;

        [Header("Grid Position")]
        [SerializeField] private bool derivePositionFromTransform = true;
        [SerializeField] private GridManager grid;

        [Header("Optional Visuals")]
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private Renderer[] tintedRenderers;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool autoTint = true;
        [SerializeField] private bool hideWhenConsumed = true;
        [SerializeField] private Color wallColor = new(0.18f, 0.18f, 0.18f, 1f);
        [SerializeField] private Color argumentColor = new(0.95f, 0.8f, 0.2f, 1f);
        [SerializeField] private Color exitColor = new(0.2f, 0.8f, 0.35f, 1f);
        [SerializeField] private Color forbiddenColor = new(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color timedBarrierBlockedColor = new(0.82f, 0.22f, 0.22f, 1f);
        [SerializeField] private Color timedBarrierPassableColor = new(0.48f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color previewArgumentColor = new(0.38f, 0.92f, 0.52f, 1f);

        [Header("Argument Rules")]
        [SerializeField, Min(0)] private int argumentSequenceOrder;

        [Header("Timed Barrier")]
        [SerializeField] private bool timedBarrierStartsPassable;
        [SerializeField] private bool timedBarrierAutoLowerDistance = true;
        [SerializeField, Min(0f)] private float timedBarrierLowerDistance = 0.04f;
        [SerializeField, Min(0f)] private float timedBarrierTransitionDuration = 0.08f;

        private bool _consumed;
        private bool _timedBarrierIsPassable;
        private Vector3 _timedBarrierRaisedPosition;
        private Vector3 _timedBarrierLoweredPosition;
        private Coroutine _timedBarrierRoutine;
        private bool _timedBarrierPositionsInitialized;
        private bool _previewHighlighted;
        private MaterialPropertyBlock _propertyBlock;

        public bool BlocksMovement => CellType == RouteCellType.Wall ||
                                      (CellType == RouteCellType.TimedBarrier && !_timedBarrierIsPassable);
        public bool IsForbidden => CellType == RouteCellType.Forbidden;
        public bool IsExit => CellType == RouteCellType.Exit;
        public bool HasAvailableArgument => CellType == RouteCellType.Argument && !_consumed;
        public bool IsArgumentOccupant => CellType == RouteCellType.Argument;
        public int ArgumentSequenceOrder => Mathf.Max(0, argumentSequenceOrder);

        public void SyncGridPosition(GridManager fallbackGrid = null)
        {
            if (!derivePositionFromTransform)
            {
                return;
            }

            GridManager targetGrid = grid != null ? grid : fallbackGrid;
            if (targetGrid == null)
            {
                targetGrid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Include);
            }

            if (targetGrid != null && targetGrid.TryGetGridPositionFromWorld(transform.position, out Vector2Int resolvedPosition))
            {
                GridPosition = resolvedPosition;
            }
        }

        public void ResetState()
        {
            _consumed = false;
            _previewHighlighted = false;

            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            if (hideWhenConsumed)
            {
                visualRoot.SetActive(true);
            }

            ResetTimedBarrierState();
            RefreshVisual();
        }

        public void AdvanceTurn(GridManager fallbackGrid = null)
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                return;
            }

            CacheTimedBarrierPositions(fallbackGrid);
            _timedBarrierIsPassable = !_timedBarrierIsPassable;
            ApplyTimedBarrierState(true, fallbackGrid);
            RefreshVisual();
        }

        public bool TryCollectArgument()
        {
            if (!HasAvailableArgument)
            {
                return false;
            }

            _consumed = true;

            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            if (hideWhenConsumed)
            {
                visualRoot.SetActive(false);
            }

            RefreshVisual();
            return true;
        }

        public bool IsBlockedAtTurn(int turnIndex)
        {
            turnIndex = Mathf.Max(0, turnIndex);

            return CellType == RouteCellType.Wall ||
                   (CellType == RouteCellType.TimedBarrier && !IsTimedBarrierPassableAtTurn(turnIndex));
        }

        public bool IsForbiddenAtTurn(int turnIndex)
        {
            return CellType == RouteCellType.Forbidden;
        }

        public bool IsTimedBarrierPassableAtTurn(int turnIndex)
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                return false;
            }

            turnIndex = Mathf.Max(0, turnIndex);
            if ((turnIndex & 1) == 0)
            {
                return timedBarrierStartsPassable;
            }

            return !timedBarrierStartsPassable;
        }

        public void SetRoutePreviewHighlighted(bool highlighted)
        {
            if (_previewHighlighted == highlighted)
            {
                return;
            }

            _previewHighlighted = highlighted;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (!autoTint)
            {
                return;
            }

            Color targetColor = CellType switch
            {
                RouteCellType.Wall => wallColor,
                RouteCellType.Argument => argumentColor,
                RouteCellType.Exit => exitColor,
                RouteCellType.Forbidden => forbiddenColor,
                RouteCellType.TimedBarrier => _timedBarrierIsPassable ? timedBarrierPassableColor : timedBarrierBlockedColor,
                _ => Color.white
            };

            if (IsArgumentOccupant && !_consumed && _previewHighlighted)
            {
                targetColor = Color.Lerp(targetColor, previewArgumentColor, 0.75f);
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = targetColor;
            }

            if (tintedRenderers == null || tintedRenderers.Length == 0)
            {
                tintedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int index = 0; index < tintedRenderers.Length; index++)
            {
                Renderer targetRenderer = tintedRenderers[index];
                if (targetRenderer == null || targetRenderer.sharedMaterial == null || !targetRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    continue;
                }

                if (!targetRenderer.gameObject.scene.IsValid())
                {
                    continue;
                }

                _propertyBlock ??= new MaterialPropertyBlock();
                _propertyBlock.Clear();
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_Color", targetColor);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void Awake()
        {
            SyncGridPosition();

            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            ResetState();
        }

        private void OnValidate()
        {
            if (!CanRefreshInValidation())
            {
                return;
            }

            SyncGridPosition();
            _timedBarrierIsPassable = CellType == RouteCellType.TimedBarrier && timedBarrierStartsPassable;
            RefreshVisual();
        }

        private void ResetTimedBarrierState()
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                StopTimedBarrierAnimation();
                return;
            }

            CacheTimedBarrierPositions();
            _timedBarrierIsPassable = timedBarrierStartsPassable;
            ApplyTimedBarrierState(false);
        }

        private void CacheTimedBarrierPositions(GridManager fallbackGrid = null)
        {
            if (visualRoot == null)
            {
                visualRoot = gameObject;
            }

            GridManager targetGrid = grid != null ? grid : fallbackGrid;
            if (targetGrid == null)
            {
                targetGrid = FindAnyObjectByType<GridManager>(FindObjectsInactive.Include);
            }

            Vector3 surfaceNormal = targetGrid != null ? targetGrid.GetSurfaceNormal() : transform.up;
            if (surfaceNormal.sqrMagnitude < 0.0001f)
            {
                surfaceNormal = Vector3.up;
            }

            surfaceNormal.Normalize();
            Transform targetVisual = visualRoot != null ? visualRoot.transform : transform;
            if (!_timedBarrierPositionsInitialized || !Application.isPlaying)
            {
                _timedBarrierRaisedPosition = targetVisual.position;
                _timedBarrierPositionsInitialized = true;
            }

            float lowerDistance = GetTimedBarrierLowerDistance(surfaceNormal);
            _timedBarrierLoweredPosition = _timedBarrierRaisedPosition - (surfaceNormal * lowerDistance);
        }

        private void ApplyTimedBarrierState(bool animate, GridManager fallbackGrid = null)
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                return;
            }

            CacheTimedBarrierPositions(fallbackGrid);

            Vector3 targetPosition = _timedBarrierIsPassable
                ? _timedBarrierLoweredPosition
                : _timedBarrierRaisedPosition;

            StopTimedBarrierAnimation();

            if (!animate || !Application.isPlaying || timedBarrierTransitionDuration <= 0f)
            {
                (visualRoot != null ? visualRoot.transform : transform).position = targetPosition;
                return;
            }

            _timedBarrierRoutine = StartCoroutine(AnimateTimedBarrier(targetPosition));
        }

        private IEnumerator AnimateTimedBarrier(Vector3 targetPosition)
        {
            Transform targetVisual = visualRoot != null ? visualRoot.transform : transform;
            Vector3 startPosition = targetVisual.position;
            float elapsed = 0f;

            while (elapsed < timedBarrierTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / timedBarrierTransitionDuration);
                targetVisual.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            targetVisual.position = targetPosition;
            _timedBarrierRoutine = null;
        }

        private void StopTimedBarrierAnimation()
        {
            if (_timedBarrierRoutine == null)
            {
                return;
            }

            StopCoroutine(_timedBarrierRoutine);
            _timedBarrierRoutine = null;
        }

        private float GetTimedBarrierLowerDistance(Vector3 surfaceNormal)
        {
            if (!timedBarrierAutoLowerDistance)
            {
                return timedBarrierLowerDistance;
            }

            if (tintedRenderers == null || tintedRenderers.Length == 0)
            {
                tintedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            float furthestExtent = 0f;
            for (int index = 0; index < tintedRenderers.Length; index++)
            {
                Renderer targetRenderer = tintedRenderers[index];
                if (targetRenderer == null)
                {
                    continue;
                }

                Bounds bounds = targetRenderer.bounds;
                Vector3 extents = bounds.extents;
                float projectedExtent =
                    Mathf.Abs(Vector3.Dot(surfaceNormal, targetRenderer.transform.right)) * extents.x +
                    Mathf.Abs(Vector3.Dot(surfaceNormal, targetRenderer.transform.up)) * extents.y +
                    Mathf.Abs(Vector3.Dot(surfaceNormal, targetRenderer.transform.forward)) * extents.z;

                furthestExtent = Mathf.Max(furthestExtent, projectedExtent);
            }

            if (furthestExtent <= 0.00001f)
            {
                return timedBarrierLowerDistance;
            }

            return furthestExtent * 2.2f;
        }

        private bool CanRefreshInValidation()
        {
            if (Application.isPlaying)
            {
                return true;
            }

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return false;
            }

            if (!isActiveAndEnabled)
            {
                return false;
            }

#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer ||
                UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating)
            {
                return false;
            }
#endif

            return true;
        }
    }
}
