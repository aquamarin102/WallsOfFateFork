using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpillZonesPattern : PatternBehaviour
{
    public SurfaceHazardZone.ZoneEffect zoneEffect = SurfaceHazardZone.ZoneEffect.Slow;
    public float zoneRadius = 1.05f;
    public float laneHazardWidth = 0.85f;
    public Color telegraphColor = new Color(1f, 0.8f, 0.25f);
    public Color zoneColor = new Color(0.52f, 0.16f, 0.06f);
    public Color sweepColor = new Color(0.82f, 0.14f, 0.08f);

    private readonly List<GameObject> _telegraphs = new();

    public override void BeginTelegraph()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);
        float y = ResolveY();

        Vector3 leftZone = center + new Vector3(-radius * 0.45f, 0f, radius * 0.35f);
        Vector3 rightZone = center + new Vector3(radius * 0.45f, 0f, -radius * 0.35f);
        leftZone.y = y - 0.45f;
        rightZone.y = y - 0.45f;

        _telegraphs.Add(AgilityHazardFactory.CreateZone(transform, "SpillTelegraphA", leftZone, zoneRadius, telegraphColor));
        _telegraphs.Add(AgilityHazardFactory.CreateZone(transform, "SpillTelegraphB", rightZone, zoneRadius, telegraphColor));
    }

    public override IEnumerator Run()
    {
        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);
        float y = ResolveY();

        CleanupTelegraphs();

        Vector3 leftZone = center + new Vector3(-radius * 0.45f, 0f, radius * 0.35f);
        Vector3 rightZone = center + new Vector3(radius * 0.45f, 0f, -radius * 0.35f);
        leftZone.y = y - 0.45f;
        rightZone.y = y - 0.45f;

        SpawnZone(leftZone, "SpillA");
        SpawnZone(rightZone, "SpillB");

        yield return new WaitForSeconds(Mathf.Max(0.3f, Definition.duration * 0.35f));

        Vector3 start = center + Vector3.left * (radius + 1f);
        Vector3 end = center + Vector3.right * (radius + 1f);
        start.y = y;
        end.y = y;

        var hazard = AgilityHazardFactory.CreateBodyHazard(
            transform,
            "SpillSweep",
            start,
            new Vector3(laneHazardWidth, laneHazardWidth, laneHazardWidth),
            sweepColor);

        yield return MoveHazard(hazard.transform, start, end, Mathf.Max(0.7f, Definition.duration * 0.45f));
    }

    public override void Cleanup()
    {
        CleanupTelegraphs();
    }

    private void SpawnZone(Vector3 position, string zoneName)
    {
        var zone = AgilityHazardFactory.CreateZone(transform, zoneName, position, zoneRadius, zoneColor);
        var effect = zone.AddComponent<SurfaceHazardZone>();
        effect.Configure(zoneEffect, Definition.duration * 0.65f, 0.65f, 0.55f);
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
            return Ctx.boardY + Ctx.hazardHeightOffset + 0.2f;

        return AgilitySceneUtility.ResolveArenaCenter(Ctx != null ? Ctx.arenaCenter : null).y + 0.5f;
    }
}
