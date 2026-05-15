using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlowDiagonalPulsePattern : FormationPhasePattern
{
    [Range(0.2f, 0.8f)] public float actionRadiusFactor = 0.44f;
    public int sweepCount = 3;
    public float pauseBetweenDiagonals = 0.12f;
    [Range(0.02f, 0.3f)] public float centerRadiusFactor = 0.08f;

    protected override void CreateTelegraphs()
    {
        float actionRadius = CurrentActionRadius();
        float lineLength = actionRadius * 2.15f;
        float laneWidth = Mathf.Max(0.6f, actionRadius * 0.28f);
        Vector3 telegraphSize = new Vector3(lineLength, 0.1f, laneWidth);
        AddTelegraphLine(Center, telegraphSize, 45f);
        AddTelegraphLine(Center, telegraphSize, -45f);
    }

    protected override IReadOnlyList<Vector3> BuildEntryTargets()
    {
        return DiagonalTargets(CurrentActionRadius());
    }

    protected override IEnumerator ExecuteFormation(float activeDuration)
    {
        Vector3[] outerTargets = DiagonalTargets(CurrentActionRadius());
        Vector3[] oppositeOuterTargets = RotateTargets(outerTargets, 2);
        Vector3[] innerTargets = DiagonalTargets(CurrentCenterRadius());

        int cycleCount = Mathf.Max(1, sweepCount);
        int steps = cycleCount * 2;
        bool useOpposite = true;
        float totalPause = pauseBetweenDiagonals * Mathf.Max(0, steps - 1);
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

            if (stepIndex < steps && pauseBetweenDiagonals > 0f)
            {
                float pauseStart = moveEnd;
                timelineElapsed += pauseBetweenDiagonals;
                float pauseEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
                yield return WaitForSecondsWithCoreProgress(pauseBetweenDiagonals, pauseStart, pauseEnd);
            }

            targets = useOpposite ? oppositeOuterTargets : outerTargets;
            useOpposite = !useOpposite;

            moveStart = timelineDuration > 0f ? timelineElapsed / timelineDuration : 0f;
            timelineElapsed += moveDuration;
            moveEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
            yield return MoveActors(targets, moveDuration, 1f, moveStart, moveEnd);
            stepIndex++;

            if (stepIndex < steps && pauseBetweenDiagonals > 0f)
            {
                float pauseStart = moveEnd;
                timelineElapsed += pauseBetweenDiagonals;
                float pauseEnd = timelineDuration > 0f ? timelineElapsed / timelineDuration : 1f;
                yield return WaitForSecondsWithCoreProgress(pauseBetweenDiagonals, pauseStart, pauseEnd);
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
