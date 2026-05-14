using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game.MiniGame.PowerCheck
{
    [RequireComponent(typeof(MiniGamePlayer))]
    public class PlayerMove : MonoBehaviour
    {
        [Header("Movement Params")]
        [SerializeField] private float _runDefaultSpeed = 6.0f;
        [SerializeField] private float _runSpeed = 6.0f;
        [SerializeField] private float _rotationSpeed = 20f;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private MiniGamePlayer _characteristics;

        [Header("Mouse Control")]
        [SerializeField] private bool _enableMouseControl = true;
        [SerializeField] private float _mouseStopDistance = 0.15f;
        [SerializeField] private float _clickMoveHoldThreshold = 0.18f;
        [SerializeField] private float _clickMoveDragThreshold = 12f;

        private Rigidbody _rb;
        private Camera _movementCamera;
        private bool _underDebuff;
        private GameObject _debuffEffect;
        private bool _mouseHoldActive;
        private Vector2 _mousePressScreenPosition;
        private float _mousePressStartedAt;
        private Vector3 _heldMouseWorldPoint;
        private bool _hasHeldMouseWorldPoint;
        private Vector3 _clickMoveTarget;
        private bool _hasClickMoveTarget;

        private void Awake()
        {
            RefreshCameraReference();

            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _characteristics = GetComponent<MiniGamePlayer>();

            FindDebuffEffect();
        }

        private void Start()
        {
            InitializeMovementSpeed();
        }

        private void Update()
        {
            UpdateMouseInput();
        }

        private void FixedUpdate()
        {
            RefreshCameraReference();

            if (_debuffEffect != null)
            {
                bool shouldShowDebuff = _underDebuff;
                if (_debuffEffect.activeSelf != shouldShowDebuff)
                {
                    _debuffEffect.SetActive(shouldShowDebuff);
                }
            }

            HandleHorizontalMovement();
        }

        private void InitializeMovementSpeed()
        {
            float configuredSpeed = _characteristics != null && _characteristics.Speed > 0f
                ? _characteristics.Speed
                : (_runSpeed > 0f ? _runSpeed : _runDefaultSpeed);

            if (configuredSpeed <= 0f)
            {
                configuredSpeed = 6f;
            }

            _runDefaultSpeed = configuredSpeed;
            _runSpeed = configuredSpeed;
        }

        private void FindDebuffEffect()
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == "Indication")
                {
                    _debuffEffect = child.gameObject;
                    _debuffEffect.SetActive(false);
                    break;
                }
            }

            if (_debuffEffect == null)
            {
                Debug.LogWarning("Не найден объект 'Indication' для отображения дебаффа.");
            }
        }

        public void ChangeSpeed(float speed, bool isDebuff)
        {
            if (isDebuff)
            {
                _underDebuff = !Mathf.Approximately(speed, 1f);
                _runSpeed = _runDefaultSpeed * speed;

                if (_underDebuff && speed <= 0.01f)
                {
                    _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
                }

                return;
            }

            _underDebuff = false;
            _runSpeed = _runDefaultSpeed * speed;
        }

        private void HandleHorizontalMovement()
        {
            Vector3 moveDirection = ResolveMoveDirection();

            if (moveDirection.sqrMagnitude > 1f)
            {
                moveDirection.Normalize();
            }

            if (moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _rotationSpeed);
            }

            Vector3 velocity = moveDirection * _runSpeed;
            _rb.linearVelocity = new Vector3(velocity.x, _rb.linearVelocity.y, velocity.z);
        }

        private Vector3 ResolveMoveDirection()
        {
            if (TryGetHeldMouseMoveDirection(out Vector3 heldMouseMoveDirection))
            {
                return heldMouseMoveDirection;
            }

            Vector3 keyboardMoveDirection = GetKeyboardMoveDirection();
            if (keyboardMoveDirection.sqrMagnitude > 0.001f)
            {
                _hasClickMoveTarget = false;
                return keyboardMoveDirection;
            }

            if (TryGetClickTargetMoveDirection(out Vector3 clickTargetMoveDirection))
            {
                return clickTargetMoveDirection;
            }

            return Vector3.zero;
        }

        private Vector3 GetKeyboardMoveDirection()
        {
            Vector2 moveInput = InputManager.GetInstance().GetMoveDirection();

            if (_cameraTransform != null)
            {
                Vector3 cameraForward = _cameraTransform.forward;
                cameraForward.y = 0f;
                cameraForward.Normalize();

                Vector3 cameraRight = _cameraTransform.right;
                cameraRight.y = 0f;
                cameraRight.Normalize();

                return cameraRight * moveInput.x + cameraForward * moveInput.y;
            }

            return new Vector3(moveInput.x, 0f, moveInput.y);
        }

        private void UpdateMouseInput()
        {
            if (!_enableMouseControl)
            {
                _mouseHoldActive = false;
                _hasHeldMouseWorldPoint = false;
                _hasClickMoveTarget = false;
                return;
            }

            bool pressedThisFrame = WasPrimaryPointerPressedThisFrame();
            bool releasedThisFrame = WasPrimaryPointerReleasedThisFrame();
            bool isPressed = IsPrimaryPointerPressed();

            if (pressedThisFrame)
            {
                if (IsPointerOverUi() || !TryReadPointerPosition(out _mousePressScreenPosition))
                {
                    _mouseHoldActive = false;
                    _hasHeldMouseWorldPoint = false;
                }
                else
                {
                    _mouseHoldActive = true;
                    _mousePressStartedAt = Time.unscaledTime;
                    _hasClickMoveTarget = false;
                    UpdateHeldMouseWorldPoint();
                }
            }

            if (_mouseHoldActive && isPressed)
            {
                UpdateHeldMouseWorldPoint();
            }

            if (_mouseHoldActive && releasedThisFrame)
            {
                Vector2 releaseScreenPosition = _mousePressScreenPosition;
                TryReadPointerPosition(out releaseScreenPosition);

                bool isClick =
                    Time.unscaledTime - _mousePressStartedAt <= _clickMoveHoldThreshold &&
                    (releaseScreenPosition - _mousePressScreenPosition).sqrMagnitude <=
                    _clickMoveDragThreshold * _clickMoveDragThreshold;

                if (isClick && _hasHeldMouseWorldPoint)
                {
                    _clickMoveTarget = _heldMouseWorldPoint;
                    _hasClickMoveTarget = true;
                }
                else
                {
                    _hasClickMoveTarget = false;
                }

                _mouseHoldActive = false;
                _hasHeldMouseWorldPoint = false;
            }
            else if (_mouseHoldActive && !isPressed)
            {
                _mouseHoldActive = false;
                _hasHeldMouseWorldPoint = false;
            }
        }

        private bool TryGetHeldMouseMoveDirection(out Vector3 moveDirection)
        {
            moveDirection = Vector3.zero;

            if (!_mouseHoldActive || !_hasHeldMouseWorldPoint)
            {
                return false;
            }

            moveDirection = _heldMouseWorldPoint - transform.position;
            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude <= _mouseStopDistance * _mouseStopDistance)
            {
                moveDirection = Vector3.zero;
            }

            return true;
        }

        private bool TryGetClickTargetMoveDirection(out Vector3 moveDirection)
        {
            moveDirection = Vector3.zero;

            if (!_hasClickMoveTarget)
            {
                return false;
            }

            moveDirection = _clickMoveTarget - transform.position;
            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude <= _mouseStopDistance * _mouseStopDistance)
            {
                _hasClickMoveTarget = false;
                moveDirection = Vector3.zero;
                return false;
            }

            return true;
        }

        private void UpdateHeldMouseWorldPoint()
        {
            if (TryGetPointerWorldPoint(out Vector3 worldPoint))
            {
                _heldMouseWorldPoint = worldPoint;
                _hasHeldMouseWorldPoint = true;
            }
        }

        private bool TryGetPointerWorldPoint(out Vector3 worldPoint)
        {
            worldPoint = default;

            Camera movementCamera = GetMovementCamera();
            if (movementCamera == null || !TryReadPointerPosition(out Vector2 pointerPosition))
            {
                return false;
            }

            Ray pointerRay = movementCamera.ScreenPointToRay(pointerPosition);
            Plane movementPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!movementPlane.Raycast(pointerRay, out float enter))
            {
                return false;
            }

            worldPoint = pointerRay.GetPoint(enter);
            worldPoint.y = transform.position.y;
            return true;
        }

        private void RefreshCameraReference()
        {
            if (_cameraTransform == null)
            {
                GameObject cameraObj = GameObject.FindGameObjectWithTag("PowerCheckCamera");
                if (cameraObj != null)
                {
                    _cameraTransform = cameraObj.transform;
                }
            }

            if (_movementCamera == null)
            {
                if (_cameraTransform != null)
                {
                    _movementCamera = _cameraTransform.GetComponent<Camera>();
                }

                _movementCamera ??= Camera.main;
            }
        }

        private Camera GetMovementCamera()
        {
            if (_movementCamera == null)
            {
                RefreshCameraReference();
            }

            return _movementCamera;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                position = pointer.position.ReadValue();
                return true;
            }
#endif
            position = Input.mousePosition;
            return true;
        }

        private static bool IsPrimaryPointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.isPressed;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                return pointer.press.isPressed;
            }
#endif
            return Input.GetMouseButton(0);
        }

        private static bool WasPrimaryPointerPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                return pointer.press.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(0);
        }

        private static bool WasPrimaryPointerReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasReleasedThisFrame;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                return pointer.press.wasReleasedThisFrame;
            }
#endif
            return Input.GetMouseButtonUp(0);
        }
    }
}
