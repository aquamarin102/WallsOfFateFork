using System.Collections;
using UnityEngine;

public class RotatingCrossSweepsPattern : PatternBehaviour
{
    [Header("Prefabs")]
    public GameObject tokenPrefab;

    [Header("Entrances")]
    public Entrance gateA = Entrance.NorthEast;
    public Entrance gateB = Entrance.SouthEast;

    [Header("Timing")]
    public float stepTelegraph = 0.18f;
    public int steps = 4;
    public float stepAngleDeg = 45f;

    [Header("Geometry")]
    public float passDistance = 6f;
    public Color telegraphColor = new Color(1f, 0.78f, 0.22f);

    [Header("Return")]
    public float returnSpacing = 0.1f;

    private GameObject _telegraphA;
    private GameObject _telegraphB;

    public override void BeginTelegraph()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        center.y = ResolveY();
        float radius = Mathf.Max(passDistance, AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter) + 0.9f);

        _telegraphA = CreateDiagonalTelegraph(center, 45f, radius, "CrossTelegraphA");
        _telegraphB = CreateDiagonalTelegraph(center, -45f, radius, "CrossTelegraphB");
    }

    public override IEnumerator Run()
    {
        Transform gatePointA = Ctx.entrances != null ? Ctx.entrances.Get(gateA) : null;
        Transform gatePointB = Ctx.entrances != null ? Ctx.entrances.Get(gateB) : null;
        if (gatePointA == null || gatePointB == null)
            yield break;

        GateController gateControllerA = ResolveGateController(gatePointA);
        GateController gateControllerB = ResolveGateController(gatePointB);
        Vector3 gatePositionA = gatePointA.position;
        Vector3 gatePositionB = gatePointB.position;
        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        center.y = gatePositionA.y;
        float radius = Mathf.Max(passDistance, AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter) + 0.9f);

        OpenGate(gateControllerA);
        OpenGate(gateControllerB);

        TokenAgent tokenA = SpawnToken(gatePositionA + new Vector3(-returnSpacing, 0f, 0f));
        TokenAgent tokenB = SpawnToken(gatePositionB + new Vector3(+returnSpacing, 0f, 0f));
        if (tokenA == null || tokenB == null)
        {
            if (tokenA != null)
                Destroy(tokenA.gameObject);
            if (tokenB != null)
                Destroy(tokenB.gameObject);

            CloseGate(gateControllerA);
            CloseGate(gateControllerB);
            yield break;
        }

        CleanupTelegraphs();

        int actualSteps = Mathf.Max(1, steps);
        float stepBudget = Mathf.Max(0.45f, Definition.duration / actualSteps);
        float sweepSeconds = stepBudget * 0.42f;
        float gapSeconds = stepBudget * 0.08f;
        float rotateSeconds = stepBudget * 0.27f;

        float angleA = 45f * Mathf.Deg2Rad;
        tokenA.transform.position = StartPoint(center, angleA, +1f, radius);
        tokenB.transform.position = StartPoint(center, angleA - Mathf.PI * 0.5f, +1f, radius);

        for (int s = 0; s < actualSteps; s++)
        {
            float startSign = (s % 2 == 0) ? +1f : -1f;
            float aA = angleA;
            float aB = angleA - Mathf.PI * 0.5f;

            Vector3 aStart = StartPoint(center, aA, startSign, radius);
            Vector3 aEnd = EndPoint(center, aA, startSign, radius);
            Vector3 bStart = StartPoint(center, aB, startSign, radius);
            Vector3 bEnd = EndPoint(center, aB, startSign, radius);

            if (stepTelegraph > 0f)
                yield return new WaitForSeconds(stepTelegraph);

            yield return MoveLinear(tokenA, aStart, aEnd, sweepSeconds);
            if (gapSeconds > 0f)
                yield return new WaitForSeconds(gapSeconds);
            yield return MoveLinear(tokenB, bStart, bEnd, sweepSeconds);

            float nextAngleA = angleA + stepAngleDeg * Mathf.Deg2Rad;
            float nextSign = ((s + 1) % 2 == 0) ? +1f : -1f;

            if (s < actualSteps - 1)
            {
                Vector3 nextAStart = StartPoint(center, nextAngleA, nextSign, radius);
                Vector3 nextBStart = StartPoint(center, nextAngleA - Mathf.PI * 0.5f, nextSign, radius);

                yield return MoveLinear(tokenA, tokenA.transform.position, nextAStart, rotateSeconds);
                yield return MoveLinear(tokenB, tokenB.transform.position, nextBStart, rotateSeconds);
            }

            angleA = nextAngleA;
        }

        tokenA.transform.position = gatePositionA + new Vector3(-returnSpacing, 0f, 0f);
        tokenB.transform.position = gatePositionB + new Vector3(+returnSpacing, 0f, 0f);

        if (tokenA != null)
            Destroy(tokenA.gameObject);
        if (tokenB != null)
            Destroy(tokenB.gameObject);

        CloseGate(gateControllerA);
        CloseGate(gateControllerB);
    }

    public override void Cleanup()
    {
        CleanupTelegraphs();
    }

    private TokenAgent SpawnToken(Vector3 pos)
    {
        GameObject go;
        if (tokenPrefab != null)
        {
            go = Instantiate(tokenPrefab, pos, Quaternion.identity, transform);
        }
        else
        {
            go = AgilityHazardFactory.CreateBodyHazard(transform, "CrossSweeper", pos, Vector3.one * 0.9f, new Color(0.76f, 0.18f, 0.08f));
        }

        var agent = go.GetComponent<TokenAgent>();
        if (agent == null)
            agent = go.AddComponent<TokenAgent>();
        return agent;
    }

    private Vector3 StartPoint(Vector3 center, float angleRad, float startSign, float radius)
    {
        Vector3 direction = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
        Vector3 point = center + direction * (radius * startSign);
        point.y = center.y;
        return point;
    }

    private Vector3 EndPoint(Vector3 center, float angleRad, float startSign, float radius)
    {
        Vector3 direction = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
        Vector3 point = center - direction * (radius * startSign);
        point.y = center.y;
        return point;
    }

    private IEnumerator MoveLinear(TokenAgent agent, Vector3 from, Vector3 to, float seconds)
    {
        if (agent == null)
            yield break;

        agent.transform.position = from;

        if (seconds <= 0f)
        {
            agent.transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (agent == null)
                yield break;

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / seconds);
            agent.transform.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        agent.transform.position = to;
    }

    private GameObject CreateDiagonalTelegraph(Vector3 center, float angleDeg, float radius, string name)
    {
        Vector3 position = center;
        Vector3 scale = new Vector3(radius * 2f, 0.1f, 0.5f);
        var telegraph = AgilityHazardFactory.CreateLaneTelegraph(transform, name, position, scale, telegraphColor);
        telegraph.transform.rotation = Quaternion.Euler(0f, -angleDeg, 0f);
        return telegraph;
    }

    private void CleanupTelegraphs()
    {
        if (_telegraphA != null)
            Destroy(_telegraphA);
        if (_telegraphB != null)
            Destroy(_telegraphB);
        _telegraphA = null;
        _telegraphB = null;
    }

    private float ResolveY()
    {
        if (Ctx != null)
            return Ctx.boardY + Ctx.hazardHeightOffset;

        return AgilitySceneUtility.ResolveArenaCenter(Ctx != null ? Ctx.arenaCenter : null).y + 0.5f;
    }

    private void OpenGate(GateController gate)
    {
        if (gate?.animator != null)
            gate.animator.SetTrigger(gate.openTrigger);
    }

    private void CloseGate(GateController gate)
    {
        if (gate?.animator != null)
            gate.animator.SetTrigger(gate.closeTrigger);
    }

    private static GateController ResolveGateController(Transform gatePoint)
    {
        if (gatePoint == null)
            return null;

        return gatePoint.GetComponent<GateController>()
               ?? gatePoint.GetComponentInParent<GateController>()
               ?? gatePoint.GetComponentInChildren<GateController>();
    }
}
