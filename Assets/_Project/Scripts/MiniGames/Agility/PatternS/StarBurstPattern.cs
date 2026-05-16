using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StarBurstPattern : PatternBehaviour
{
    public int starPoints = 5;
    public float starRadius = 1.9f;
    public float blockerSize = 0.7f;
    public float projectileSpeed = 9f;
    public int projectileCount = 8;
    public Color telegraphColor = new Color(1f, 0.84f, 0.28f);
    public Color blockerColor = new Color(0.7f, 0.18f, 0.1f);
    public Color projectileColor = new Color(0.95f, 0.2f, 0.06f);

    private readonly List<GameObject> _telegraphs = new();

    public override void BeginTelegraph()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float y = ResolveY();
        center.y = y;

        for (int i = 0; i < starPoints; i++)
        {
            float angle = Mathf.PI * 2f * i / starPoints;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * starRadius;
            _telegraphs.Add(AgilityHazardFactory.CreateZone(transform, $"StarTelegraph_{i}", point, blockerSize * 0.75f, telegraphColor));
        }

        Vector3 centerZone = center;
        centerZone.y = y - 0.45f;
        _telegraphs.Add(AgilityHazardFactory.CreateZone(transform, "CenterBurstTelegraph", centerZone, 1f, telegraphColor));
    }

    public override IEnumerator Run()
    {
        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float arenaRadius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);
        float y = ResolveY();
        center.y = y;

        CleanupTelegraphs();

        for (int i = 0; i < starPoints; i++)
        {
            float angle = Mathf.PI * 2f * i / starPoints;
            Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * starRadius;
            var blocker = AgilityHazardFactory.CreateBodyHazard(
                transform,
                $"StarBlocker_{i}",
                point,
                Vector3.one * blockerSize,
                blockerColor);

            blocker.transform.position = point;
        }

        yield return new WaitForSeconds(Mathf.Max(0.35f, Definition.duration * 0.25f));

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = Mathf.PI * 2f * i / projectileCount;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            var projectile = AgilityHazardFactory.CreateProjectile(transform, $"StarProjectile_{i}", center, 0.22f, projectileColor);
            var mover = projectile.AddComponent<SimpleProjectileHazard>();
            mover.Launch(direction, projectileSpeed, center, arenaRadius, 0.15f, 0, 2.2f);
        }

        yield return new WaitForSeconds(Mathf.Max(0.45f, Definition.duration * 0.45f));
    }

    public override void Cleanup()
    {
        CleanupTelegraphs();
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
