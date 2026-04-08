using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class ChickenAI : MonoBehaviour
{
    #region --- Inspector ---

    [Header("Общее")]
    public float detectionRange = 5f;

    [Header("Скорости")]
    public float wanderSpeed = 1.5f;
    public float fleeSpeed = 3.5f;

    [Header("Паузы / Радиусы")]
    public float idleTime = 2f;
    public float wanderRadius = 10f;
    public float fleeDistance = 8f;          // Насколько далеко пытаемся убежать

    [Header("Поворот")]
    public float turnSpeed = 360f;           // °/сек

    [Header("Анти-тупик")]
    public float stuckTime = 2f;             // Если не сдвинулись за N секунд — пересчитываем маршрут

    public Transform player;                 // ссылка на игрока

    #endregion

    private NavMeshAgent agent;
    private Animator animator;

    private enum State { Idle, Turn, Wander, Flee }
    private State state;

    private float idleTimer;
    private float moveTimer;
    private Vector3 pendingDestination;

    private NavMeshPath path;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        agent.updateRotation = false;        // поворот берём на себя
        path = new NavMeshPath();

        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
            //else Debug.LogError("Player not found — тегните объект игрока как 'Player'.");
        }

        state = State.Idle;
        idleTimer = idleTime;
        SetAnim(0, 0);
    }

    void Update()
    {
        DetectThreat();

        switch (state)
        {
            case State.Idle: TickIdle(); break;
            case State.Turn: TickTurn(); break;
            case State.Wander: TickWander(); break;
            case State.Flee: TickFlee(); break;
        }

        if (state != State.Turn && agent.velocity.sqrMagnitude > 0.001f)
        {
            Quaternion look = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.RotateTowards(
                                    transform.rotation,
                                    look,
                                    turnSpeed * Time.deltaTime);

        }
        }

    #region --- State logic ---

    void TickIdle()
    {
        SetAnim(0, 0);
        agent.isStopped = true;

        if ((idleTimer -= Time.deltaTime) <= 0)
            PickWanderTarget();
    }

    void TickTurn()
    {
        SetAnim(0, 0);

        if (RotateTowards(pendingDestination))
        {
            agent.isStopped = false;
            moveTimer = 0;
            state = (agent.speed == fleeSpeed) ? State.Flee : State.Wander;
        }
    }

    void TickWander()
    {
        SetAnim(1, 0);
        agent.speed = wanderSpeed;
        AntiStuck(PickWanderTarget);

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            idleTimer = idleTime;
            state = State.Idle;
        }
    }

    void TickFlee()
    {
        SetAnim(1, 1);
        agent.speed = fleeSpeed;
        AntiStuck(StartFlee);

        if (Vector3.Distance(transform.position, player.position) > detectionRange * 1.3f)
        {
            idleTimer = idleTime;
            state = State.Idle;
        }
    }

    #endregion

    #region --- Movement helpers ---

    void AntiStuck(System.Action retry)
    {
        moveTimer += Time.deltaTime;

        if (moveTimer >= stuckTime &&
            agent.remainingDistance > agent.stoppingDistance + 0.1f)
        {
            retry.Invoke();         // пересчитываем маршрут
        }
    }

    void BeginMove(Vector3 dest, float speed, State finalState)
    {
        pendingDestination = dest;
        agent.SetDestination(dest);
        agent.speed = speed;
        agent.isStopped = true;     // ждём поворота
        moveTimer = 0;
        state = State.Turn;
    }

    bool RotateTowards(Vector3 worldTarget)
    {
        Vector3 dir = worldTarget - transform.position;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.001f) return true;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
                                transform.rotation,
                                targetRot,
                                turnSpeed * Time.deltaTime);

        return Quaternion.Angle(transform.rotation, targetRot) < 1f;
    }

    #endregion

    #region --- Target selection ---

    // --- Wander -------------------------------------------------------------
    void PickWanderTarget()
    {
        const int tryCount = 8;

        for (int i = 0; i < tryCount; i++)
        {
            Vector3 random = Random.insideUnitSphere * wanderRadius + transform.position;

            if (NavMesh.SamplePosition(random, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                BeginMove(hit.position, wanderSpeed, State.Wander);
                return;
            }
        }

        // ничего не нашли — остаёмся в Idle
        idleTimer = idleTime;
        state = State.Idle;
    }

    // --- Flee ---------------------------------------------------------------
    void StartFlee()
    {
        if (!player)
        {
            PickWanderTarget();
            return;
        }

        Vector3 away = (transform.position - player.position).normalized;

        // Перебираем углы внутри полусферы, пока не найдём путь
        float[] angles = { 0, -30, 30, -60, 60, -90, 90 };   // градусы от направления «прямо от игрока»
        foreach (float a in angles)
        {
            Vector3 dir = Quaternion.AngleAxis(a, Vector3.up) * away;
            Vector3 dst = transform.position + dir * fleeDistance;

            if (NavMesh.SamplePosition(dst, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete)
            {
                BeginMove(hit.position, fleeSpeed, State.Flee);
                return;
            }
        }

        // Всё заблокировано (например, в самом тесном углу) — пробуем блуждать
        PickWanderTarget();
    }

    #endregion

    #region --- Threat detection & animation ---

    void DetectThreat()
    {
        if (!player) return;

        float d = Vector3.Distance(transform.position, player.position);
        if (d < detectionRange &&
            state != State.Flee && state != State.Turn)
        {
            StartFlee();
        }
    }

    void SetAnim(float vert, float stateParam)
    {
        animator.SetFloat("Vert", vert);     // 0-Idle  1-Движение
        animator.SetFloat("State", stateParam); // 0-Walk 1-Run
    }

    #endregion
}
