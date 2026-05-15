using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EntranceVolleyPattern : PatternBehaviour
{
    public int shotsPerVolley = 3;
    public int maxBounces = 0;
    public bool rotateClockwise = true;
    public float projectileRadius = 0.23f;
    public float projectileSpeed = 8.5f;
    public Color telegraphColor = new Color(1f, 0.8f, 0.24f);
    public Color activeColor = new Color(0.88f, 0.25f, 0.08f);

    private readonly List<GameObject> _telegraphs = new();
    private readonly ArenaCardinal[] _plannedDirections = new ArenaCardinal[4];
    private bool _hasPlannedDirections;

    public override void BeginTelegraph()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);
        float y = ResolveY();
        AgilitySceneUtility.ShuffleCardinals(_plannedDirections);
        _hasPlannedDirections = true;

        int laneCount = Mathf.Clamp(shotsPerVolley, 2, 4);
        for (int i = 0; i < laneCount; i++)
        {
            Vector3 start = AgilitySceneUtility.CardinalPoint(center, radius + 0.3f, _plannedDirections[i], y);
            Vector3 end = center + (center - start).normalized * radius * 0.15f;
            Vector3 linePosition = Vector3.Lerp(start, end, 0.5f);
            Vector3 scale = Mathf.Abs(start.x - end.x) > Mathf.Abs(start.z - end.z)
                ? new Vector3(Vector3.Distance(start, end), 0.1f, 0.45f)
                : new Vector3(0.45f, 0.1f, Vector3.Distance(start, end));

            _telegraphs.Add(AgilityHazardFactory.CreateLaneTelegraph(transform, $"VolleyTelegraph_{i}", linePosition, scale, telegraphColor));
        }
    }

    public override IEnumerator Run()
    {
        CleanupTelegraphs();

        Vector3 center = AgilitySceneUtility.ResolveArenaCenter(Ctx.arenaCenter);
        float radius = AgilitySceneUtility.ResolveArenaRadius(Ctx.arenaCenter);
        float y = ResolveY();
        if (!_hasPlannedDirections)
        {
            AgilitySceneUtility.ShuffleCardinals(_plannedDirections);
            _hasPlannedDirections = true;
        }

        int laneCount = Mathf.Clamp(shotsPerVolley, 2, 4);
        float interval = Mathf.Max(0.18f, Definition.duration / Mathf.Max(2f, laneCount + 1f));
        int rotationStep = rotateClockwise ? 1 : -1;

        for (int i = 0; i < laneCount; i++)
        {
            int directionIndex = (i * rotationStep + _plannedDirections.Length * 4) % _plannedDirections.Length;
            ArenaCardinal cardinal = _plannedDirections[directionIndex];
            Vector3 spawn = AgilitySceneUtility.CardinalPoint(center, radius + 0.35f, cardinal, y);
            Vector3 direction = (center - spawn).normalized;

            var projectile = AgilityHazardFactory.CreateProjectile(transform, $"Plate_{i}", spawn, projectileRadius, activeColor);
            var mover = projectile.AddComponent<SimpleProjectileHazard>();
            mover.Launch(direction, projectileSpeed, center, radius, 0.12f, maxBounces, Definition.duration + 1.2f);

            yield return new WaitForSeconds(interval);
        }
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

        return AgilitySceneUtility.ResolveArenaCenter(Ctx != null ? Ctx.arenaCenter : null).y + 0.6f;
    }
}
