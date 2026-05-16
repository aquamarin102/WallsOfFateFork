using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaneSweepPattern : PatternBehaviour
{
    [Header("Lane Setup")]
    public float laneOffset = 1f;
    public float telegraphWidth = 1.1f;
    public bool doubleSweep = false;
    public float hazardRadius = 0.55f;
    public Color telegraphColor = new Color(1f, 0.76f, 0.2f);
    public Color activeColor = new Color(0.82f, 0.16f, 0.08f);

    private readonly List<GameObject> _telegraphs = new();
    private bool _plannedHorizontal;

    public override void BeginTelegraph()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        center.y = ResolveY();
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);

        _plannedHorizontal = Random.value > 0.5f;
        Vector3 crossDir = _plannedHorizontal ? Vector3.forward : Vector3.right;

        foreach (float offset in GetLaneOffsets())
        {
            Vector3 telegraphPosition = center + crossDir * offset;
            Vector3 telegraphScale = _plannedHorizontal
                ? new Vector3(radius * 2.2f, 0.1f, telegraphWidth)
                : new Vector3(telegraphWidth, 0.1f, radius * 2.2f);

            _telegraphs.Add(AgilityHazardFactory.CreateLaneTelegraph(transform, "LaneTelegraph", telegraphPosition, telegraphScale, telegraphColor));
        }
    }

    public override IEnumerator Run()
    {
        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        center.y = ResolveY();
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);

        Vector3 moveDir = _plannedHorizontal ? Vector3.right : Vector3.forward;
        Vector3 crossDir = _plannedHorizontal ? Vector3.forward : Vector3.right;

        CleanupTelegraphs();

        float[] offsets = GetLaneOffsets();
        float moveTime = Mathf.Max(0.5f, Definition.duration / Mathf.Max(1, offsets.Length));

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 laneCenter = center + crossDir * offsets[i];
            Vector3 start = laneCenter - moveDir * (radius + 1.1f);
            Vector3 end = laneCenter + moveDir * (radius + 1.1f);

            var hazard = AgilityHazardFactory.CreateBodyHazard(
                transform,
                $"LaneHazard_{i}",
                start,
                Vector3.one * (hazardRadius * 1.75f),
                activeColor);

            yield return MoveHazard(hazard.transform, start, end, moveTime);
        }
    }

    public override void Cleanup()
    {
        CleanupTelegraphs();
    }

    private float[] GetLaneOffsets()
    {
        return doubleSweep
            ? new[] { -laneOffset, laneOffset }
            : new[] { laneOffset };
    }

    private IEnumerator MoveHazard(Transform hazard, Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (hazard != null)
                hazard.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        if (hazard != null)
            hazard.position = to;
    }

    private void CleanupTelegraphs()
    {
        for (int i = 0; i < _telegraphs.Count; i++)
        {
            if (_telegraphs[i] != null)
                Destroy(_telegraphs[i]);
        }

        _telegraphs.Clear();
    }

    private float ResolveY()
    {
        if (Ctx != null)
            return Ctx.boardY + Ctx.hazardHeightOffset;

        return AgilitySceneUtility.ResolveArenaCenter(Ctx != null ? Ctx.arenaCenter : null).y + 0.5f;
    }
}
