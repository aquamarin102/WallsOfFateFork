using Game;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(AudioSource))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerMoveController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 3f;
        [SerializeField] private float runMultiplier = 1.5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = 9.81f;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private LayerMask groundMask;

        [Header("Mouse & Click Settings")]
        [SerializeField] private float stopThreshold = 0.1f;     // статичные цели
        [SerializeField] private float holdThreshold = 0.2f;     // follow курсора
        [SerializeField] private float interactionStopDistance = 1.2f;
        [SerializeField] private float mouseRaycastDistance = 500f;
        [SerializeField] private float navMeshSampleRadius = 2.5f;
        [SerializeField] private float holdRetargetDistance = 0.35f;
        [SerializeField] private float doubleClickThreshold = 0.3f;
        [SerializeField] private float doubleClickMaxScreenDistance = 35f;
        [SerializeField] private float interactionClickProbeRadius = 0.65f;

        [Header("Interaction Settings")]
        [SerializeField] private float keyboardInteractionRadius = 1.6f;

        [Header("Pitch Settings")]
        [SerializeField] private float walkingPitch = 1f;
        [SerializeField] private float runningPitch = 1.5f;

        private Vector3 grabDirection;
        private bool isBoxGrabMode = false;
        private BoxMover heldBoxMover;
        private Vector3 heldBoxPlayerOffset;

        private bool isHoldMove;
        private bool isWalkingMode;
        private bool isPathRunRequested = true;
        private float mouseDownTime;
        private float lastClickTime = -1f;
        private Vector2 lastClickPosition;
        private UnityEngine.Object lastClickInteractionTarget;
        private int lastProcessedInteractPressId;

        private Vector3 clickTarget;
        private Transform dynamicTarget;
        private float dynamicStopDist;

        private Action _onArriveAction;
        private Func<bool> _canInvokeArriveAction;

        [Header("Footstep Settings")]
        [SerializeField] private float walkingStepInterval = 0.48f;
        [SerializeField] private float runningStepInterval = 0.32f;
        [SerializeField] private float footstepMinSpeed = 0.15f;
        private readonly Dictionary<string, List<AudioClip>> sceneFootstepSounds = new();
        private AudioClip leftClip, rightClip;


        private CharacterController characterController;
        private NavMeshAgent agent;
        private AudioSource footstepSource;
        private NavMeshPath navMeshPath;

        private Vector3 moveDirection;
        private float verticalVelocity;
        private bool isLeftFoot = true;
        private float currentPlanarSpeed;
        private bool isRunning;
        private float footstepTimer;

        private PlayerAnimationController interactManager;   // ссылка на менеджер взаимодействия

        // --------------------------------------------------
        [Inject] private void Construct(Transform camTransform) => cameraTransform = camTransform;

        private void Awake()
        {
            interactManager = GetComponent<PlayerAnimationController>();
            //if (!interactManager) Debug.LogError("PlayerMoveController: InteractManager missing!");
        }

        private void Start()
        {
            characterController = GetComponent<CharacterController>();
            agent = GetComponent<NavMeshAgent>();
            footstepSource = GetComponent<AudioSource>();
            navMeshPath = new NavMeshPath();

            agent.updatePosition = false;     // Agent используется только как path‑finder
            agent.updateRotation = false;
            agent.stoppingDistance = stopThreshold;

            // пример инициализации звуков
            sceneFootstepSounds.Add("MainRoom", new()
        {
            Resources.Load<AudioClip>("Footsteps/wood1"),
            Resources.Load<AudioClip>("Footsteps/wood2")
        });
            sceneFootstepSounds.Add("Forge", new()
        {
            Resources.Load<AudioClip>("Footsteps/gravel1"),
            Resources.Load<AudioClip>("Footsteps/gravel2")
        });
            sceneFootstepSounds.Add("Storage", new()
        {
            Resources.Load<AudioClip>("Footsteps/stone1"),
            Resources.Load<AudioClip>("Footsteps/stone2")
        });

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateFootstepSounds(SceneManager.GetActiveScene().name);
        }

        public void InBoxGrabMode(BoxMover boxMover, Vector3 axis, Vector3 playerOffset)
        {
            isBoxGrabMode = true;
            grabDirection = axis.normalized;
            heldBoxMover = boxMover;
            heldBoxPlayerOffset = new Vector3(playerOffset.x, 0f, playerOffset.z);
            StopMovement();
            AlignToHeldBoxFace(true);
        }

        public void StopBoxGrabMode()
        {
            isBoxGrabMode = false;
            heldBoxMover = null;
            heldBoxPlayerOffset = Vector3.zero;
        }

        private void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

        public float CurrentPlanarSpeed => currentPlanarSpeed;
        public bool IsRunning => isRunning;
        public float InteractionRadius => keyboardInteractionRadius;

        private void Update()
        {
            HandleMovementModeInput();
            HandleInteractionInput();
            HandleMouseInput();
            HandleMovement();
            UpdateFootstep();
        }

        private void LateUpdate()
        {
            ApplyHeldBoxCorrection();
        }

        // ==================================================
        #region Mouse Input
        private void HandleMouseInput()
        {

            bool pointerDown = IsPrimaryPointerPressedThisFrame();
            bool pointerHeld = IsPrimaryPointerPressed();
            bool pointerUp = IsPrimaryPointerReleasedThisFrame();

            if (DialogManager.Instance.Active == true) return;

            if (IsPointerOverUi())
            {
                if (pointerUp)
                    isHoldMove = false;
                return;
            }

            if (isBoxGrabMode)
            {
                if (pointerDown)
                    mouseDownTime = Time.time;

                if (pointerUp)
                {
                    float held = Time.time - mouseDownTime;
                    if (held < holdThreshold)
                        ProcessClick();

                    isHoldMove = false;
                }

                return;
            }

            if (pointerDown)
            {
                mouseDownTime = Time.time;
            }

            if (pointerHeld && !isHoldMove && Time.time - mouseDownTime >= holdThreshold)
                isHoldMove = true;

            if (pointerUp)
            {
                float held = Time.time - mouseDownTime;
                dynamicTarget = null;                     // сброс преследования

                if (held < holdThreshold) ProcessClick();
                isHoldMove = false;
            }
        }

        private void ProcessClick()
        {
            if (!TryCreatePointerRay(out Ray ray))
                return;

            TryReadPointerPosition(out Vector2 clickPosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, mouseRaycastDistance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool hasInteractionTarget = TryResolveInteractionTarget(hits, out InteractionTarget interactionTarget);
            if (!isBoxGrabMode &&
                (!hasInteractionTarget || interactionTarget.Kind != InteractionTargetKind.Item) &&
                TryResolveItemTargetNearPointer(ray, hits, out InteractionTarget pointerItemTarget))
            {
                interactionTarget = pointerItemTarget;
                hasInteractionTarget = true;
            }

            bool isInteractionDoubleClick = hasInteractionTarget &&
                IsInteractionDoubleClick(interactionTarget.Identity, clickPosition);

            RegisterClick(hasInteractionTarget ? interactionTarget.Identity : null, clickPosition);

            if (isInteractionDoubleClick)
            {
                if (isBoxGrabMode)
                    interactionTarget.Invoke();
                else
                    MoveToInteractionTarget(interactionTarget);
                return;
            }

            if (isBoxGrabMode)
            {
                if (TryResolveMovementDestination(ray, hits, out Vector3 boxDestination))
                    MoveToAndCallback(boxDestination, IsRunModeActive(), null);

                return;
            }

            if (TryResolveMovementDestination(ray, hits, out Vector3 destination))
                MoveToAndCallback(destination, IsRunModeActive(), null);
        }

        private bool TryResolveMovementDestination(Ray ray, out Vector3 destination)
        {
            if (TryGetMovementPlanePoint(ray, out Vector3 planePoint) &&
                TryGetPathableDestination(planePoint, out destination))
            {
                return true;
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, mouseRaycastDistance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return TryResolveMovementDestination(hits, out destination);
        }

        private bool TryResolveMovementDestination(Ray ray, RaycastHit[] hits, out Vector3 destination)
        {
            if (TryGetMovementPlanePoint(ray, out Vector3 planePoint) &&
                TryGetPathableDestination(planePoint, out destination))
            {
                return true;
            }

            return TryResolveMovementDestination(hits, out destination);
        }

        private bool TryResolveMovementDestination(RaycastHit[] hits, out Vector3 destination)
        {
            if (TryResolveMovementDestinationFromHits(hits, true, out destination))
                return true;

            return TryResolveMovementDestinationFromHits(hits, false, out destination);
        }

        private bool TryResolveMovementDestinationFromHits(RaycastHit[] hits, bool groundOnly, out Vector3 destination)
        {
            foreach (RaycastHit hit in hits)
            {
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;

                bool isGroundLayer = ((1 << hitCollider.gameObject.layer) & groundMask) != 0;
                if (groundOnly != isGroundLayer)
                    continue;

                if (TryGetPathableDestination(hit.point, out destination))
                    return true;
            }

            destination = default;
            return false;
        }

        private bool TryGetMovementPlanePoint(Ray ray, out Vector3 point)
        {
            Plane movementPlane = new(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (movementPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private bool TryGetPathableDestination(Vector3 targetPoint, out Vector3 destination)
        {
            destination = default;

            if (!NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
                return false;

            if (!agent.CalculatePath(navHit.position, navMeshPath) || navMeshPath.status == NavMeshPathStatus.PathInvalid)
                return false;

            if (navMeshPath.status == NavMeshPathStatus.PathComplete)
            {
                destination = navHit.position;
                return true;
            }

            if (!TryGetPartialPathDestination(navMeshPath, out destination))
                return false;

            return true;
        }

        private bool TryGetPartialPathDestination(NavMeshPath path, out Vector3 destination)
        {
            Vector3[] corners = path.corners;
            for (int i = corners.Length - 1; i >= 0; i--)
            {
                Vector3 corner = corners[i];
                corner.y = transform.position.y;
                if ((corner - transform.position).sqrMagnitude <= stopThreshold * stopThreshold)
                    continue;

                if (NavMesh.SamplePosition(corners[i], out NavMeshHit navHit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    destination = navHit.position;
                    return true;
                }
            }

            destination = default;
            return false;
        }
        #endregion

        // ==================================================
        #region Movement Core
        private void HandleMovement()
        {
            Vector3 positionBeforeMove = transform.position;

            if (DialogManager.Instance.Active == true)
            {
                moveDirection = new Vector3(0, moveDirection.y, 0);
                characterController.Move(moveDirection * Time.deltaTime);
                agent.isStopped = true;
                currentPlanarSpeed = 0f;
                isRunning = false;
                return;
            }

            // ---- WASD breaks mouse modes ----
            Vector2 moveAxes = ReadMoveInput();
            float h = moveAxes.x;
            float v = moveAxes.y;
            Vector3 input = new(h, 0, v);
            if (input.sqrMagnitude > 0.01f)
            {
                isHoldMove = false; ClearDynamic();
                agent.isStopped = true; agent.ResetPath();
            }

            Vector3 desired = Vector3.zero;
            bool usesNavigationPath = false;
            bool arrivedByInteractionRadius = TryInvokeArriveActionByPredicate();

            // A) Преследование динамической цели
            if (arrivedByInteractionRadius)
            {
                desired = Vector3.zero;
            }
            else if (dynamicTarget)
            {
                usesNavigationPath = true;
                if (TryInvokeArriveActionByPredicate())
                {
                    desired = Vector3.zero;
                }
                else if (!dynamicTarget.gameObject.activeInHierarchy) StopMovement();
                else
                {
                    if (agent.destination != dynamicTarget.position)
                        agent.SetDestination(dynamicTarget.position);
                    desired = agent.desiredVelocity.WithY(0).normalized;

                    if (!agent.pathPending && agent.remainingDistance <= dynamicStopDist + 0.05f)
                    {
                        InvokeArriveAction();
                    }
                }
            }
            // B) Follow‑режим (удержание)
            else if (isHoldMove && !IsPointerOverUi())
            {
                usesNavigationPath = true;
                isPathRunRequested = IsRunModeActive();

                if (TryCreatePointerRay(out Ray ray) &&
                    TryResolveMovementDestination(ray, out Vector3 destination))
                {
                    bool shouldRetarget = !agent.hasPath ||
                        agent.isStopped ||
                        (clickTarget - destination).sqrMagnitude > holdRetargetDistance * holdRetargetDistance;

                    if (shouldRetarget)
                    {
                        clickTarget = destination;
                        agent.SetDestination(clickTarget);
                        agent.isStopped = false;
                    }
                }

                if (agent.hasPath && !agent.isStopped)
                    desired = agent.desiredVelocity.WithY(0).normalized;
            }
            // C) Click‑to‑point
            else if (agent.hasPath && !agent.isStopped)
            {
                usesNavigationPath = true;
                if (TryInvokeArriveActionByPredicate())
                {
                    desired = Vector3.zero;
                }
                else
                {
                    desired = agent.desiredVelocity.WithY(0).normalized;
                    if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    {
                        InvokeArriveAction();
                    }
                }
            }
            // D) WASD прямое движение
            else if (input.sqrMagnitude > 0.01f)
            {
                Vector3 f = cameraTransform.forward; f.y = 0; f.Normalize();
                Vector3 r = cameraTransform.right; r.y = 0; r.Normalize();
                if (isBoxGrabMode)
                {
                    Vector3 moveInput = (f * v + r * h).normalized;
                    if (moveInput.sqrMagnitude < 0.01f)
                    {
                        desired = Vector3.zero;
                    }
                    else
                    {
                        // Проверяем, насколько ввод близок к оси grabAxis или противоположной
                        float angle = Vector3.Angle(moveInput, grabDirection);
                        float oppositeAngle = Vector3.Angle(moveInput, -grabDirection);
                        float minAngle = Mathf.Min(angle, oppositeAngle);

                        if (minAngle < 10f) // порог 10 градусов
                        {
                            // Определяем направление (вперёд или назад)
                            float sign = (angle < oppositeAngle) ? 1f : -1f;
                            desired = grabDirection * sign * moveInput.magnitude;
                        }
                        else
                        {
                            desired = Vector3.zero;
                        }
                    }
                }
                else
                {
                    desired = (f * v + r * h).normalized;
                }
            }

            // Поворот
            if (desired != Vector3.zero)
                if (!isBoxGrabMode) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(desired), rotationSpeed * Time.deltaTime);

            // Скорость + гравитация
            bool hasPlanarInput = desired.sqrMagnitude > 0.0001f;
            bool running = !isBoxGrabMode && hasPlanarInput &&
                (usesNavigationPath ? isPathRunRequested : IsRunModeActive());
            float speed = moveSpeed * (running ? runMultiplier : 1f);
            Vector3 planarMoveDirection = desired * speed;

            if (isBoxGrabMode && heldBoxMover != null)
            {
                heldBoxMover.SetDesiredPlanarVelocity(planarMoveDirection);
                moveDirection = Vector3.zero;
            }
            else
            {
                moveDirection = planarMoveDirection;
            }

            verticalVelocity = characterController.isGrounded ? -1f : verticalVelocity - gravity * Time.deltaTime;
            moveDirection.y = verticalVelocity;

            characterController.Move(moveDirection * Time.deltaTime);
            agent.nextPosition = transform.position;

            if (isBoxGrabMode && heldBoxMover != null)
            {
                currentPlanarSpeed = heldBoxMover.CurrentPlanarSpeed;
                isRunning = false;
            }
            else
            {
                Vector3 actualDelta = transform.position - positionBeforeMove;
                actualDelta.y = 0f;
                currentPlanarSpeed = actualDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                isRunning = running && currentPlanarSpeed > 0.01f;
            }
        }
        #endregion

        // ==================================================
        #region Public API
        public void MoveToAndCallback(Vector3 target, bool run, Action onArrive, float stopDistance = 0.1f, Func<bool> canInvokeArriveAction = null)
        {
            dynamicTarget = null; clickTarget = target; isPathRunRequested = run; isHoldMove = false;
            agent.stoppingDistance = stopDistance; agent.SetDestination(target); agent.isStopped = false;
            _onArriveAction = onArrive;
            _canInvokeArriveAction = canInvokeArriveAction;
        }

        public void MoveToAndCallback(Transform target, bool run, Action onArrive, float stopDistance = 1f, Func<bool> canInvokeArriveAction = null)
        {
            dynamicTarget = target; clickTarget = target.position; dynamicStopDist = stopDistance; isPathRunRequested = run; isHoldMove = false;
            agent.stoppingDistance = stopDistance; agent.SetDestination(target.position); agent.isStopped = false;
            _onArriveAction = onArrive;
            _canInvokeArriveAction = canInvokeArriveAction;
        }

        public void StopMovement()
        {
            agent.isStopped = true; agent.ResetPath(); isHoldMove = false; clickTarget = Vector3.zero; ClearDynamic();
            currentPlanarSpeed = 0f;
            isRunning = false;
        }
        #endregion

        // ==================================================
        #region Helpers
        private void ClearDynamic()
        {
            dynamicTarget = null;
            _onArriveAction = null;
            _canInvokeArriveAction = null;
        }

        private bool TryInvokeArriveActionByPredicate()
        {
            return _onArriveAction != null &&
                   _canInvokeArriveAction != null &&
                   _canInvokeArriveAction() &&
                   InvokeArriveAction();
        }

        private bool InvokeArriveAction()
        {
            agent.isStopped = true;
            agent.ResetPath();

            Action callback = _onArriveAction;
            ClearDynamic();
            callback?.Invoke();
            return true;
        }

        private void ApplyHeldBoxCorrection()
        {
            if (!isBoxGrabMode || heldBoxMover == null)
                return;

            Vector3 targetPlayerPosition = heldBoxMover.Position + heldBoxPlayerOffset;
            Vector3 currentPosition = transform.position;
            Vector3 correction = new Vector3(
                targetPlayerPosition.x - currentPosition.x,
                0f,
                targetPlayerPosition.z - currentPosition.z);

            if (correction.sqrMagnitude > 0.000001f)
                characterController.Move(correction);

            AlignToHeldBoxFace(false);
        }

        private void AlignToHeldBoxFace(bool instant)
        {
            if (!isBoxGrabMode)
                return;

            Vector3 lookDirection = -heldBoxPlayerOffset;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized);
            transform.rotation = instant
                ? targetRotation
                : Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void HandleMovementModeInput()
        {
            if (DialogManager.Instance.Active == true)
                return;

            if (WasWalkTogglePressedThisFrame())
            {
                isWalkingMode = !isWalkingMode;
                isPathRunRequested = IsRunModeActive();
            }
        }

        private bool IsRunModeActive()
        {
            return !isWalkingMode;
        }

        private void HandleInteractionInput()
        {
            if (DialogManager.Instance.Active == true)
                return;

            if (!ConsumeInteractInput())
                return;

            if (TryFindBestInteractionTargetAroundPlayer(out InteractionTarget interactionTarget))
            {
                interactionTarget.Invoke();
                return;
            }

            if (heldBoxMover != null)
                ToggleBoxInteraction(heldBoxMover, heldBoxMover.gameObject);
        }

        private bool ConsumeInteractInput()
        {
            global::InputManager inputManager = global::InputManager.GetInstance();
            if (inputManager != null && inputManager.TryConsumeInteractPress(ref lastProcessedInteractPressId))
                return true;

            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        private static Vector2 ReadMoveInput()
        {
            Vector2 move = Vector2.zero;
            global::InputManager inputManager = global::InputManager.GetInstance();
            if (inputManager != null)
            {
                Vector3 sharedMove = inputManager.GetMoveDirection();
                move = new Vector2(sharedMove.x, sharedMove.y);
                if (move.sqrMagnitude > 0.0001f)
                    return Vector2.ClampMagnitude(move, 1f);
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                move = gamepad.leftStick.ReadValue();
                if (move.sqrMagnitude > 0.0001f)
                    return Vector2.ClampMagnitude(move, 1f);
            }

            Keyboard keyboard = Keyboard.current;
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

        private static bool WasWalkTogglePressedThisFrame()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null &&
                   (keyboard.leftCtrlKey.wasPressedThisFrame || keyboard.rightCtrlKey.wasPressedThisFrame);
        }

        private bool TryCreatePointerRay(out Ray ray)
        {
            ray = default;

            Camera mainCamera = Camera.main;
            if (mainCamera == null || !TryReadPointerPosition(out Vector2 pointerPosition))
                return false;

            ray = mainCamera.ScreenPointToRay(pointerPosition);
            return true;
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
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

            position = default;
            return false;
        }

        private static bool IsPrimaryPointerPressedThisFrame()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                return mouse.leftButton.wasPressedThisFrame;

            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasPressedThisFrame;
        }

        private static bool IsPrimaryPointerReleasedThisFrame()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                return mouse.leftButton.wasReleasedThisFrame;

            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.wasReleasedThisFrame;
        }

        private static bool IsPrimaryPointerPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                return mouse.leftButton.isPressed;

            Pointer pointer = Pointer.current;
            return pointer != null && pointer.press.isPressed;
        }

        private bool TryFindBestInteractionTargetAroundPlayer(out InteractionTarget interactionTarget)
        {
            return TryFindBestInteractionTargetAroundPlayer(null, out interactionTarget);
        }

        private bool TryFindBestInteractionTargetAroundPlayer(Func<InteractionTarget, bool> canUseTarget, out InteractionTarget interactionTarget)
        {
            interactionTarget = default;
            float bestScore = float.PositiveInfinity;

            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                keyboardInteractionRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider candidateCollider in colliders)
            {
                if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
                    continue;

                Vector3 hitPoint = candidateCollider.ClosestPoint(transform.position);
                if (!TryResolveInteractionTarget(candidateCollider, hitPoint, out InteractionTarget candidate))
                    continue;

                if (canUseTarget != null && !canUseTarget(candidate))
                    continue;

                float score = candidate.GetScore(transform.position);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                interactionTarget = candidate;
            }

            return interactionTarget.IsValid;
        }

        private bool TryResolveInteractionTarget(RaycastHit[] hits, out InteractionTarget interactionTarget)
        {
            interactionTarget = default;
            float bestScore = float.PositiveInfinity;

            foreach (RaycastHit hit in hits)
            {
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;

                if (!TryResolveInteractionTarget(hitCollider, hit.point, out InteractionTarget candidate))
                    continue;

                float score = candidate.GetScore(hit.point);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                interactionTarget = candidate;
            }

            return interactionTarget.IsValid;
        }

        private bool TryResolveItemTargetNearPointer(Ray ray, RaycastHit[] hits, out InteractionTarget interactionTarget)
        {
            interactionTarget = default;
            float bestScore = float.PositiveInfinity;
            float probeRadius = Mathf.Max(0.01f, interactionClickProbeRadius);

            RaycastHit[] probeHits = Physics.SphereCastAll(
                ray,
                probeRadius,
                mouseRaycastDistance,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(probeHits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in probeHits)
            {
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;

                TryUsePointerItemTarget(
                    hitCollider,
                    hit.point,
                    hit.point,
                    hit.distance * 0.01f,
                    ref bestScore,
                    ref interactionTarget);
            }

            foreach (RaycastHit hit in hits)
            {
                Collider hitCollider = hit.collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                    continue;

                Collider[] colliders = Physics.OverlapSphere(
                    hit.point,
                    probeRadius,
                    ~0,
                    QueryTriggerInteraction.Collide);

                foreach (Collider candidateCollider in colliders)
                {
                    if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
                        continue;

                    Vector3 candidatePoint = candidateCollider.ClosestPoint(hit.point);
                    float distanceFromPointerHit = (candidatePoint - hit.point).sqrMagnitude;
                    TryUsePointerItemTarget(
                        candidateCollider,
                        candidatePoint,
                        hit.point,
                        hit.distance * 0.01f + distanceFromPointerHit * 10f,
                        ref bestScore,
                        ref interactionTarget);
                }
            }

            return interactionTarget.IsValid;
        }

        private bool TryUsePointerItemTarget(
            Collider candidateCollider,
            Vector3 hitPoint,
            Vector3 scoreOrigin,
            float distanceBias,
            ref float bestScore,
            ref InteractionTarget interactionTarget)
        {
            if (!TryResolveInteractionTarget(candidateCollider, hitPoint, out InteractionTarget candidate))
                return false;

            if (candidate.Kind != InteractionTargetKind.Item)
                return false;

            float score = distanceBias + candidate.GetScore(scoreOrigin);
            if (score >= bestScore)
                return false;

            bestScore = score;
            interactionTarget = candidate;
            return true;
        }

        private bool TryResolveInteractionTarget(Collider hitCollider, Vector3 hitPoint, out InteractionTarget interactionTarget)
        {
            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out InteractableItemInfluenceArea itemArea))
            {
                interactionTarget = CreateInteractionTarget(
                    itemArea,
                    ResolveInteractionTransform(itemArea),
                    itemArea.GetComponent<Collider>(),
                    () => _ = itemArea.InvokeDirectInteractionAsync(gameObject),
                    InteractionTargetKind.Item,
                    true,
                    true);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out DoorInfluenceArea doorArea))
            {
                interactionTarget = CreateInteractionTarget(
                    doorArea,
                    ResolveInteractionTransform(doorArea),
                    doorArea.GetComponent<Collider>(),
                    () => _ = doorArea.InvokeDirectInteractionAsync(gameObject),
                    InteractionTargetKind.Generic);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out InfluenceArea influenceArea))
            {
                interactionTarget = CreateInteractionTarget(
                    influenceArea,
                    ResolveInteractionTransform(influenceArea),
                    influenceArea.GetComponent<Collider>(),
                    () => _ = influenceArea.InvokeDirectInteractionAsync(gameObject),
                    InteractionTargetKind.Generic);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out StartDayDialogTriggerZone startDayDialogueZone))
            {
                interactionTarget = CreateInteractionTarget(
                    startDayDialogueZone,
                    ResolveInteractionTransform(startDayDialogueZone),
                    startDayDialogueZone.GetComponent<Collider>(),
                    () => startDayDialogueZone.InvokeDirectInteraction(gameObject),
                    InteractionTargetKind.Generic);
                return true;
            }

            if (TryFindComponentOnClickedObject(hitCollider, out InteractableItem interactableItem))
            {
                interactionTarget = new InteractionTarget(
                    interactableItem,
                    interactableItem.transform,
                    interactableItem.GetComponent<Collider>(),
                    interactableItem.ApproachDistance,
                    interactableItem.Interact,
                    InteractionTargetKind.Item,
                    true,
                    true);
                return true;
            }

            if (TryFindComponentOnClickedObject(hitCollider, out BoxMover boxMover))
            {
                GameObject boxObject = boxMover.gameObject;
                interactionTarget = CreateInteractionTarget(
                    boxMover,
                    boxMover.transform,
                    boxMover.GetComponent<Collider>(),
                    () => ToggleBoxInteraction(boxMover, boxObject),
                    InteractionTargetKind.Box,
                    false);
                return true;
            }

            if (TryFindComponentOnClickedObject(hitCollider, out PlayChestAnimation chestAnimation))
            {
                GameObject chestObject = chestAnimation.gameObject;
                interactionTarget = CreateInteractionTarget(
                    chestAnimation,
                    chestAnimation.transform,
                    chestAnimation.GetComponent<Collider>(),
                    () => chestAnimation.Triggered(new TriggerEvent(
                        InfluenceType.Object,
                        gameObject,
                        chestObject,
                        true,
                        string.Empty)),
                    InteractionTargetKind.Generic);
                return true;
            }

            interactionTarget = default;
            return false;
        }

        private InteractionTarget CreateInteractionTarget(
            UnityEngine.Object identity,
            Transform target,
            Collider interactionCollider,
            Action invoke,
            InteractionTargetKind kind,
            bool canInvokeFromInteractionRadius = true,
            bool usePlanarColliderDistance = false)
        {
            return new InteractionTarget(
                identity,
                target != null ? target : transform,
                interactionCollider,
                interactionStopDistance,
                invoke,
                kind,
                canInvokeFromInteractionRadius,
                usePlanarColliderDistance);
        }

        private void MoveToInteractionTarget(InteractionTarget interactionTarget)
        {
            if (!interactionTarget.IsValid)
                return;

            Action onArrive = CreateInteractionArriveAction(interactionTarget);

            if (interactionTarget.CanInvokeFromInteractionRadius && IsInteractionTargetAvailable(interactionTarget))
            {
                StopMovement();
                onArrive.Invoke();
                return;
            }

            Func<bool> canInvokeOnApproach = interactionTarget.CanInvokeFromInteractionRadius
                ? () => CanInvokeInteractionOnApproach(interactionTarget)
                : null;

            if (TryResolveInteractionMoveDestination(interactionTarget, out Vector3 destination))
            {
                MoveToAndCallback(
                    destination,
                    IsRunModeActive(),
                    onArrive,
                    interactionTarget.StopDistance,
                    canInvokeOnApproach);
                return;
            }

            MoveToAndCallback(
                interactionTarget.MoveTarget,
                IsRunModeActive(),
                onArrive,
                interactionTarget.StopDistance,
                canInvokeOnApproach);
        }

        private Action CreateInteractionArriveAction(InteractionTarget interactionTarget)
        {
            if (interactionTarget.Kind != InteractionTargetKind.Item)
                return interactionTarget.Invoke;

            return () =>
            {
                if (TryFindBestInteractionTargetAroundPlayer(
                        candidate => candidate.Matches(interactionTarget),
                        out InteractionTarget matchingTarget))
                {
                    matchingTarget.Invoke();
                    return;
                }

                if (TryFindBestInteractionTargetAroundPlayer(
                        candidate => candidate.Kind == InteractionTargetKind.Item,
                        out InteractionTarget nearbyItemTarget))
                {
                    nearbyItemTarget.Invoke();
                    return;
                }

                interactionTarget.Invoke();
            };
        }

        private bool CanInvokeInteractionOnApproach(InteractionTarget interactionTarget)
        {
            if (IsInteractionTargetAvailable(interactionTarget))
                return true;

            return interactionTarget.Kind == InteractionTargetKind.Item &&
                   HasReachedClickTarget() &&
                   TryFindBestInteractionTargetAroundPlayer(
                       candidate => candidate.Kind == InteractionTargetKind.Item,
                       out _);
        }

        private bool HasReachedClickTarget()
        {
            Vector3 delta = clickTarget - transform.position;
            delta.y = 0f;
            float tolerance = Mathf.Max(agent.stoppingDistance + 0.35f, stopThreshold + 0.35f);
            return delta.sqrMagnitude <= tolerance * tolerance;
        }

        private bool IsInteractionTargetAvailable(InteractionTarget interactionTarget)
        {
            if (!interactionTarget.CanInvokeFromInteractionRadius)
                return false;

            if (interactionTarget.IsPlayerInRange(transform.position, keyboardInteractionRadius))
                return true;

            Collider[] colliders = Physics.OverlapSphere(
                transform.position,
                keyboardInteractionRadius,
                ~0,
                QueryTriggerInteraction.Collide);

            foreach (Collider candidateCollider in colliders)
            {
                if (candidateCollider == null || candidateCollider.transform.IsChildOf(transform))
                    continue;

                Vector3 hitPoint = candidateCollider.ClosestPoint(transform.position);
                if (!TryResolveInteractionTarget(candidateCollider, hitPoint, out InteractionTarget candidate))
                    continue;

                if (candidate.Matches(interactionTarget))
                    return true;
            }

            return false;
        }

        private bool TryResolveInteractionMoveDestination(InteractionTarget interactionTarget, out Vector3 destination)
        {
            if (interactionTarget.TryGetClosestPoint(transform.position, out Vector3 closestPoint))
            {
                if (TryGetPathableDestination(closestPoint, out destination))
                    return true;

                closestPoint.y = transform.position.y;
                if (TryGetPathableDestination(closestPoint, out destination))
                    return true;
            }

            if (interactionTarget.MoveTarget != null)
            {
                Vector3 targetPosition = interactionTarget.MoveTarget.position;
                if (TryGetPathableDestination(targetPosition, out destination))
                    return true;

                targetPosition.y = transform.position.y;
                if (TryGetPathableDestination(targetPosition, out destination))
                    return true;
            }

            destination = default;
            return false;
        }

        private bool IsInteractionDoubleClick(UnityEngine.Object interactionTarget, Vector2 clickPosition)
        {
            if (interactionTarget == null || interactionTarget != lastClickInteractionTarget)
                return false;

            if (Time.unscaledTime - lastClickTime > doubleClickThreshold)
                return false;

            return (clickPosition - lastClickPosition).sqrMagnitude <=
                   doubleClickMaxScreenDistance * doubleClickMaxScreenDistance;
        }

        private void RegisterClick(UnityEngine.Object interactionTarget, Vector2 clickPosition)
        {
            lastClickInteractionTarget = interactionTarget;
            lastClickPosition = clickPosition;
            lastClickTime = Time.unscaledTime;
        }

        private void ToggleBoxInteraction(BoxMover boxMover, GameObject boxObject)
        {
            if (boxMover == null || boxObject == null)
                return;

            TriggerEvent eventData = new TriggerEvent(
                InfluenceType.Object,
                gameObject,
                boxObject,
                true,
                string.Empty);

            if (boxMover.IsBeingHeld)
            {
                boxMover.StopHolding();
                interactManager?.InteractWith(eventData, false);
                return;
            }

            if (heldBoxMover != null && heldBoxMover != boxMover)
                heldBoxMover.StopHolding();

            interactManager?.InteractWith(eventData, true);
            boxMover.StartHolding();
        }

        private static Transform ResolveInteractionTransform(InfluenceArea influenceArea)
        {
            return influenceArea.triggerObject != null
                ? influenceArea.triggerObject.transform
                : influenceArea.transform;
        }

        private static Transform ResolveInteractionTransform(StartDayDialogTriggerZone triggerZone)
        {
            return triggerZone.triggerObject != null
                ? triggerZone.triggerObject.transform
                : triggerZone.transform;
        }

        private static bool TryFindRelatedComponent<T>(Collider hitCollider, out T component) where T : Component
        {
            component = hitCollider.GetComponent<T>();
            if (component != null)
                return true;

            component = hitCollider.GetComponentInParent<T>();
            if (component != null)
                return true;

            component = hitCollider.GetComponentInChildren<T>(true);
            return component != null;
        }

        private static bool TryFindComponentOnClickedObject<T>(Collider hitCollider, out T component) where T : Component
        {
            component = hitCollider.GetComponent<T>();
            if (component != null)
                return true;

            component = hitCollider.GetComponentInParent<T>();
            return component != null;
        }

        private static bool TryFindClosestInteractionZone<T>(Collider hitCollider, Vector3 hitPoint, out T component) where T : Component
        {
            component = null;
            T bestCandidate = null;
            float bestScore = float.PositiveInfinity;
            HashSet<T> candidates = new();

            for (Transform current = hitCollider.transform; current != null; current = current.parent)
            {
                foreach (T candidate in current.GetComponentsInChildren<T>(true))
                    candidates.Add(candidate);
            }

            foreach (T candidate in candidates)
            {
                float score = GetInteractionCandidateScore(candidate, hitPoint);
                if (float.IsPositiveInfinity(score) || score >= bestScore)
                    continue;

                bestScore = score;
                bestCandidate = candidate;
            }

            component = bestCandidate;
            return component != null;
        }

        private static float GetInteractionCandidateScore(Component candidate, Vector3 hitPoint)
        {
            Collider candidateCollider = candidate.GetComponent<Collider>();
            float boundsDistance = candidateCollider != null
                ? candidateCollider.bounds.SqrDistance(hitPoint)
                : (candidate.transform.position - hitPoint).sqrMagnitude;

            // Отсекаем слишком далёкие зоны, чтобы клик не улетал в соседние объекты.
            if (boundsDistance > 2.25f)
                return float.PositiveInfinity;

            Transform targetTransform = candidate.transform;
            if (candidate is InfluenceArea area && area.triggerObject != null)
                targetTransform = area.triggerObject.transform;
            else if (candidate is StartDayDialogTriggerZone zone && zone.triggerObject != null)
                targetTransform = zone.triggerObject.transform;

            float targetDistance = (targetTransform.position - hitPoint).sqrMagnitude;
            return boundsDistance * 10f + targetDistance;
        }

        private enum InteractionTargetKind
        {
            Generic,
            Item,
            Box
        }

        private readonly struct InteractionTarget
        {
            public readonly UnityEngine.Object Identity;
            public readonly Transform MoveTarget;
            public readonly Collider InteractionCollider;
            public readonly float StopDistance;
            public readonly InteractionTargetKind Kind;
            public readonly bool CanInvokeFromInteractionRadius;
            private readonly bool usePlanarColliderDistance;
            private readonly Action invoke;

            public InteractionTarget(
                UnityEngine.Object identity,
                Transform moveTarget,
                Collider interactionCollider,
                float stopDistance,
                Action invoke,
                InteractionTargetKind kind,
                bool canInvokeFromInteractionRadius = true,
                bool usePlanarColliderDistance = false)
            {
                Identity = identity;
                MoveTarget = moveTarget;
                InteractionCollider = interactionCollider;
                StopDistance = stopDistance;
                Kind = kind;
                CanInvokeFromInteractionRadius = canInvokeFromInteractionRadius;
                this.usePlanarColliderDistance = usePlanarColliderDistance;
                this.invoke = invoke;
            }

            public bool IsValid => Identity != null && MoveTarget != null && invoke != null;

            public void Invoke()
            {
                if (IsValid)
                    invoke();
            }

            public float GetScore(Vector3 origin)
            {
                if (TryGetClosestPoint(origin, out Vector3 closestPoint))
                {
                    if (usePlanarColliderDistance)
                        closestPoint.y = origin.y;

                    return (closestPoint - origin).sqrMagnitude;
                }

                if (MoveTarget == null)
                    return float.PositiveInfinity;

                Vector3 delta = MoveTarget.position - origin;
                delta.y = 0f;
                return delta.sqrMagnitude;
            }

            public bool IsPlayerInRange(Vector3 playerPosition, float interactionRadius)
            {
                float radius = Mathf.Max(StopDistance, interactionRadius);
                return GetScore(playerPosition) <= radius * radius;
            }

            public bool Matches(InteractionTarget other)
            {
                if (Identity != null && Identity == other.Identity)
                    return true;

                if (MoveTarget != null && MoveTarget == other.MoveTarget)
                    return true;

                if (InteractionCollider != null && InteractionCollider == other.InteractionCollider)
                    return true;

                GameObject gameObject = GetIdentityGameObject(Identity);
                GameObject otherGameObject = GetIdentityGameObject(other.Identity);
                return gameObject != null && gameObject == otherGameObject;
            }

            public bool TryGetClosestPoint(Vector3 origin, out Vector3 closestPoint)
            {
                if (InteractionCollider == null ||
                    !InteractionCollider.enabled ||
                    !InteractionCollider.gameObject.activeInHierarchy)
                {
                    closestPoint = default;
                    return false;
                }

                closestPoint = InteractionCollider.ClosestPoint(origin);
                return true;
            }

            private static GameObject GetIdentityGameObject(UnityEngine.Object identity)
            {
                if (identity is Component component)
                    return component.gameObject;

                if (identity is GameObject gameObject)
                    return gameObject;

                return null;
            }
        }

        private void UpdateFootstep()
        {
            if (DialogManager.Instance.Active == true || footstepSource == null)
            {
                footstepTimer = 0f;
                return;
            }

            if (!characterController.isGrounded || currentPlanarSpeed < footstepMinSpeed || leftClip == null || rightClip == null)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer += Time.deltaTime;

            float interval = isRunning ? runningStepInterval : walkingStepInterval;
            if (footstepTimer >= interval)
            {
                PlayFootstep();
                footstepTimer = 0f;
            }
        }

        private void PlayFootstep()
        {
            footstepSource.pitch = isRunning ? runningPitch : walkingPitch;
            if (leftClip && rightClip)
            {
                footstepSource.PlayOneShot(isLeftFoot ? leftClip : rightClip);
                isLeftFoot = !isLeftFoot;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => UpdateFootstepSounds(scene.name);

        private void UpdateFootstepSounds(string sceneName)
        {
            if (sceneFootstepSounds.TryGetValue(sceneName, out var list) && list.Count >= 2)
            {
                leftClip = list[0]; rightClip = list[1];
            }
            else leftClip = rightClip = null;
        }
        #endregion
    }

    public static class Vector3Ext { public static Vector3 WithY(this Vector3 v, float y) => new(v.x, y, v.z); }
}

