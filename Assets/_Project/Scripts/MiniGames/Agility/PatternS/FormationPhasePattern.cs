using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class FormationPhasePattern : PatternBehaviour
{
    [Header("Formation")]
    public float formationRadius = 1.55f;
    public float entryPortion = 0.22f;
    public float exitPortion = 0.18f;
    public float movementLerp = 1f;
    //public float actorHeightOffset = 0.75f;
    public float actorHeightOffset = 0;
    public Color telegraphColor = new Color(1f, 0.78f, 0.22f, 0.75f);

    [Header("Motion Feel")]
    public float preMoveTurnDuration = 0.11f;
    public float preMoveHoldDuration = 0.03f;

    [Header("Rim Routing")]
    public bool routeEntryAlongRim = true;
    public bool routeExitAlongRim = true;
    public float rimPathPadding = 0.45f;
    [Range(0.45f, 0.9f)] public float rimRadiusFactor = 0.72f;
    [Range(0.35f, 1f)] public float rimMovementLerpMultiplier = 0.72f;
    [Range(0.25f, 0.8f)] public float rimArcPortion = 0.58f;

    protected readonly List<GameObject> Telegraphs = new();
    protected readonly List<FormationActorSlot> Actors = new();

    protected Vector3 Center;
    protected float ArenaRadius;
    protected float GroundY;

    protected virtual Entrance[] SlotEntrances => new[]
    {
        Entrance.NorthEast,
        Entrance.NorthWest,
        Entrance.SouthEast,
        Entrance.SouthWest
    };

    public override void BeginTelegraph()
    {
        ResolveArena();
        CleanupTelegraphs();
        CreateTelegraphs();
    }

    public override IEnumerator Run()
    {
        ResolveArena();
        CleanupTelegraphs();
        EnsureActors();
        yield return PlayGateAnimation(open: true);

        float entryDuration = Mathf.Clamp(Definition.duration * entryPortion, 0.2f, 1.1f);
        float exitDuration = Mathf.Clamp(Definition.duration * exitPortion, 0.2f, 1f);
        float activeDuration = Mathf.Max(0.6f, Definition.duration - entryDuration - exitDuration);

        IReadOnlyList<Vector3> entryTargets = BuildEntryTargets();
        if (routeEntryAlongRim)
            yield return MoveActorsViaRim(entryTargets, entryDuration, exiting: false);
        else
            yield return MoveActors(entryTargets, entryDuration);

        if (Ctx?.runner is PatternSequencer sequencer)
            sequencer.NotifyPatternCoreStarted();

        yield return ExecuteFormation(activeDuration);

        if (Ctx?.runner is PatternSequencer completedSequencer)
            completedSequencer.NotifyPatternCoreCompleted();

        IReadOnlyList<Vector3> exitTargets = BuildExitTargets();
        if (routeExitAlongRim)
            yield return MoveActorsViaRim(exitTargets, exitDuration, exiting: true);
        else
            yield return MoveActors(exitTargets, exitDuration);

        yield return PlayGateAnimation(open: false);
    }

    public override void Cleanup()
    {
        CleanupTelegraphs();
        CleanupActors();
    }

    protected abstract void CreateTelegraphs();
    protected abstract IReadOnlyList<Vector3> BuildEntryTargets();
    protected abstract IEnumerator ExecuteFormation(float activeDuration);

    protected Vector3 WithGroundY(Vector3 point)
    {
        point.y = GroundY;
        return point;
    }

    protected Vector3 PointOnRing(float angleDeg, float distance)
    {
        float angle = angleDeg * Mathf.Deg2Rad;
        return WithGroundY(Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance);
    }

    protected Vector3[] CardinalTargets(float distance)
    {
        return new[]
        {
            PointOnRing(0f, distance),
            PointOnRing(90f, distance),
            PointOnRing(-90f, distance),
            PointOnRing(180f, distance)
        };
    }

    protected Vector3[] DiagonalTargets(float distance)
    {
        return new[]
        {
            PointOnRing(45f, distance),
            PointOnRing(135f, distance),
            PointOnRing(-45f, distance),
            PointOnRing(-135f, distance)
        };
    }

    protected void AddTelegraphZone(Vector3 position, float radius)
    {
        var telegraph = AgilityHazardFactory.CreateTelegraphZone(transform, "FormationTelegraph", position, radius, telegraphColor);
        Telegraphs.Add(telegraph);
    }

    protected void AddTelegraphLine(Vector3 position, Vector3 size, float yRotation)
    {
        var telegraph = AgilityHazardFactory.CreateLaneTelegraph(transform, "FormationLineTelegraph", position, size, telegraphColor);
        telegraph.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        Telegraphs.Add(telegraph);
    }

    protected IEnumerator MoveActors(
        IReadOnlyList<Vector3> targets,
        float duration,
        float speedScale = 1f,
        float progressStart = -1f,
        float progressEnd = -1f)
    {
        if (Actors.Count == 0 || targets == null || targets.Count == 0)
            yield break;

        duration = Mathf.Max(0.08f, duration);
        float effectiveMovementLerp = Mathf.Max(0.1f, movementLerp * Mathf.Max(0.1f, speedScale));
        bool trackProgress = progressStart >= 0f && progressEnd >= 0f;
        var startPositions = new Vector3[Actors.Count];
        var startRotations = new Quaternion[Actors.Count];
        var targetRotations = new Quaternion[Actors.Count];

        for (int i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i].actor;
            if (actor == null)
                continue;

            startPositions[i] = actor.transform.position;
            startRotations[i] = actor.transform.rotation;

            Vector3 target = targets[Mathf.Min(i, targets.Count - 1)];
            targetRotations[i] = ResolveTargetRotation(startPositions[i], target, actor.transform.rotation);
        }

        float turnDuration = Mathf.Min(preMoveTurnDuration, duration * 0.32f);
        float holdDuration = Mathf.Min(preMoveHoldDuration, Mathf.Max(0f, duration * 0.12f));
        float maxNonMoveDuration = Mathf.Max(0f, duration - 0.05f);
        if (turnDuration + holdDuration > maxNonMoveDuration)
            holdDuration = Mathf.Max(0f, maxNonMoveDuration - turnDuration);

        float moveDuration = Mathf.Max(0.05f, duration - turnDuration - holdDuration);

        if (turnDuration > 0.001f)
        {
            float turnElapsed = 0f;
            while (turnElapsed < turnDuration)
            {
                turnElapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(turnElapsed / turnDuration);
                float t = Mathf.SmoothStep(0f, 1f, rawT);

                for (int i = 0; i < Actors.Count; i++)
                {
                    var actor = Actors[i].actor;
                    if (actor == null)
                        continue;

                    actor.transform.position = startPositions[i];
                    actor.transform.rotation = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
                }

                if (trackProgress)
                    ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01(turnElapsed / duration)));

                yield return null;
            }
        }

        if (holdDuration > 0.001f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < holdDuration)
            {
                holdElapsed += Time.deltaTime;

                for (int i = 0; i < Actors.Count; i++)
                {
                    var actor = Actors[i].actor;
                    if (actor == null)
                        continue;

                    actor.transform.position = startPositions[i];
                    actor.transform.rotation = targetRotations[i];
                }

                if (trackProgress)
                    ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01((turnDuration + holdElapsed) / duration)));

                yield return null;
            }
        }

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime * effectiveMovementLerp;
            float rawT = Mathf.Clamp01(elapsed / moveDuration);
            float t = Mathf.SmoothStep(0f, 1f, rawT);

            for (int i = 0; i < Actors.Count; i++)
            {
                var actor = Actors[i].actor;
                if (actor == null)
                    continue;

                Vector3 target = targets[Mathf.Min(i, targets.Count - 1)];
                Vector3 next = Vector3.Lerp(startPositions[i], target, t);
                actor.transform.position = next;
                actor.transform.rotation = targetRotations[i];
            }

            if (trackProgress)
                ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01((turnDuration + holdDuration + elapsed) / duration)));

            yield return null;
        }

        for (int i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i].actor;
            if (actor == null)
                continue;

            Vector3 target = targets[Mathf.Min(i, targets.Count - 1)];
            actor.transform.position = target;
            actor.transform.rotation = targetRotations[i];
        }

        if (trackProgress)
            ReportCoreProgress(progressEnd);
    }

    protected IEnumerator MoveActorsViaRim(IReadOnlyList<Vector3> targets, float duration, bool exiting)
    {
        if (Actors.Count == 0 || targets == null || targets.Count == 0)
            yield break;

        float rimRadius = CurrentRimRadius();
        float arcPortion = Mathf.Clamp(rimArcPortion, 0.25f, 0.8f);
        float sidePortion = (1f - arcPortion) * 0.5f;
        float firstDuration = Mathf.Max(0.06f, duration * sidePortion);
        float arcDuration = Mathf.Max(0.1f, duration * arcPortion);
        float lastDuration = Mathf.Max(0.06f, duration - firstDuration - arcDuration);

        var firstTargets = new Vector3[Actors.Count];
        var rimAnglesFrom = new float[Actors.Count];
        var rimAnglesTo = new float[Actors.Count];

        for (int i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i].actor;
            if (actor == null)
                continue;

            Vector3 gatePoint = Actors[i].gatePoint != null ? Actors[i].gatePoint.position : actor.transform.position;
            gatePoint = WithGroundY(gatePoint);

            Vector3 currentPosition = actor.transform.position;
            Vector3 finalTarget = targets[Mathf.Min(i, targets.Count - 1)];

            float startAngle = exiting ? AngleFromCenter(currentPosition) : AngleFromCenter(gatePoint);
            float endAngle = exiting ? AngleFromCenter(gatePoint) : AngleFromCenter(finalTarget);

            rimAnglesFrom[i] = startAngle;
            rimAnglesTo[i] = endAngle;
            firstTargets[i] = PointOnRing(startAngle, rimRadius);
        }

        float rimSpeedScale = Mathf.Clamp(rimMovementLerpMultiplier, 0.35f, 1f);
        yield return MoveActors(firstTargets, firstDuration, rimSpeedScale);
        yield return MoveActorsAlongArc(rimAnglesFrom, rimAnglesTo, rimRadius, arcDuration, rimSpeedScale);
        yield return MoveActors(targets, lastDuration, rimSpeedScale);
    }

    protected Vector3[] RotateTargets(Vector3[] source, int shift)
    {
        var rotated = new Vector3[source.Length];
        for (int i = 0; i < source.Length; i++)
            rotated[i] = source[(i + shift + source.Length) % source.Length];
        return rotated;
    }

    protected float StepDuration(float totalDuration, int moveCount, float minStep = 0.18f)
    {
        return Mathf.Max(minStep, totalDuration / Mathf.Max(1, moveCount));
    }

    protected IEnumerator WaitForSecondsWithCoreProgress(float seconds, float progressStart, float progressEnd)
    {
        float safeSeconds = Mathf.Max(0f, seconds);
        if (safeSeconds <= 0.0001f)
        {
            ReportCoreProgress(progressEnd);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < safeSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeSeconds);
            ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, t));
            yield return null;
        }

        ReportCoreProgress(progressEnd);
    }

    private void ResolveArena()
    {
        Center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        ArenaRadius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);

        Transform board = Ctx.arenaCenter != null ? Ctx.arenaCenter : AgilitySceneUtility.FindTransform("Board");
        GroundY = AgilitySceneUtility.ResolveTopY(board, Center.y) + actorHeightOffset;
        Center.y = GroundY;
    }

    private void EnsureActors()
    {
        if (Actors.Count > 0)
            return;

        if (Ctx.entrances == null)
            return;

        foreach (Entrance entrance in SlotEntrances)
        {
            Transform gatePoint = Ctx.entrances.Get(entrance);
            if (gatePoint == null)
                continue;

            Vector3 spawnPosition = gatePoint.position;
            spawnPosition.y = GroundY;

            GameObject actor = AgilityHazardFactory.CreateChickenHazard(transform, $"ThreatActor_{entrance}", spawnPosition);
            //actor.transform.SetParent(transform, worldPositionStays: true);
            //actor.transform.position = spawnPosition;

            Actors.Add(new FormationActorSlot
            {
                entrance = entrance,
                gatePoint = gatePoint,
                gateController = ResolveGateController(gatePoint),
                actor = actor
            });
        }
    }

    private IReadOnlyList<Vector3> BuildExitTargets()
    {
        var targets = new List<Vector3>(Actors.Count);
        for (int i = 0; i < Actors.Count; i++)
        {
            Vector3 point = Actors[i].gatePoint != null ? Actors[i].gatePoint.position : Center;
            point.y = GroundY;
            targets.Add(point);
        }

        return targets;
    }

    private IEnumerator PlayGateAnimation(bool open)
    {
        float wait = 0f;
        for (int i = 0; i < Actors.Count; i++)
        {
            GateController gate = Actors[i].gateController;
            if (gate?.animator == null)
                continue;

            gate.animator.SetTrigger(open ? gate.openTrigger : gate.closeTrigger);
            wait = Mathf.Max(wait, open ? gate.openDelay : gate.closeDelay);
        }

        if (wait > 0f)
            yield return new WaitForSeconds(wait);
    }

    private GateController ResolveGateController(Transform gatePoint)
    {
        if (gatePoint == null)
            return null;

        return gatePoint.GetComponent<GateController>()
               ?? gatePoint.GetComponentInParent<GateController>()
               ?? gatePoint.GetComponentInChildren<GateController>();
    }

    private void RotateActor(Transform actor, Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return;

        actor.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private Quaternion ResolveTargetRotation(Vector3 from, Vector3 to, Quaternion fallback)
    {
        Vector3 forward = to - from;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return fallback;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    protected IEnumerator MoveActorsAlongArc(
        float[] startAngles,
        float[] endAngles,
        float radius,
        float duration,
        float speedScale,
        float progressStart = -1f,
        float progressEnd = -1f)
    {
        if (Actors.Count == 0)
            yield break;

        duration = Mathf.Max(0.08f, duration);
        float effectiveMovementLerp = Mathf.Max(0.1f, movementLerp * Mathf.Max(0.1f, speedScale));
        bool trackProgress = progressStart >= 0f && progressEnd >= 0f;
        var startPositions = new Vector3[Actors.Count];
        var startRotations = new Quaternion[Actors.Count];
        var targetRotations = new Quaternion[Actors.Count];
        var angleDirections = new float[Actors.Count];

        for (int i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i].actor;
            if (actor == null)
                continue;

            float startAngle = startAngles[Mathf.Min(i, startAngles.Length - 1)];
            float endAngle = endAngles[Mathf.Min(i, endAngles.Length - 1)];
            float deltaAngle = Mathf.DeltaAngle(startAngle, endAngle);
            float directionSign = Mathf.Abs(deltaAngle) <= 0.01f ? 1f : Mathf.Sign(deltaAngle);

            startPositions[i] = actor.transform.position;
            startRotations[i] = actor.transform.rotation;
            angleDirections[i] = directionSign;
            targetRotations[i] = ResolveLookRotation(TangentOnRing(startAngle, directionSign), actor.transform.rotation);
        }

        float turnDuration = Mathf.Min(preMoveTurnDuration, duration * 0.32f);
        float holdDuration = Mathf.Min(preMoveHoldDuration, Mathf.Max(0f, duration * 0.12f));
        float maxNonMoveDuration = Mathf.Max(0f, duration - 0.05f);
        if (turnDuration + holdDuration > maxNonMoveDuration)
            holdDuration = Mathf.Max(0f, maxNonMoveDuration - turnDuration);

        float moveDuration = Mathf.Max(0.05f, duration - turnDuration - holdDuration);

        if (turnDuration > 0.001f)
        {
            float turnElapsed = 0f;
            while (turnElapsed < turnDuration)
            {
                turnElapsed += Time.deltaTime;
                float rawT = Mathf.Clamp01(turnElapsed / turnDuration);
                float t = Mathf.SmoothStep(0f, 1f, rawT);

                for (int i = 0; i < Actors.Count; i++)
                {
                    var actor = Actors[i].actor;
                    if (actor == null)
                        continue;

                    actor.transform.position = startPositions[i];
                    actor.transform.rotation = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
                }

                if (trackProgress)
                    ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01(turnElapsed / duration)));

                yield return null;
            }
        }

        if (holdDuration > 0.001f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < holdDuration)
            {
                holdElapsed += Time.deltaTime;

                for (int i = 0; i < Actors.Count; i++)
                {
                    var actor = Actors[i].actor;
                    if (actor == null)
                        continue;

                    actor.transform.position = startPositions[i];
                    actor.transform.rotation = targetRotations[i];
                }

                if (trackProgress)
                    ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01((turnDuration + holdElapsed) / duration)));

                yield return null;
            }
        }

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime * effectiveMovementLerp;
            float rawT = Mathf.Clamp01(elapsed / moveDuration);
            float t = Mathf.SmoothStep(0f, 1f, rawT);

            for (int i = 0; i < Actors.Count; i++)
            {
                var actor = Actors[i].actor;
                if (actor == null)
                    continue;

                float startAngle = startAngles[Mathf.Min(i, startAngles.Length - 1)];
                float endAngle = endAngles[Mathf.Min(i, endAngles.Length - 1)];
                float currentAngle = Mathf.LerpAngle(startAngle, endAngle, t);
                Vector3 point = PointOnRing(currentAngle, radius);
                Vector3 tangent = TangentOnRing(currentAngle, angleDirections[i]);

                actor.transform.position = point;
                actor.transform.rotation = ResolveLookRotation(tangent, targetRotations[i]);
            }

            if (trackProgress)
                ReportCoreProgress(Mathf.Lerp(progressStart, progressEnd, Mathf.Clamp01((turnDuration + holdDuration + elapsed) / duration)));

            yield return null;
        }

        for (int i = 0; i < Actors.Count; i++)
        {
            var actor = Actors[i].actor;
            if (actor == null)
                continue;

            float endAngle = endAngles[Mathf.Min(i, endAngles.Length - 1)];
            actor.transform.position = PointOnRing(endAngle, radius);
            actor.transform.rotation = ResolveLookRotation(TangentOnRing(endAngle, angleDirections[i]), actor.transform.rotation);
        }

        if (trackProgress)
            ReportCoreProgress(progressEnd);
    }

    private float CurrentRimRadius()
    {
        float scaledRadius = ArenaRadius * rimRadiusFactor;
        return Mathf.Max(0.8f, scaledRadius - rimPathPadding);
    }

    protected float AngleFromCenter(Vector3 point)
    {
        Vector3 offset = point - Center;
        offset.y = 0f;
        if (offset.sqrMagnitude <= 0.0001f)
            return 0f;

        return Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
    }

    private Vector3 TangentOnRing(float angleDeg, float directionSign)
    {
        float radians = angleDeg * Mathf.Deg2Rad;
        Vector3 tangent = new Vector3(-Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        if (directionSign < 0f)
            tangent = -tangent;

        return tangent;
    }

    private Quaternion ResolveLookRotation(Vector3 forward, Quaternion fallback)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return fallback;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void ReportCoreProgress(float normalized)
    {
        if (Ctx?.runner is PatternSequencer sequencer)
            sequencer.NotifyPatternCoreProgress(normalized);
    }

    private void CleanupTelegraphs()
    {
        for (int i = 0; i < Telegraphs.Count; i++)
        {
            if (Telegraphs[i] != null)
                Object.Destroy(Telegraphs[i]);
        }

        Telegraphs.Clear();
    }

    private void CleanupActors()
    {
        for (int i = 0; i < Actors.Count; i++)
        {
            if (Actors[i].actor != null)
                Object.Destroy(Actors[i].actor);
        }

        Actors.Clear();
    }

    protected sealed class FormationActorSlot
    {
        public Entrance entrance;
        public Transform gatePoint;
        public GateController gateController;
        public GameObject actor;
    }
}
