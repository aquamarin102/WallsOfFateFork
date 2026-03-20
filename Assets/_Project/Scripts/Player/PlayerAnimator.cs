using Game;
using UnityEngine;

/// <summary>
/// Handles animation parameters for the player character and plays footstep sounds.
/// The main goal is to keep the locomotion blend-tree perfectly smooth, even when the
/// physics update rate fluctuates or the character changes state (pushing / running / idle).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    #region Inspector
    [Header("Movement")]
    [Tooltip("Top-end speed of the character in m/s. Used for normalising the blend-tree parameter.")]
    [SerializeField] private float maxSpeed = 4.5f;

    [Tooltip("Time it takes for the Speed parameter to reach the target value (seconds).")]
    [SerializeField] private float speedSmoothTime = 0.1f;

    [Header("Pushing Box")]
    [Tooltip("Tag that marks objects the player can push.")]
    [SerializeField] private string boxTag = "Box";
    [SerializeField] private float pushAnimationSpeed = 1.5f;

    [Tooltip("Minimal speed (m/s) at which the pushing animation starts to blend in.")]
    [SerializeField] private float pushMoveThreshold = 0.05f;

    [Header("Footsteps (optional)")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] walkStepClips;
    [SerializeField] private AudioClip[] runStepClips;
    [SerializeField] private float baseFootstepInterval = 0.5f; // walk cadence
    [SerializeField] private float runFootstepInterval = 0.3f; // run cadence
    #endregion

    private Animator animator;
    private CharacterController characterController;
    private PlayerMoveController moveController;
    private Vector3 lastPos;
    private float currentSpeedParam; // smoothed animator parameter [0-1]
    private float speedRef;          // velocity reference for SmoothDamp
    private float footstepTimer;
    private bool isPushing;

    // -----------------------------------------------------------------------------
    #region Unity
    private void Awake()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        moveController = GetComponent<PlayerMoveController>();
        lastPos = transform.position;
    }

    private void LateUpdate()
    {
        UpdateLocomotion();   // handles speed, pushing, animator parameters
        UpdateFootsteps();    // handles audio cadence
        lastPos = transform.position; // cache for next frame after all logic
    }
    #endregion

    public void StartPushing()
    {
        isPushing = true;
    }

    public void StopPushing()
    {
        isPushing = false;
    }

    // -----------------------------------------------------------------------------
    #region Locomotion
    private void UpdateLocomotion()
    {
        float rawSpeed = GetPlanarSpeed();
        float targetNorm = Mathf.Clamp01(rawSpeed / Mathf.Max(maxSpeed, 0.0001f));
        currentSpeedParam = Mathf.SmoothDamp(currentSpeedParam, targetNorm, ref speedRef, speedSmoothTime);

        float pushSpeedParam = isPushing
            ? (currentSpeedParam >= pushMoveThreshold ? 1f : 0f)
            : 0f;

        animator.SetBool("IsPushing", isPushing);
        animator.SetFloat("PushSpeed", pushSpeedParam);

        if (isPushing && pushSpeedParam == 0f)
        {
            animator.speed = 0f;
            return;
        }

        animator.speed = isPushing ? pushAnimationSpeed : 1f;

        if (!isPushing)
        {
            animator.SetFloat("Speed", currentSpeedParam);
            animator.SetBool("IsPushing", false);
        }
    }

    private float GetPlanarSpeed()
    {
        if (moveController != null)
            return moveController.CurrentPlanarSpeed;

        if (characterController != null)
        {
            Vector3 velocity = characterController.velocity;
            velocity.y = 0f;
            return velocity.magnitude;
        }

        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        return delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
    }
    #endregion

    // -----------------------------------------------------------------------------
    #region Footstep Audio
    private void UpdateFootsteps()
    {
        if (footstepSource == null) return; // sound system is optional

        if (currentSpeedParam > 0.05f)
        {
            footstepTimer += Time.deltaTime;
            float interval = Mathf.Lerp(baseFootstepInterval, runFootstepInterval, currentSpeedParam);

            if (footstepTimer >= interval)
            {
                bool running = moveController != null ? moveController.IsRunning : currentSpeedParam > 0.6f;
                PlayRandomFootstep(running);
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = 0f; // reset so the first step triggers instantly when we start moving again
        }
    }

    private void PlayRandomFootstep(bool running)
    {
        AudioClip[] bank = running ? runStepClips : walkStepClips;
        if (bank == null || bank.Length == 0) return;

        footstepSource.clip = bank[Random.Range(0, bank.Length)];
        footstepSource.Play();
    }
    #endregion

    // -----------------------------------------------------------------------------
    #region Public Triggers
    public void PlayPickupFloor() => animator.SetTrigger("PickupFloor");
    public void PlayPickupBody() => animator.SetTrigger("PickupBody");
    public void PlayOpenChest() => animator.SetTrigger("OpenChest");
    public void PlayGrabBox() => animator.SetTrigger("GrabBox");
    #endregion
}
