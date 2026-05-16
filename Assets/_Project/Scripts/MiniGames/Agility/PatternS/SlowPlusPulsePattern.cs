using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlowPlusPulsePattern : FormationPhasePattern
{
    [Range(0.2f, 0.8f)] public float actionRadiusFactor = 0.42f;
    public int sweepCount = 3;
    public float pauseBetweenSweeps = 0.12f;
    [Range(0.02f, 0.3f)] public float centerRadiusFactor = 0.08f;

    protected override void CreateTelegraphs()
    {
        float outerRadius = CurrentActionRadius();
        float lineLength = outerRadius * 2.15f;
        float laneWidth = Mathf.Max(0.6f, outerRadius * 0.28f);
        AddTelegraphLine(Center, new Vector3(lineLength, 0.1f, laneWidth), 0f);
        AddTelegraphLine(Center, new Vector3(laneWidth, 0.1f, lineLength), 0f);
    }

    protected override IReadOnlyList<Vector3> BuildEntryTargets()
    {
        return CardinalTargets(CurrentActionRadius());
    }

    protected override IEnumerator ExecuteFormation(float activeDuration)
    {
        Vector3[] outerTargets = CardinalTargets(CurrentActionRadius());
        Vector3[] oppositeOuterTargets = RotateTargets(outerTargets, 2);
        Vector3[] innerTargets = CardinalTargets(CurrentCenterRadius());

        int cycleCount = Mathf.Max(1, sweepCount);
        int steps = cycleCount * 2;
        bool useOpposite = true;
        float totalPause = pauseBetweenSweeps * Mathf.Max(0, steps - 1);
        float moveDuration = Mathf.Max(0.3f, (activeDuration - totalPause) / steps);
        float timelineDuration = moveDuration * steps + totalPause;
        float timelineElapsed = 0f;
        int stepIndex = 0;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            IReadOnlyList<Vector3> targets = innerTargets;

            float moveStart = timelineDuration > 0f ? timelineElapsed / timelineDuration : 0f;
            timelineElapsed += moveDuration;
            float moveEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
            yield return MoveActors(targets, moveDuration, 1f, moveStart, moveEnd);
            stepIndex++;

            if (stepIndex < steps && pauseBetweenSweeps > 0f)
            {
                float pauseStart = moveEnd;
                timelineElapsed += pauseBetweenSweeps;
                float pauseEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
                yield return WaitForSecondsWithCoreProgress(pauseBetweenSweeps, pauseStart, pauseEnd);
            }

            targets = useOpposite ? oppositeOuterTargets : outerTargets;
            useOpposite = !useOpposite;

            moveStart = timelineDuration > 0f ? timelineElapsed / timelineDuration : 0f;
            timelineElapsed += moveDuration;
            moveEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
            yield return MoveActors(targets, moveDuration, 1f, moveStart, moveEnd);
            stepIndex++;

            if (stepIndex < steps && pauseBetweenSweeps > 0f)
            {
                float pauseStart = moveEnd;
                timelineElapsed += pauseBetweenSweeps;
                float pauseEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
                yield return WaitForSecondsWithCoreProgress(pauseBetweenSweeps, pauseStart, pauseEnd);
            }
        }
    }

    private float CurrentActionRadius()
    {
        return Mathf.Max(1.05f, ArenaRadius * actionRadiusFactor);
    }

    private float CurrentCenterRadius()
    {
        return Mathf.Max(0.12f, ArenaRadius * centerRadiusFactor);
    }
}
