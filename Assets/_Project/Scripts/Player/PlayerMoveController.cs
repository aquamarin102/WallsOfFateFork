using Game;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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

        [Header("Pitch Settings")]
        [SerializeField] private float walkingPitch = 1f;
        [SerializeField] private float runningPitch = 1.5f;
        [SerializeField] private DialogueManager _dialogueManager;

        private Vector3 grabDirection;
        private bool isBoxGrabMode = false;

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

        public void InBoxGrabMode(Vector3 axis)
        {
            isBoxGrabMode = true;
            grabDirection = axis.normalized;
        }

        public void StopBoxGrabMode()
        {
            isBoxGrabMode = false;
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

        // ==================================================
        #region Mouse Input
        private void HandleMouseInput()
        {
            if (_dialogueManager.IsInDialogue == true) return;

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

                //if (held < holdThreshold) ProcessClick();
                isHoldMove = false;
            }
        }

        //private void ProcessClick()
        //{
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    var hits = Physics.RaycastAll(ray, 100f);
        //    Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        //    foreach (var hit in hits)
        //    {
        //        // 1) Любой объект, реализующий ITriggerable
        //        if (hit.collider.TryGetComponent<ITriggerable>(out var trigger) && interactManager)
        //        {
        //            // если уже активирован и не повторяется — пропускаем
        //            if (interactManager.HasTriggerBeenActivated(trigger) && trigger is not Box)
        //                continue;

        //            float stopDist = 1.2f;
        //            if (hit.collider.TryGetComponent<NavMeshAgent>(out _))
        //                MoveToAndCallback(hit.collider.transform, isClickRun, () => interactManager.InteractWith(trigger), stopDist);
        //            else
        //                MoveToAndCallback(hit.point, isClickRun, () => interactManager.InteractWith(trigger), stopDist);
        //            return;
        //        }
        //        // 2) Плоскость земли
        //        if (((1 << hit.collider.gameObject.layer) & groundMask) != 0)
        //        {
        //            if (agent.CalculatePath(hit.point, navMeshPath) && navMeshPath.status == NavMeshPathStatus.PathComplete)
        //                MoveToAndCallback(hit.point, isClickRun, null);
        //            return;
        //        }
        //    }
        //}
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
            else if (isHoldMove)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100f, groundMask) &&
                    agent.CalculatePath(hit.point, navMeshPath) && navMeshPath.status == NavMeshPathStatus.PathComplete)
                {
                    clickTarget = hit.point; agent.SetDestination(clickTarget); agent.isStopped = false;
                    desired = agent.desiredVelocity.WithY(0).normalized;
                }
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
            bool running = hasPlanarInput && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || isClickRun);
            float speed = moveSpeed * (running ? runMultiplier : 1f);
            moveDirection = desired * speed;

            verticalVelocity = characterController.isGrounded ? -1f : verticalVelocity - gravity * Time.deltaTime;
            moveDirection.y = verticalVelocity;

            characterController.Move(moveDirection * Time.deltaTime);
            agent.nextPosition = transform.position;

            Vector3 actualDelta = transform.position - positionBeforeMove;
            actualDelta.y = 0f;
            currentPlanarSpeed = actualDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            isRunning = running && currentPlanarSpeed > 0.01f;
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

