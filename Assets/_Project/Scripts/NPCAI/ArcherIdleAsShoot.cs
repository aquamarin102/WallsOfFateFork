using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ArcherIdleAsShoot : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Player Tag")]
    public string playerTag = "Player";

    [Header("Animation Speed Range")]
    public float minSpeed = 0.8f;
    public float maxSpeed = 1.2f;

    [Header("Hit Sound")]
    public AudioSource hitAudioSource;
    public AudioClip[] hitSounds;
    [Range(0f, 1f)] public float hitVolume = 1f;
    [Range(0f, 1f)] public float hitMomentNormalized = 0.55f;

    [Header("Hit Sound 3D")]
    [Range(0f, 1f)] public float hitSpatialBlend = 1f;
    [Min(0f)] public float hitMinDistance = 2f;
    [Min(0f)] public float hitMaxDistance = 12f;

    private Animator animator;
    private bool isBlocked;
    private float currentSpeed;
    private int lastHitLoop = -1;

    private readonly int idleStateHash = Animator.StringToHash("Idle");

    private void Start()
    {
        animator = GetComponent<Animator>();
        if (hitAudioSource == null)
            hitAudioSource = GetComponent<AudioSource>();
        if (hitAudioSource == null)
            hitAudioSource = gameObject.AddComponent<AudioSource>();

        ConfigureHitAudioSource();

        ResumeLoop();
    }

    private void OnValidate()
    {
        ConfigureHitAudioSource();
    }

    private void Update()
    {
        if (animator == null || target == null)
            return;

        TryPlayLoopHitSound();

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 dir = (target.position - origin).normalized;
        float dist = Vector3.Distance(origin, target.position);

        bool nowBlocked = false;
        if (Physics.Raycast(origin, dir, out var hit, dist))
            nowBlocked = hit.collider.CompareTag(playerTag);

        if (nowBlocked && !isBlocked)
        {
            isBlocked = true;
            animator.Play(idleStateHash, 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
            return;
        }
        else if (!nowBlocked && isBlocked)
        {
            isBlocked = false;
            ResumeLoop();
        }
    }

    private void ResumeLoop()
    {
        currentSpeed = Random.Range(minSpeed, maxSpeed);
        animator.speed = currentSpeed;
        animator.Play(idleStateHash, 0, 0f);
        lastHitLoop = -1;
    }

    private void TryPlayLoopHitSound()
    {
        if (isBlocked || hitSounds == null || hitSounds.Length == 0)
            return;

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (state.shortNameHash != idleStateHash)
            return;

        int currentLoop = Mathf.FloorToInt(state.normalizedTime);
        float normalizedCycleTime = state.normalizedTime - currentLoop;

        if (normalizedCycleTime < hitMomentNormalized || lastHitLoop == currentLoop)
            return;

        PlayRandomHitSound();
        lastHitLoop = currentLoop;
    }

    public void PlayRandomHitSound()
    {
        if (hitSounds == null || hitSounds.Length == 0)
            return;

        AudioClip clip = hitSounds[Random.Range(0, hitSounds.Length)];
        if (clip == null)
            return;

        ConfigureHitAudioSource();
        if (hitAudioSource == null)
            return;

        hitAudioSource.PlayOneShot(clip, hitVolume);
    }

    private void ConfigureHitAudioSource()
    {
        if (hitAudioSource == null)
            return;

        hitAudioSource.playOnAwake = false;
        hitAudioSource.loop = false;
        hitAudioSource.spatialBlend = hitSpatialBlend;
        hitAudioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        hitAudioSource.minDistance = hitMinDistance;
        hitAudioSource.maxDistance = Mathf.Max(hitMinDistance + 0.01f, hitMaxDistance);
        hitAudioSource.dopplerLevel = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        Gizmos.color = Color.red;
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Gizmos.DrawLine(origin, target.position);
    }
}
