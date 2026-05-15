using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(PlayerMoveController))]
    [RequireComponent(typeof(AudioSource))]
    public class PlayerAnimationController : MonoBehaviour
    {
        [Header("Locomotion")]
        [SerializeField] private float maxSpeed = 4.5f;
        [SerializeField] private float speedSmoothTime = 0.1f;
        [SerializeField] private float pushMoveThreshold = 0.05f;

        [Header("Footstep Audio")]
        [SerializeField] private float walkingStepInterval = 0.48f;
        [SerializeField] private float runningStepInterval = 0.32f;
        [SerializeField] private float footstepMinSpeed = 0.15f;
        [SerializeField] private float walkingPitch = 1f;
        [SerializeField] private float runningPitch = 1.5f;

        private readonly Dictionary<string, List<AudioClip>> sceneFootstepSounds = new();

        private PlayerAnimator playerAnimator;
        private PlayerMoveController moveController;
        private CharacterController characterController;
        private AudioSource footstepSource;
        private AudioClip leftClip;
        private AudioClip rightClip;
        private float currentPlanarSpeed;
        private float currentSpeedParam;
        private float speedRef;
        private float footstepTimer;
        private bool isLeftFoot = true;
        private bool isPushing;

        private void Awake()
        {
            playerAnimator = GetComponent<PlayerAnimator>();
            moveController = GetComponent<PlayerMoveController>();
            characterController = GetComponent<CharacterController>();
            footstepSource = GetComponent<AudioSource>();

            if (playerAnimator == null)
                Debug.LogError("PlayerAnimationController: PlayerAnimator component is missing.", this);

            if (moveController == null)
                Debug.LogError("PlayerAnimationController: PlayerMoveController component is missing.", this);
        }

        private void Start()
        {
            sceneFootstepSounds.Add("MainRoom", new List<AudioClip>
            {
                Resources.Load<AudioClip>("Footsteps/wood1"),
                Resources.Load<AudioClip>("Footsteps/wood2")
            });
            sceneFootstepSounds.Add("Forge", new List<AudioClip>
            {
                Resources.Load<AudioClip>("Footsteps/gravel1"),
                Resources.Load<AudioClip>("Footsteps/gravel2")
            });
            sceneFootstepSounds.Add("Storage", new List<AudioClip>
            {
                Resources.Load<AudioClip>("Footsteps/stone1"),
                Resources.Load<AudioClip>("Footsteps/stone2")
            });

            SceneManager.sceneLoaded += OnSceneLoaded;
            UpdateFootstepSounds(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void LateUpdate()
        {
            UpdateLocomotion();
            UpdateFootsteps();
        }

        public void InteractWith(TriggerEvent eventData)
        {
            if (eventData?.TriggerObj == null)
                return;

            PlayInteractionAnimation(eventData.TriggerObj);
        }

        public void SetBoxPushState(bool active)
        {
            isPushing = active;
        }

        private void UpdateLocomotion()
        {
            if (playerAnimator == null)
                return;

            currentPlanarSpeed = moveController != null ? moveController.CurrentPlanarSpeed : 0f;
            float targetNorm = Mathf.Clamp01(currentPlanarSpeed / Mathf.Max(maxSpeed, 0.0001f));
            currentSpeedParam = Mathf.SmoothDamp(currentSpeedParam, targetNorm, ref speedRef, speedSmoothTime);

            bool isActivelyPushing = isPushing &&
                moveController != null &&
                moveController.IsBoxPushCommandActive &&
                currentPlanarSpeed >= pushMoveThreshold;
            float pushSpeedParam = isActivelyPushing && moveController != null
                ? moveController.BoxPushAnimationSpeed
                : 1f;
            playerAnimator.ApplyLocomotion(currentSpeedParam, isActivelyPushing, pushSpeedParam);
        }

        private void UpdateFootsteps()
        {
            if (DialogManager.Instance.Active == true || footstepSource == null)
            {
                footstepTimer = 0f;
                return;
            }

            if (characterController == null ||
                !characterController.isGrounded ||
                currentPlanarSpeed < footstepMinSpeed ||
                leftClip == null ||
                rightClip == null)
            {
                footstepTimer = 0f;
                return;
            }

            footstepTimer += Time.deltaTime;

            float interval = moveController != null && moveController.IsRunning
                ? runningStepInterval
                : walkingStepInterval;
            if (footstepTimer < interval)
                return;

            PlayFootstep();
            footstepTimer = 0f;
        }

        private void PlayFootstep()
        {
            if (footstepSource == null || leftClip == null || rightClip == null)
                return;

            bool isRunning = moveController != null && moveController.IsRunning;
            footstepSource.pitch = isRunning ? runningPitch : walkingPitch;
            footstepSource.PlayOneShot(isLeftFoot ? leftClip : rightClip);
            isLeftFoot = !isLeftFoot;
        }

        private void PlayInteractionAnimation(GameObject targetObject)
        {
            if (playerAnimator == null || targetObject == null)
                return;

            if (targetObject.CompareTag("PickupFloor"))
                playerAnimator.PlayPickupFloor();
            else if (targetObject.CompareTag("PickupBody"))
                playerAnimator.PlayPickupBody();
            else if (targetObject.CompareTag("Chest"))
                playerAnimator.PlayOpenChest();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UpdateFootstepSounds(scene.name);
        }

        private void UpdateFootstepSounds(string sceneName)
        {
            if (sceneFootstepSounds.TryGetValue(sceneName, out List<AudioClip> list) && list.Count >= 2)
            {
                leftClip = list[0];
                rightClip = list[1];
            }
            else
            {
                leftClip = null;
                rightClip = null;
            }
        }
    }
}
