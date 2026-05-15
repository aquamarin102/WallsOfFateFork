using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    public float maxSpeed = 6f;
    public float acceleration = 25f;
    public float deceleration = 35f;

    [Header("Mouse Control")]
    [SerializeField] private bool enableMouseControl = true;
    [SerializeField] private Transform movementCameraTransform;
    [SerializeField] private float mouseStopDistance = 0.15f;
    [SerializeField] private float clickMoveHoldThreshold = 0.18f;
    [SerializeField] private float clickMoveDragThreshold = 12f;

    [Header("Dash")]
    [SerializeField] private bool enableDash = true;
    [SerializeField] private float dashSpeed = 12.5f;
    [SerializeField] private float dashDuration = 0.16f;
    [SerializeField] private float dashCooldown = 0.45f;

    [Header("Arena")]
    public bool clampToArena = true;
    public float arenaPadding = 0.45f;
    public Transform arenaCenterOverride;
    public float arenaRadiusOverride = 0f;

    [Header("Pushback")]
    public float externalVelocityDecay = 12f;
    public float maxExternalVelocity = 3.5f;

    [Header("Facing")]
    public float rotationSpeed = 720f;
    public float facingAngleOffset = 0f;

    [Header("Runtime Tuning")]
    public float runtimeSpeedMultiplier = 1f;
    public float runtimeAccelerationMultiplier = 1f;
    public float runtimeDecelerationMultiplier = 1f;

    private Rigidbody _rb;
    private MovementModifiers _mods;
    private Camera _movementCamera;
    private Vector3 _input;
    private Vector3 _arenaCenter;
    private float _arenaRadius;
    private Vector3 _externalVelocity;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Vector3 _dashDirection = Vector3.forward;
    private Vector3 _clickMoveTarget;
    private Vector3 _heldMouseWorldPoint;
    private Vector2 _mousePressScreenPosition;
    private Transform _resolvedArenaTransform;
    private Transform _cachedArenaRadiusSource;
    private float _lastArenaRadiusOverride = float.NaN;
    private float _mousePressStartedAt;
    private float _dashEndTime = float.NegativeInfinity;
    private float _dashCooldownEndTime = float.NegativeInfinity;
    private float _lastPublishedDashRemaining = float.NaN;
    private bool _legacyInputAvailable = true;
    private bool _lastPublishedDashReady;
    private bool _mouseHoldActive;
    private bool _hasHeldMouseWorldPoint;
    private bool _hasClickMoveTarget;

    public float DashCooldownDuration => Mathf.Max(0f, dashCooldown);
    public float DashCooldownRemaining => Mathf.Max(0f, _dashCooldownEndTime - Time.time);
    public bool IsDashReady => DashCooldownRemaining <= 0f;

    public event System.Action<float, float, bool> OnDashCooldownChanged;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _mods = GetComponent<MovementModifiers>();
        RefreshArenaBounds(forceRadiusRefresh: true);
    }

    private void Update()
    {
        RefreshMovementCamera();
        UpdateMouseInput();
        _input = ReadMoveInput();
        if (_input.sqrMagnitude > 0.0001f)
            _lastMoveDirection = _input;
        UpdateDashInput();
        PublishDashCooldown();
    }

    private void FixedUpdate()
    {
        float speedMult = _mods ? _mods.speedMultiplier : 1f;
        float controlMult = _mods ? _mods.controlMultiplier : 1f;

        float targetSpeed = maxSpeed * speedMult * runtimeSpeedMultiplier;
        Vector3 desiredVel = _input * targetSpeed;

        Vector3 currentVel = _rb.linearVelocity;
        currentVel.y = 0f;
        Vector3 delta = desiredVel - currentVel;

        float accel = (_input.sqrMagnitude > 0.001f) ? acceleration : deceleration;
        accel *= controlMult;
        accel *= _input.sqrMagnitude > 0.001f ? runtimeAccelerationMultiplier : runtimeDecelerationMultiplier;

        Vector3 change = Vector3.ClampMagnitude(delta, accel * Time.fixedDeltaTime);
        Vector3 nextVelocity = currentVel + change;
        nextVelocity += GetDashVelocity();
        nextVelocity += _externalVelocity;
        float maxPlanarSpeed = targetSpeed + maxExternalVelocity + (IsDashActive() ? dashSpeed : 0f);
        nextVelocity = Vector3.ClampMagnitude(nextVelocity, maxPlanarSpeed);
        nextVelocity.y = 0f;
        _rb.linearVelocity = nextVelocity;

        _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, externalVelocityDecay * Time.fixedDeltaTime);

        RotateTowardsMovement(nextVelocity);

        if (clampToArena)
        {
            RefreshArenaBounds();
            Vector3 clampedPosition = AgilitySceneUtility.ClampToArena(transform.position, _arenaCenter, _arenaRadius, arenaPadding);
            if ((clampedPosition - transform.position).sqrMagnitude > 0.0001f)
            {
                _rb.position = clampedPosition;

                Vector3 planarNormal = clampedPosition - _arenaCenter;
                planarNormal.y = 0f;
                if (planarNormal.sqrMagnitude > 0.0001f)
                {
                    Vector3 outwardVelocity = Vector3.Project(_rb.linearVelocity, planarNormal.normalized);
                    if (Vector3.Dot(outwardVelocity, planarNormal) > 0f)
                        _rb.linearVelocity -= outwardVelocity;
                }
            }
        }

    }

    private Vector3 ReadMoveInput()
    {
        Vector2 move = ReadSharedMoveInput();

        if (move.sqrMagnitude > 0.0001f)
        {
            _mouseHoldActive = false;
            _hasHeldMouseWorldPoint = false;
            _hasClickMoveTarget = false;
            return new Vector3(move.x, 0f, move.y).normalized;
        }

        if (TryGetHeldMouseMoveDirection(out Vector3 heldMoveDirection))
            return heldMoveDirection.normalized;

        if (TryGetClickTargetMoveDirection(out Vector3 clickMoveDirection))
            return clickMoveDirection.normalized;

        return Vector3.zero;
    }

    private Vector2 ReadSharedMoveInput()
    {
        Vector2 move = Vector2.zero;

        var inputManager = InputManager.GetInstance();
        if (inputManager != null)
        {
            Vector3 sharedMove = inputManager.GetMoveDirection();
            move = new Vector2(sharedMove.x, sharedMove.y);
        }

#if ENABLE_INPUT_SYSTEM
        if (move.sqrMagnitude <= 0.0001f)
            move = ReadInputSystemMove();
#endif

        if (move.sqrMagnitude <= 0.0001f)
            move = ReadLegacyMove();

        return Vector2.ClampMagnitude(move, 1f);
    }

#if ENABLE_INPUT_SYSTEM
    private Vector2 ReadInputSystemMove()
    {
        Vector2 move = Vector2.zero;

        var gamepad = Gamepad.current;
        if (gamepad != null)
            move = gamepad.leftStick.ReadValue();

        if (move.sqrMagnitude > 0.0001f)
            return Vector2.ClampMagnitude(move, 1f);

        var keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            move.x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            move.x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            move.y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            move.y += 1f;

        return Vector2.ClampMagnitude(move, 1f);
    }
#endif

    private Vector2 ReadLegacyMove()
    {
        if (!_legacyInputAvailable)
            return Vector2.zero;

        try
        {
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }
        catch
        {
            _legacyInputAvailable = false;
            return Vector2.zero;
        }
    }

    private void RotateTowardsMovement(Vector3 planarVelocity)
    {
        planarVelocity.y = 0f;
        if (planarVelocity.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up)
                                    * Quaternion.Euler(0f, facingAngleOffset, 0f);
        Quaternion nextRotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        _rb.MoveRotation(nextRotation);
    }

    private void RefreshArenaBounds()
    {
        RefreshArenaBounds(forceRadiusRefresh: false);
    }

    private void RefreshArenaBounds(bool forceRadiusRefresh)
    {
        if (arenaCenterOverride != null)
        {
            _resolvedArenaTransform = arenaCenterOverride;
        }
        else if (_resolvedArenaTransform == null || !_resolvedArenaTransform.gameObject.scene.IsValid())
        {
            _resolvedArenaTransform = AgilitySceneUtility.FindArenaRootTransform();
        }

        _arenaCenter = _resolvedArenaTransform != null ? _resolvedArenaTransform.position : Vector3.zero;

        if (arenaRadiusOverride > 0f)
        {
            _arenaRadius = arenaRadiusOverride;
            _cachedArenaRadiusSource = null;
            _lastArenaRadiusOverride = arenaRadiusOverride;
            return;
        }

        bool overrideChanged = !Mathf.Approximately(_lastArenaRadiusOverride, arenaRadiusOverride);
        bool sourceChanged = _cachedArenaRadiusSource != _resolvedArenaTransform;
        if (forceRadiusRefresh || sourceChanged || overrideChanged)
        {
            _arenaRadius = _resolvedArenaTransform != null
                ? AgilitySceneUtility.ResolveArenaRadius(_resolvedArenaTransform)
                : AgilitySceneUtility.ResolveArenaRadius();

            _cachedArenaRadiusSource = _resolvedArenaTransform;
            _lastArenaRadiusOverride = arenaRadiusOverride;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = arenaCenterOverride != null
            ? arenaCenterOverride.position
            : AgilitySceneUtility.ResolveArenaCenter();

        float radius = arenaRadiusOverride > 0f
            ? arenaRadiusOverride
            : AgilitySceneUtility.ResolveArenaRadius(arenaCenterOverride);

        if (radius <= 0.01f)
            return;

        Gizmos.color = new Color(1f, 0.67f, 0.12f, 0.9f);
        DrawWireCircle(center, radius);

        if (arenaPadding > 0f && radius - arenaPadding > 0.01f)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.12f, 0.9f);
            DrawWireCircle(center, radius - arenaPadding);
        }
    }

    private static void DrawWireCircle(Vector3 center, float radius, int segments = 48)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }

    public void AddExternalVelocity(Vector3 deltaVelocity)
    {
        deltaVelocity.y = 0f;
        _externalVelocity += deltaVelocity;
        _externalVelocity = Vector3.ClampMagnitude(_externalVelocity, maxExternalVelocity);
    }

    public void SetRuntimeTuning(float speedMultiplier, float accelerationMultiplier = 1f, float decelerationMultiplier = 1f)
    {
        runtimeSpeedMultiplier = Mathf.Max(0.1f, speedMultiplier);
        runtimeAccelerationMultiplier = Mathf.Max(0.1f, accelerationMultiplier);
        runtimeDecelerationMultiplier = Mathf.Max(0.1f, decelerationMultiplier);
    }

    public void ForcePublishDashCooldown()
    {
        PublishDashCooldown(force: true);
    }

    private void UpdateMouseInput()
    {
        if (!enableMouseControl)
        {
            _mouseHoldActive = false;
            _hasHeldMouseWorldPoint = false;
            _hasClickMoveTarget = false;
            return;
        }

        bool pressedThisFrame = WasPrimaryPointerPressedThisFrame();
        bool releasedThisFrame = WasPrimaryPointerReleasedThisFrame();
        bool isPressed = IsPrimaryPointerPressed();
        bool secondaryPressedThisFrame = WasSecondaryPointerPressedThisFrame();

        if (secondaryPressedThisFrame && !IsPointerOverUi())
            TryDashToPointer();

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
            UpdateHeldMouseWorldPoint();

        if (_mouseHoldActive && releasedThisFrame)
        {
            Vector2 releaseScreenPosition = _mousePressScreenPosition;
            TryReadPointerPosition(out releaseScreenPosition);

            bool isClick =
                Time.unscaledTime - _mousePressStartedAt <= clickMoveHoldThreshold &&
                (releaseScreenPosition - _mousePressScreenPosition).sqrMagnitude <=
                clickMoveDragThreshold * clickMoveDragThreshold;

            if (isClick && _hasHeldMouseWorldPoint)
                HandleMouseClick(_heldMouseWorldPoint);

            _mouseHoldActive = false;
            _hasHeldMouseWorldPoint = false;
        }
        else if (_mouseHoldActive && !isPressed)
        {
            _mouseHoldActive = false;
            _hasHeldMouseWorldPoint = false;
        }
    }

    private void HandleMouseClick(Vector3 worldPoint)
    {
        worldPoint = ClampPointToArena(worldPoint);
        _clickMoveTarget = worldPoint;
        _hasClickMoveTarget = true;
    }

    private void UpdateDashInput()
    {
        if (!enableDash || !WasDashPressedThisFrame())
            return;

        TryStartDash(ResolveDashDirection());
    }

    private bool TryStartDash(Vector3 direction)
    {
        if (!enableDash || Time.time < _dashCooldownEndTime)
            return false;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        _dashDirection = direction.normalized;
        _lastMoveDirection = _dashDirection;
        _dashEndTime = Time.time + dashDuration;
        _dashCooldownEndTime = Time.time + dashCooldown;
        PublishDashCooldown(force: true);
        return true;
    }

    private Vector3 ResolveDashDirection()
    {
        Vector3 direction = _input;
        if (direction.sqrMagnitude > 0.0001f)
            return direction;

        if (_mouseHoldActive && _hasHeldMouseWorldPoint)
            direction = _heldMouseWorldPoint - transform.position;
        else if (_hasClickMoveTarget)
            direction = _clickMoveTarget - transform.position;
        else
            direction = _lastMoveDirection.sqrMagnitude > 0.0001f ? _lastMoveDirection : transform.forward;

        direction.y = 0f;
        return direction;
    }

    private Vector3 GetDashVelocity()
    {
        if (!IsDashActive())
            return Vector3.zero;

        float remaining = Mathf.Max(0f, _dashEndTime - Time.time);
        float normalized = dashDuration > 0.0001f ? remaining / dashDuration : 0f;
        return _dashDirection * (dashSpeed * Mathf.Clamp01(normalized));
    }

    private bool IsDashActive()
    {
        return Time.time < _dashEndTime;
    }

    private void TryDashToPointer()
    {
        if (TryGetPointerWorldPoint(out Vector3 worldPoint))
            TryStartDash(worldPoint - transform.position);
    }

    private bool TryGetHeldMouseMoveDirection(out Vector3 moveDirection)
    {
        moveDirection = Vector3.zero;

        if (!_mouseHoldActive || !_hasHeldMouseWorldPoint)
            return false;

        moveDirection = _heldMouseWorldPoint - transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= mouseStopDistance * mouseStopDistance)
        {
            moveDirection = Vector3.zero;
            return false;
        }

        return true;
    }

    private bool TryGetClickTargetMoveDirection(out Vector3 moveDirection)
    {
        moveDirection = Vector3.zero;

        if (!_hasClickMoveTarget)
            return false;

        moveDirection = _clickMoveTarget - transform.position;
        moveDirection.y = 0f;

        if (moveDirection.sqrMagnitude <= mouseStopDistance * mouseStopDistance)
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
            _heldMouseWorldPoint = ClampPointToArena(worldPoint);
            _hasHeldMouseWorldPoint = true;
        }
    }

    private bool TryGetPointerWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;

        Camera movementCamera = GetMovementCamera();
        if (movementCamera == null || !TryReadPointerPosition(out Vector2 pointerPosition))
            return false;

        Ray pointerRay = movementCamera.ScreenPointToRay(pointerPosition);
        Plane movementPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        if (!movementPlane.Raycast(pointerRay, out float enter))
            return false;

        worldPoint = pointerRay.GetPoint(enter);
        worldPoint.y = transform.position.y;
        return true;
    }

    private Vector3 ClampPointToArena(Vector3 point)
    {
        if (!clampToArena)
            return point;

        RefreshArenaBounds();
        return AgilitySceneUtility.ClampToArena(point, _arenaCenter, _arenaRadius, arenaPadding);
    }

    private void RefreshMovementCamera()
    {
        if (_movementCamera != null)
            return;

        if (movementCameraTransform != null)
            _movementCamera = movementCameraTransform.GetComponent<Camera>();

        _movementCamera ??= Camera.main;
        if (_movementCamera == null)
            _movementCamera = AgilitySceneUtility.FindInLoadedScene<Camera>();
    }

    private Camera GetMovementCamera()
    {
        if (_movementCamera == null)
            RefreshMovementCamera();

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
            return mouse.leftButton.isPressed;

        Pointer pointer = Pointer.current;
        if (pointer != null)
            return pointer.press.isPressed;
#endif
        return Input.GetMouseButton(0);
    }

    private static bool WasPrimaryPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasPressedThisFrame;

        Pointer pointer = Pointer.current;
        if (pointer != null)
            return pointer.press.wasPressedThisFrame;
#endif
        return Input.GetMouseButtonDown(0);
    }

    private static bool WasPrimaryPointerReleasedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.leftButton.wasReleasedThisFrame;

        Pointer pointer = Pointer.current;
        if (pointer != null)
            return pointer.press.wasReleasedThisFrame;
#endif
        return Input.GetMouseButtonUp(0);
    }

    private static bool WasSecondaryPointerPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
            return mouse.rightButton.wasPressedThisFrame;
#endif
        return Input.GetMouseButtonDown(1);
    }

    private static bool WasDashPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            return true;
#endif
        return Input.GetKeyDown(KeyCode.Space);
    }

    private void PublishDashCooldown(bool force = false)
    {
        float remaining = DashCooldownRemaining;
        bool isReady = remaining <= 0f;

        if (!force)
        {
            bool unchanged = Mathf.Abs(remaining - _lastPublishedDashRemaining) <= 0.005f;
            if (unchanged && isReady == _lastPublishedDashReady)
                return;
        }

        _lastPublishedDashRemaining = remaining;
        _lastPublishedDashReady = isReady;
        OnDashCooldownChanged?.Invoke(remaining, DashCooldownDuration, isReady);
    }
}
