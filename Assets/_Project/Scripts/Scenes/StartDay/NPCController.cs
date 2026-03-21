using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(AudioSource))]
public class NPCController : MonoBehaviour
{
    public event Action<NPCController> Arrived;
    public event Action<NPCController> Left;

    [Header("Rotation")]
    [SerializeField] private float _rotationSpeed = 360f;

    [Header("Footsteps")]
    [SerializeField] private AudioClip stepClip1;
    [SerializeField] private AudioClip stepClip2;
    [Tooltip("Интервал между шагами при ходьбе с NavMeshAgent.speed")]
    [SerializeField] private float baseStepInterval = 0.5f;

    private NavMeshAgent _agent;
    private Animator _animator;
    private AudioSource _audio;
    private static readonly int SpeedHash = Animator.StringToHash("speed");

    private Transform _exitSpot;
    private bool _reachedSpot;

    /* ——— шаговый таймер ——— */
    private float _stepTimer;
    private bool _stepToggle;      // чередуем clip1 / clip2

    /* ----------------------- */

    public void Init(Transform dialogSpot, Transform exitSpot)
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponent<Animator>();
        _audio = GetComponent<AudioSource>();
        _exitSpot = exitSpot;

        _reachedSpot = false;
        _stepTimer = 0f;
        _stepToggle = false;

        _agent.isStopped = false;
        _agent.SetDestination(dialogSpot.position);
    }

    private void Update()
    {
        float currentSpeed = _agent.velocity.magnitude;
        _animator.SetFloat(SpeedHash, currentSpeed);

        HandleFootsteps(currentSpeed);

        if (!_reachedSpot &&
            !_agent.pathPending &&
            _agent.remainingDistance <= _agent.stoppingDistance + 0.05f &&
            currentSpeed <= 0.01f)
        {
            _reachedSpot = true;
            _agent.isStopped = true;
            Arrived?.Invoke(this);
        }
    }

    /* ---------- FOOTSTEP LOGIC ---------- */
    private void HandleFootsteps(float currentSpeed)
    {
        if (currentSpeed > 0.1f && !_agent.isStopped)
        {
            // интервал обратно пропорционален скорости
            float interval = baseStepInterval * (_agent.speed / currentSpeed);
            _stepTimer += Time.deltaTime;

            if (_stepTimer >= interval)
            {
                PlayStep();
                _stepTimer = 0f;
            }
        }
        else
        {
            _stepTimer = 0f;   // сброс, если стоим
        }
    }

    private void PlayStep()
    {
        if (!_audio || (!stepClip1 && !stepClip2)) return;

        AudioClip clip = _stepToggle ? stepClip1 : stepClip2;
        _stepToggle = !_stepToggle;
        _audio.PlayOneShot(clip);
    }
    /* ------------------------------------ */

    /* ---------- УХОД NPC ---------- */
    public void Leave() => StartCoroutine(LeaveSequence());

    private IEnumerator LeaveSequence()
    {
        _agent.isStopped = true;
        _agent.velocity = Vector3.zero;
        _animator.SetFloat(SpeedHash, 0f);

        yield return RotateToPoint(_exitSpot.position);

        _agent.isStopped = false;
        _agent.SetDestination(_exitSpot.position);

        yield return WaitExit();

        _agent.isStopped = true;
        _animator.SetFloat(SpeedHash, 0f);

        Left?.Invoke(this);
    }

    private IEnumerator RotateToPoint(Vector3 target)
    {
        Vector3 dir = target - transform.position; dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) yield break;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator WaitExit()
    {
        while (_agent.pathPending ||
               _agent.remainingDistance > _agent.stoppingDistance + 0.05f)
            yield return null;
    }
}
