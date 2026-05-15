using UnityEngine;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(Camera), typeof(CameraObstacleTransparency))]
    [DefaultExecutionOrder(10000)]
    public class CameraMovementController : MonoBehaviour
    {
        private const float MinRange = 0.01f;

        [Header("Follow")]
        [SerializeField] private Vector3 offset = new(0f, 10f, -10f);
        [SerializeField, Range(0f, 1f)] private float followSmoothness = 0.125f;
        [SerializeField] private float verticalBias = 1f;

        [Header("Framing")]
        [SerializeField] private float framingForwardOffset = 0.75f;
        [SerializeField] private float framingRightOffset = 0f;

        [Header("Soft Zone")]
        [SerializeField] private float softZoneWidth = 1.4f;
        [SerializeField] private float softZoneDepth = 1f;

        [Header("Movement Look Ahead")]
        [SerializeField] private float movementLookAheadDistance = 0.45f;
        [SerializeField] private float movementLookAheadFullAtSpeed = 3.5f;
        [SerializeField] private float movementLookAheadMinSpeed = 0.05f;
        [SerializeField] private float movementLookAheadSmoothTime = 0.18f;
        [SerializeField] private float movementLookAheadReturnSmoothTime = 0.35f;
        [SerializeField] private float movementLookAheadTeleportResetDistance = 3f;

        [Header("Rotation")]
        [SerializeField] private float angleX = 30f;
        [SerializeField] private float angleY = 45f;

        [Header("Zoom")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minFOV = 15f;
        [SerializeField] private float maxFOV = 90f;
        [SerializeField] private float defaultFieldOfView = 30f;
        [SerializeField] private float zoomTransitionSpeed = 8f;
        [SerializeField] private float zoomInEffectRange = 5f;
        [SerializeField] private float zoomOutEffectRange = 12f;
        [SerializeField] private float zoomInDistanceOffset = 2.25f;
        [SerializeField] private float zoomOutDistanceOffset = 4.5f;
        [SerializeField] private float zoomInVerticalOffset = -0.375f;
        [SerializeField] private float zoomOutVerticalOffset = 0.625f;
        [SerializeField] private float zoomInAngleXOffset = -2.1f;
        [SerializeField] private float zoomOutAngleXOffset = 2.8f;

        private Camera _camera;
        private Transform _target;
        private float _targetFieldOfView;
        private float _zoomInBlend;
        private float _zoomOutBlend;
        private Vector3 _followAnchorPosition;
        private Vector3 _currentMovementLookAhead;
        private Vector3 _movementLookAheadVelocity;
        private Vector3 _lastTargetPosition;
        private bool _hasFollowAnchor;
        private bool _hasTargetPositionSample;

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
            RemoveInvalidZenjectAutoInjecter();
            ConfigureCamera();
        }

        private void OnValidate()
        {
            followSmoothness = Mathf.Clamp01(followSmoothness);
            softZoneWidth = Mathf.Max(MinRange, softZoneWidth);
            softZoneDepth = Mathf.Max(MinRange, softZoneDepth);
            zoomSpeed = Mathf.Max(MinRange, zoomSpeed);
            minFOV = Mathf.Max(1f, minFOV);
            maxFOV = Mathf.Max(minFOV, maxFOV);
            defaultFieldOfView = Mathf.Clamp(defaultFieldOfView, minFOV, maxFOV);
            zoomTransitionSpeed = Mathf.Max(MinRange, zoomTransitionSpeed);
            zoomInEffectRange = Mathf.Max(MinRange, zoomInEffectRange);
            zoomOutEffectRange = Mathf.Max(MinRange, zoomOutEffectRange);
            movementLookAheadDistance = Mathf.Max(0f, movementLookAheadDistance);
            movementLookAheadFullAtSpeed = Mathf.Max(MinRange, movementLookAheadFullAtSpeed);
            movementLookAheadMinSpeed = Mathf.Max(0f, movementLookAheadMinSpeed);
            movementLookAheadSmoothTime = Mathf.Max(MinRange, movementLookAheadSmoothTime);
            movementLookAheadReturnSmoothTime = Mathf.Max(MinRange, movementLookAheadReturnSmoothTime);
            movementLookAheadTeleportResetDistance = Mathf.Max(MinRange, movementLookAheadTeleportResetDistance);
        }

        public void SetTarget(Transform target)
        {
            _target = target;
            _followAnchorPosition = target != null ? target.position : Vector3.zero;
            _currentMovementLookAhead = Vector3.zero;
            _movementLookAheadVelocity = Vector3.zero;
            _lastTargetPosition = target != null ? target.position : Vector3.zero;
            _hasFollowAnchor = target != null;
            _hasTargetPositionSample = target != null;
        }

        private void Update()
        {
            if (_camera == null || DialogManager.Instance?.Active == true)
            {
                return;
            }

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) <= 0.01f)
            {
                return;
            }

            _targetFieldOfView = Mathf.Clamp(_targetFieldOfView - scroll * zoomSpeed, minFOV, maxFOV);
        }

        private void LateUpdate()
        {
            UpdateZoomState();
            if (_target == null)
            {
                return;
            }

            UpdateZoomBlends();
            UpdateRotation();
            UpdateMovementLookAhead();
            UpdateFollowAnchor();
            UpdatePosition();
        }

        private void ConfigureCamera()
        {
            if (_camera == null)
            {
                return;
            }

            _camera.orthographic = false;
            _targetFieldOfView = Mathf.Clamp(defaultFieldOfView, minFOV, maxFOV);
            _camera.fieldOfView = _targetFieldOfView;
            transform.rotation = Quaternion.Euler(angleX, angleY, 0f);
        }

        private void UpdateZoomState()
        {
            if (_camera == null)
            {
                return;
            }

            float zoomLerpFactor = GetFrameRateIndependentLerpFactor(zoomTransitionSpeed);
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _targetFieldOfView, zoomLerpFactor);
        }

        private void UpdateZoomBlends()
        {
            if (_camera == null)
            {
                _zoomInBlend = 0f;
                _zoomOutBlend = 0f;
                return;
            }

            float currentFov = _camera.fieldOfView;
            _zoomInBlend = GetZoomInBlend(currentFov);
            _zoomOutBlend = GetZoomOutBlend(currentFov);
        }

        private void UpdateRotation()
        {
            float desiredAngleX = angleX
                + zoomInAngleXOffset * _zoomInBlend
                + zoomOutAngleXOffset * _zoomOutBlend;

            Quaternion desiredRotation = Quaternion.Euler(desiredAngleX, angleY, 0f);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                GetFrameRateIndependentLerpFactor(zoomTransitionSpeed));
        }

        private void UpdatePosition()
        {
            Vector3 anchorPosition = _hasFollowAnchor ? _followAnchorPosition : _target.position;
            Vector3 focusPoint = anchorPosition + Vector3.up * verticalBias + GetFramingOffset();
            Vector3 desiredPosition = focusPoint + GetCurrentOffset();
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                GetFrameRateIndependentFollowFactor());
        }

        private void UpdateFollowAnchor()
        {
            if (_target == null)
            {
                _followAnchorPosition = Vector3.zero;
                _hasFollowAnchor = false;
                return;
            }

            Vector3 desiredSubjectPosition = _target.position + _currentMovementLookAhead;
            if (!_hasFollowAnchor)
            {
                _followAnchorPosition = desiredSubjectPosition;
                _hasFollowAnchor = true;
                return;
            }

            Vector3 planarRight = GetPlanarAxis(transform.right, Vector3.right);
            Vector3 planarForward = GetPlanarAxis(transform.forward, Vector3.forward);
            Vector3 planarDelta = desiredSubjectPosition - _followAnchorPosition;
            planarDelta.y = 0f;

            float halfWidth = softZoneWidth * 0.5f;
            float halfDepth = softZoneDepth * 0.5f;
            float rightOffset = Vector3.Dot(planarDelta, planarRight);
            float forwardOffset = Vector3.Dot(planarDelta, planarForward);

            if (rightOffset > halfWidth)
            {
                _followAnchorPosition += planarRight * (rightOffset - halfWidth);
            }
            else if (rightOffset < -halfWidth)
            {
                _followAnchorPosition += planarRight * (rightOffset + halfWidth);
            }

            if (forwardOffset > halfDepth)
            {
                _followAnchorPosition += planarForward * (forwardOffset - halfDepth);
            }
            else if (forwardOffset < -halfDepth)
            {
                _followAnchorPosition += planarForward * (forwardOffset + halfDepth);
            }

            _followAnchorPosition.y = _target.position.y;
        }

        private void UpdateMovementLookAhead()
        {
            if (_target == null)
            {
                _currentMovementLookAhead = Vector3.zero;
                _movementLookAheadVelocity = Vector3.zero;
                _hasTargetPositionSample = false;
                return;
            }

            if (!_hasTargetPositionSample)
            {
                _lastTargetPosition = _target.position;
                _hasTargetPositionSample = true;
                _currentMovementLookAhead = Vector3.zero;
                _movementLookAheadVelocity = Vector3.zero;
                return;
            }

            Vector3 positionDelta = _target.position - _lastTargetPosition;
            _lastTargetPosition = _target.position;
            positionDelta.y = 0f;

            if (positionDelta.sqrMagnitude >= movementLookAheadTeleportResetDistance * movementLookAheadTeleportResetDistance)
            {
                _currentMovementLookAhead = Vector3.zero;
                _movementLookAheadVelocity = Vector3.zero;
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float planarSpeed = positionDelta.magnitude / deltaTime;
            Vector3 desiredLookAhead = Vector3.zero;
            if (positionDelta.sqrMagnitude > 0.000001f && planarSpeed > movementLookAheadMinSpeed)
            {
                float speedFactor = Mathf.Clamp01(planarSpeed / movementLookAheadFullAtSpeed);
                desiredLookAhead = positionDelta.normalized * movementLookAheadDistance * speedFactor;
            }

            float smoothTime = desiredLookAhead.sqrMagnitude > 0.000001f
                ? movementLookAheadSmoothTime
                : movementLookAheadReturnSmoothTime;

            _currentMovementLookAhead = Vector3.SmoothDamp(
                _currentMovementLookAhead,
                desiredLookAhead,
                ref _movementLookAheadVelocity,
                smoothTime,
                Mathf.Infinity,
                Time.deltaTime);
        }

        private Vector3 GetCurrentOffset()
        {
            Vector3 orbitDirection = GetOrbitDirection();
            float distanceOffset = -zoomInDistanceOffset * _zoomInBlend + zoomOutDistanceOffset * _zoomOutBlend;
            float verticalOffset = zoomInVerticalOffset * _zoomInBlend + zoomOutVerticalOffset * _zoomOutBlend;
            return offset + orbitDirection * distanceOffset + Vector3.up * verticalOffset;
        }

        private Vector3 GetFramingOffset()
        {
            Vector3 planarForward = GetPlanarAxis(transform.forward, Vector3.forward);
            Vector3 planarRight = GetPlanarAxis(transform.right, Vector3.right);
            return planarForward * framingForwardOffset + planarRight * framingRightOffset;
        }

        private float GetZoomInBlend(float currentFov)
        {
            if (currentFov >= defaultFieldOfView)
            {
                return 0f;
            }

            float zoomInLimit = Mathf.Max(minFOV, defaultFieldOfView - zoomInEffectRange);
            if (Mathf.Approximately(defaultFieldOfView, zoomInLimit))
            {
                return 1f;
            }

            return Mathf.InverseLerp(defaultFieldOfView, zoomInLimit, currentFov);
        }

        private float GetZoomOutBlend(float currentFov)
        {
            if (currentFov <= defaultFieldOfView)
            {
                return 0f;
            }

            float zoomOutLimit = Mathf.Min(maxFOV, defaultFieldOfView + zoomOutEffectRange);
            if (Mathf.Approximately(defaultFieldOfView, zoomOutLimit))
            {
                return 1f;
            }

            return Mathf.InverseLerp(defaultFieldOfView, zoomOutLimit, currentFov);
        }

        private Vector3 GetOrbitDirection()
        {
            if (offset.sqrMagnitude > 0.0001f)
            {
                return offset.normalized;
            }

            return (Quaternion.Euler(angleX, angleY, 0f) * Vector3.back).normalized;
        }

        private Vector3 GetPlanarAxis(Vector3 axis, Vector3 fallback)
        {
            axis.y = 0f;
            if (axis.sqrMagnitude > 0.0001f)
            {
                return axis.normalized;
            }

            return fallback;
        }

        private float GetFrameRateIndependentFollowFactor()
        {
            if (followSmoothness <= 0f)
            {
                return 0f;
            }

            if (followSmoothness >= 1f)
            {
                return 1f;
            }

            return 1f - Mathf.Pow(1f - followSmoothness, Time.deltaTime * 60f);
        }

        private float GetFrameRateIndependentLerpFactor(float speed)
        {
            return 1f - Mathf.Exp(-Mathf.Max(MinRange, speed) * Time.deltaTime);
        }

        private void RemoveInvalidZenjectAutoInjecter()
        {
            if (GetComponent<Animator>() != null)
            {
                return;
            }

            ZenjectStateMachineBehaviourAutoInjecter autoInjecter = GetComponent<ZenjectStateMachineBehaviourAutoInjecter>();
            if (autoInjecter != null)
            {
                Destroy(autoInjecter);
            }
        }
    }
}
