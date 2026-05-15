using System;
using System.Collections.Generic;
using UnityEngine;

public static class AgilitySceneUtility
{
    private const string ArenaRootName = "MiniGame_Desk_02";
    private const string LegacyArenaRootName = "Board";
    private static readonly Dictionary<ComponentCacheKey, Component> ComponentCache = new();
    private static readonly Dictionary<string, Transform> TransformCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<Transform, float> RendererRadiusCache = new();
    private static readonly ArenaCardinal[] CardinalTemplate =
    {
        ArenaCardinal.North,
        ArenaCardinal.South,
        ArenaCardinal.East,
        ArenaCardinal.West
    };
    public static float SharedPieceGroundOffset { get; private set; }
    public static float SharedTelegraphSurfaceOffset { get; private set; }

    public static T FindInLoadedScene<T>(string objectName = null) where T : Component
    {
        var key = new ComponentCacheKey(typeof(T), objectName);
        if (ComponentCache.TryGetValue(key, out Component cached) && IsValidSceneObject(cached))
            return cached as T;

        foreach (var candidate in Resources.FindObjectsOfTypeAll<T>())
        {
            if (!IsValidSceneObject(candidate))
                continue;

            if (!string.IsNullOrEmpty(objectName) && candidate.gameObject.name != objectName)
                continue;

            ComponentCache[key] = candidate;
            return candidate;
        }

        ComponentCache.Remove(key);
        return null;
    }

    public static Transform FindTransform(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        if (TransformCache.TryGetValue(objectName, out Transform cached) && IsValidSceneObject(cached))
            return cached;

        foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!IsValidSceneObject(transform))
                continue;

            if (transform.name == objectName)
            {
                TransformCache[objectName] = transform;
                return transform;
            }
        }

        TransformCache.Remove(objectName);
        return null;
    }

    public static Vector3 ResolveArenaCenter(Transform explicitCenter = null)
    {
        if (explicitCenter != null)
            return explicitCenter.position;

        Transform arenaRoot = FindArenaRootTransform();
        if (arenaRoot != null)
            return arenaRoot.position;

        return Vector3.zero;
    }

    public static float ResolveArenaRadius(Transform explicitCenter = null, float fallbackRadius = 4.5f)
    {
        if (explicitCenter != null)
        {
            var explicitRadius = ResolveRendererRadiusCached(explicitCenter);
            if (explicitRadius > 0f)
                return explicitRadius;
        }

        Transform arenaRoot = FindArenaRootTransform();
        if (arenaRoot != null)
        {
            var arenaRadius = ResolveRendererRadiusCached(arenaRoot);
            if (arenaRadius > 0f)
                return arenaRadius;
        }

        return fallbackRadius;
    }

    public static Transform FindArenaRootTransform()
    {
        Transform arenaRoot = FindTransform(ArenaRootName);
        if (arenaRoot != null)
            return arenaRoot;

        return FindTransform(LegacyArenaRootName);
    }

    public static float ResolveTopY(Transform root, float fallbackY = 0f)
    {
        if (root != null && TryGetWorldBounds(root, out Bounds bounds))
            return bounds.max.y;

        return fallbackY;
    }

    public static float ResolveBottomOffset(Transform root, float fallbackOffset = 0f)
    {
        if (root != null && TryGetWorldBounds(root, out Bounds bounds))
            return bounds.min.y - root.position.y;

        return fallbackOffset;
    }

    public static float ResolveAlignedRootY(Transform root, float surfaceTopY, float fallbackRootY = 0f, float clearance = 0f)
    {
        if (root == null)
            return fallbackRootY + clearance;

        float bottomOffset = ResolveBottomOffset(root, 0f);
        return surfaceTopY - bottomOffset + clearance;
    }

    public static bool TryResolveEntranceSurfaceY(EntrancePoints entrancePoints, out float surfaceY)
    {
        surfaceY = 0f;
        if (entrancePoints == null)
            return false;

        float totalY = 0f;
        int count = 0;
        foreach (Entrance entrance in Enum.GetValues(typeof(Entrance)))
        {
            Transform point = entrancePoints.Get(entrance);
            if (point == null)
                continue;

            totalY += point.position.y;
            count++;
        }

        if (count == 0)
            return false;

        surfaceY = totalY / count;
        return true;
    }

    public static float ResolveArenaSurfaceY(
        EntrancePoints entrancePoints,
        Transform arenaTransform = null,
        Transform fallbackTransform = null,
        float fallbackY = 0f)
    {
        if (TryResolveEntranceSurfaceY(entrancePoints, out float entranceSurfaceY))
            return entranceSurfaceY;

        if (fallbackTransform != null)
            return fallbackTransform.position.y;

        if (arenaTransform != null)
            return arenaTransform.position.y;

        return fallbackY;
    }

    public static void ConfigureMiniGamePlacement(float pieceGroundOffset, float telegraphSurfaceOffset)
    {
        SharedPieceGroundOffset = pieceGroundOffset;
        SharedTelegraphSurfaceOffset = telegraphSurfaceOffset;
    }

    public static float ResolveTelegraphSurfaceY(
        EntrancePoints entrancePoints = null,
        Transform arenaTransform = null,
        float fallbackY = 0f)
    {
        float surfaceY = ResolveArenaSurfaceY(entrancePoints, arenaTransform, null, fallbackY);
        return surfaceY + SharedTelegraphSurfaceOffset;
    }

    public static bool TryGetWorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        bool hasBounds = false;

        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return hasBounds;
    }
    public static Vector3 CardinalPoint(Vector3 center, float radius, ArenaCardinal direction, float y)
    {
        Vector3 offset = direction switch
        {
            ArenaCardinal.North => Vector3.forward,
            ArenaCardinal.South => Vector3.back,
            ArenaCardinal.East => Vector3.right,
            ArenaCardinal.West => Vector3.left,
            _ => Vector3.forward
        };

        Vector3 point = center + offset * radius;
        point.y = y;
        return point;
    }

    public static Vector3 ClampToArena(Vector3 position, Vector3 center, float radius, float padding = 0f)
    {
        Vector3 planar = position - center;
        planar.y = 0f;

        float maxRadius = Mathf.Max(0f, radius - padding);
        if (planar.sqrMagnitude <= maxRadius * maxRadius)
            return position;

        Vector3 clamped = center + planar.normalized * maxRadius;
        clamped.y = position.y;
        return clamped;
    }

    public static void ShuffleCardinals(ArenaCardinal[] buffer, System.Random rng = null)
    {
        if (buffer == null || buffer.Length == 0)
            return;

        rng ??= new System.Random();
        int count = Mathf.Min(buffer.Length, CardinalTemplate.Length);
        for (int i = 0; i < count; i++)
            buffer[i] = CardinalTemplate[i];

        for (int i = count - 1; i > 0; i--)
        {
            int swapIndex = rng.Next(i + 1);
            (buffer[i], buffer[swapIndex]) = (buffer[swapIndex], buffer[i]);
        }
    }

    public static ArenaCardinal[] ShuffleCardinals(System.Random rng = null)
    {
        var result = new ArenaCardinal[CardinalTemplate.Length];
        ShuffleCardinals(result, rng);
        return result;
    }

    private static float ResolveRendererRadiusCached(Transform root)
    {
        if (root == null)
            return 0f;

        if (RendererRadiusCache.TryGetValue(root, out float cachedRadius) && IsValidSceneObject(root))
            return cachedRadius;

        float radius = ResolveRendererRadius(root);
        RendererRadiusCache[root] = radius;
        return radius;
    }

    private static float ResolveRendererRadius(Transform root)
    {
        if (!TryGetWorldBounds(root, out Bounds bounds))
            return 0f;

        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        return radius > 0.01f ? radius * 0.82f : 0f;
    }

    private static bool IsValidSceneObject(Component component)
    {
        return component != null && component.gameObject.scene.IsValid();
    }

    private readonly struct ComponentCacheKey : IEquatable<ComponentCacheKey>
    {
        public ComponentCacheKey(Type componentType, string objectName)
        {
            ComponentType = componentType;
            ObjectName = objectName ?? string.Empty;
        }

        public Type ComponentType { get; }
        public string ObjectName { get; }

        public bool Equals(ComponentCacheKey other)
        {
            return ComponentType == other.ComponentType && string.Equals(ObjectName, other.ObjectName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ComponentCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ComponentType != null ? ComponentType.GetHashCode() : 0) * 397) ^ StringComparer.Ordinal.GetHashCode(ObjectName);
            }
        }
    }
}

public enum ArenaCardinal
{
    North,
    South,
    East,
    West
}
