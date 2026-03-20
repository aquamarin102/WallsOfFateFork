using Game;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
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
        [SerializeField] private float clickRunThreshold = 0.3f; // двойной клик
        [SerializeField] private float holdThreshold = 0.2f;     // follow курсора
        [SerializeField] private float interactionStopDistance = 1.2f;
        [SerializeField] private float mouseRaycastDistance = 500f;
        [SerializeField] private float navMeshSampleRadius = 2.5f;
        [SerializeField] private float holdRetargetDistance = 0.35f;

        [Header("Pitch Settings")]
        [SerializeField] private float walkingPitch = 1f;
        [SerializeField] private float runningPitch = 1.5f;
        [SerializeField] private DialogueManager _dialogueManager;

        private Vector3 grabDirection;
        private bool isBoxGrabMode = false;
        private BoxMover heldBoxMover;
        private Vector3 heldBoxPlayerOffset;

        private bool isHoldMove;
        private bool isClickRun;
        private float lastClickTime = -1f;
        private float mouseDownTime;

        private Vector3 clickTarget;
        private Transform dynamicTarget;
        private float dynamicStopDist;

        private Action _onArriveAction;

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

        private PlayerAnimatinController interactManager;   // ссылка на менеджер взаимодействия

        // --------------------------------------------------
        [Inject] private void Construct(Transform camTransform) => cameraTransform = camTransform;

        private void Awake()
        {
            _dialogueManager = DialogueManager.Instance;
            interactManager = GetComponent<PlayerAnimatinController>();
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

        private void Update()
        {
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
            if (_dialogueManager.IsInDialogue == true) return;
            if (IsPointerOverUi())
            {
                if (Input.GetMouseButtonUp(0))
                    isHoldMove = false;
                return;
            }

            if (isBoxGrabMode)
            {
                if (Input.GetMouseButtonDown(0))
                    mouseDownTime = Time.time;

                if (Input.GetMouseButtonUp(0))
                {
                    float held = Time.time - mouseDownTime;
                    if (held < holdThreshold)
                        ProcessClick();

                    isHoldMove = false;
                }

                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                float now = Time.time;
                isClickRun = (now - lastClickTime) <= clickRunThreshold;
                lastClickTime = now;
                mouseDownTime = now;
            }

            if (Input.GetMouseButton(0) && !isHoldMove && Time.time - mouseDownTime >= holdThreshold)
                isHoldMove = true;

            if (Input.GetMouseButtonUp(0))
            {
                float held = Time.time - mouseDownTime;
                dynamicTarget = null;                     // сброс преследования

                if (held < holdThreshold) ProcessClick();
                isHoldMove = false;
            }
        }

        private void ProcessClick()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
                return;

            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, mouseRaycastDistance, ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Collider hitCollider = hit.collider;
                if (hitCollider == null)
                    continue;

                if (TryProcessInteractionClick(hit))
                    return;

            }

            if (isBoxGrabMode)
                return;

            if (TryResolveMovementDestination(ray, hits, out Vector3 destination))
                MoveToAndCallback(destination, isClickRun, null);
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

            if (!agent.CalculatePath(navHit.position, navMeshPath) || navMeshPath.status != NavMeshPathStatus.PathComplete)
                return false;

            destination = navHit.position;
            return true;
        }
        #endregion

        // ==================================================
        #region Movement Core
        private void HandleMovement()
        {
            Vector3 positionBeforeMove = transform.position;

            if (_dialogueManager.IsInDialogue == true)
            {
                moveDirection = new Vector3(0, moveDirection.y, 0);
                characterController.Move(moveDirection * Time.deltaTime);
                agent.isStopped = true;
                currentPlanarSpeed = 0f;
                isRunning = false;
                return;
            }

            // ---- WASD breaks mouse modes ----
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            Vector3 input = new(h, 0, v);
            if (input.sqrMagnitude > 0.01f)
            {
                isHoldMove = false; isClickRun = false; dynamicTarget = null;
                agent.isStopped = true; agent.ResetPath();
            }

            Vector3 desired = Vector3.zero;

            // A) Преследование динамической цели
            if (dynamicTarget)
            {
                if (!dynamicTarget.gameObject.activeInHierarchy) StopMovement();
                else
                {
                    if (agent.destination != dynamicTarget.position)
                        agent.SetDestination(dynamicTarget.position);
                    desired = agent.desiredVelocity.WithY(0).normalized;

                    if (!agent.pathPending && agent.remainingDistance <= dynamicStopDist + 0.05f)
                    {
                        agent.isStopped = true; isClickRun = false;
                        var cb = _onArriveAction; ClearDynamic(); cb?.Invoke();
                    }
                }
            }
            // B) Follow‑режим (удержание)
            else if (isHoldMove && !IsPointerOverUi())
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (TryResolveMovementDestination(ray, out Vector3 destination))
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
                desired = agent.desiredVelocity.WithY(0).normalized;
                if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                {
                    agent.isStopped = true; isClickRun = false; _onArriveAction?.Invoke(); _onArriveAction = null;
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
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || isClickRun);
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
        public void MoveToAndCallback(Vector3 target, bool run, Action onArrive, float stopDistance = 0.1f)
        {
            dynamicTarget = null; clickTarget = target; isClickRun = run; isHoldMove = false;
            agent.stoppingDistance = stopDistance; agent.SetDestination(target); agent.isStopped = false;
            _onArriveAction = onArrive;
        }

        public void MoveToAndCallback(Transform target, bool run, Action onArrive, float stopDistance = 1f)
        {
            dynamicTarget = target; dynamicStopDist = stopDistance; isClickRun = run; isHoldMove = false;
            agent.stoppingDistance = stopDistance; agent.SetDestination(target.position); agent.isStopped = false;
            _onArriveAction = onArrive;
        }

        public void StopMovement()
        {
            agent.isStopped = true; agent.ResetPath(); isHoldMove = false; isClickRun = false; clickTarget = Vector3.zero; ClearDynamic();
            currentPlanarSpeed = 0f;
            isRunning = false;
        }
        #endregion

        // ==================================================
        #region Helpers
        private void ClearDynamic() { dynamicTarget = null; _onArriveAction = null; }

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

        private bool TryProcessInteractionClick(RaycastHit hit)
        {
            Collider hitCollider = hit.collider;
            Vector3 hitPoint = hit.point;

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out InteractibleItemInfluenceArea itemArea))
            {
                Transform target = itemArea.triggerObject != null ? itemArea.triggerObject.transform : itemArea.transform;
                MoveToAndCallback(target, isClickRun, () => _ = itemArea.InvokeDirectInteractionAsync(gameObject), interactionStopDistance);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out DoorInfluenceArea doorArea))
            {
                Transform target = doorArea.triggerObject != null ? doorArea.triggerObject.transform : doorArea.transform;
                MoveToAndCallback(target, isClickRun, () => _ = doorArea.InvokeDirectInteractionAsync(gameObject), interactionStopDistance);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out InfluenceArea influenceArea))
            {
                Transform target = influenceArea.triggerObject != null ? influenceArea.triggerObject.transform : influenceArea.transform;
                MoveToAndCallback(target, isClickRun, () => _ = influenceArea.InvokeDirectInteractionAsync(gameObject), interactionStopDistance);
                return true;
            }

            if (TryFindClosestInteractionZone(hitCollider, hitPoint, out StartDayDialogueTriggerZone startDayDialogueZone))
            {
                Transform target = startDayDialogueZone.triggerObject != null ? startDayDialogueZone.triggerObject.transform : startDayDialogueZone.transform;
                MoveToAndCallback(target, isClickRun, () => startDayDialogueZone.InvokeDirectInteraction(gameObject), interactionStopDistance);
                return true;
            }

            if (TryFindComponentOnClickedObject(hitCollider, out InteractableItem interactableItem))
            {
                MoveToAndCallback(interactableItem.transform, isClickRun, interactableItem.Interact, interactionStopDistance);
                return true;
            }

            return false;
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
            else if (candidate is StartDayDialogueTriggerZone zone && zone.triggerObject != null)
                targetTransform = zone.triggerObject.transform;

            float targetDistance = (targetTransform.position - hitPoint).sqrMagnitude;
            return boundsDistance * 10f + targetDistance;
        }

        private void UpdateFootstep()
        {
            if (_dialogueManager.IsInDialogue == true || footstepSource == null)
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

