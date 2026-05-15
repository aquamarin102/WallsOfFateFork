using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlowClockwiseVolleyPattern : FormationPhasePattern
{
    [Range(0.15f, 0.7f)] public float orbitRadiusFactor = 0.36f;
    public float orbitRadiusPadding = 0f;
    public float angularSpeed = 62f;
    public int pulseCount = 2;
    public float desiredStepDuration = 0.48f;
    [Range(0.1f, 0.8f)] public float innerRadiusFactor = 0.52f;

    private readonly Vector3[] _orbitTargets = new Vector3[4];

    protected override void CreateTelegraphs()
    {
        IReadOnlyList<Vector3> orbitTargets = BuildOrbitTargets(CurrentOuterRadius());
        for (int i = 0; i < orbitTargets.Count; i++)
            AddTelegraphZone(orbitTargets[i], 0.32f);
    }

    protected override IReadOnlyList<Vector3> BuildEntryTargets()
    {
        return BuildOrbitTargets(CurrentOuterRadius());
    }

    protected override IEnumerator ExecuteFormation(float activeDuration)
    {
        float outerRadius = CurrentOuterRadius();
        float innerRadius = Mathf.Max(0.35f, outerRadius * innerRadiusFactor);
        int stepCount = Mathf.Max(4, Mathf.CeilToInt(activeDuration / Mathf.Max(0.28f, desiredStepDuration)));
        float stepDuration = StepDuration(activeDuration, stepCount, 0.28f);
        float angleOffset = 0f;
        float timelineDuration = stepCount * stepDuration;
        float timelineElapsed = 0f;

        for (int step = 0; step < stepCount; step++)
        {
            float normalizedTime = stepCount <= 1
                ? 1f
                : step / Mathf.Max(1f, stepCount - 1f);
            float pulse = Mathf.PingPong(normalizedTime * pulseCount * 2f, 1f);
            float radius = Mathf.Lerp(outerRadius, innerRadius, pulse);
            angleOffset -= angularSpeed * stepDuration;
            float stepStart = timelineDuration > 0f ? timelineElapsed / timelineDuration : 0f;
            timelineElapsed += stepDuration;
            float stepEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
            yield return MoveActors(BuildOrbitTargets(radius, angleOffset), stepDuration, 1f, stepStart, stepEnd);
        }
    }

    private IReadOnlyList<Vector3> BuildOrbitTargets(float radius, float angleOffset = 0f)
    {
        int count = Mathf.Min(SlotEntrances.Length, _orbitTargets.Length);
        for (int i = 0; i < count; i++)
        {
            float angle = angleOffset + i * (360f / Mathf.Max(1, count));
            _orbitTargets[i] = PointOnRing(angle, radius);
        }

        return _orbitTargets;
    }

    private float CurrentOuterRadius()
    {
        return Mathf.Max(0.8f, ArenaRadius * orbitRadiusFactor + orbitRadiusPadding);
    }
}
