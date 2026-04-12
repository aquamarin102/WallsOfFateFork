using UnityEngine;

public static class AgilityHazardFactory
{
    private const string PatternActorPrefabPath = "MiniGames/Agility/figurka_magnat";
    private const string ChickenPrefabPath = "MiniGames/Agility/Enemies/Chicken";
    private static GameObject _patternActorPrefabOverride;

    public static void SetPatternActorPrefab(GameObject prefab)
    {
        _patternActorPrefabOverride = prefab;
    }

    public static GameObject CreateBodyHazard(Transform parent, string name, Vector3 position, Vector3 scale, Color color, int damage = 1)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        ConfigureVisual(go, color);

        var collider = go.GetComponent<Collider>();
        collider.isTrigger = true;

        var damageOnTouch = go.AddComponent<DamageOnTouch>();
        damageOnTouch.damage = damage;
        damageOnTouch.requireTrigger = true;
        return go;
    }

    public static GameObject CreateBoxHazard(Transform parent, string name, Vector3 position, Vector3 size, Color color, int damage = 1)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = size;

        ConfigureVisual(go, color);

        var collider = go.GetComponent<Collider>();
        collider.isTrigger = true;

        var damageOnTouch = go.AddComponent<DamageOnTouch>();
        damageOnTouch.damage = damage;
        damageOnTouch.requireTrigger = true;
        return go;
    }

    public static GameObject CreateLaneTelegraph(Transform parent, string name, Vector3 position, Vector3 size, Color color)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        float verticalOffset = -Mathf.Max(0.04f, size.y * 0.45f) - 1.43f;
        root.transform.position = position + Vector3.up * verticalOffset;
        root.transform.rotation = Quaternion.identity;

        bool alongX = size.x >= size.z;
        float lineLength = Mathf.Max(size.x, size.z);
        float baseWidth = Mathf.Min(size.x, size.z);
        float dashWidth = Mathf.Max(0.06f, baseWidth * 0.35f);
        float dashHeight = Mathf.Max(0.03f, size.y * 0.65f);
        int dashCount = Mathf.Clamp(Mathf.RoundToInt(lineLength / 0.65f), 4, 14);
        float slotLength = lineLength / Mathf.Max(1, dashCount);
        float dashLength = Mathf.Max(0.14f, slotLength * 0.56f);
        float startOffset = -lineLength * 0.5f + slotLength * 0.5f;

        for (int i = 0; i < dashCount; i++)
        {
            var dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dash.name = $"Dash_{i}";
            dash.transform.SetParent(root.transform, false);

            float axisOffset = startOffset + slotLength * i;
            dash.transform.localPosition = alongX
                ? new Vector3(axisOffset, 0f, 0f)
                : new Vector3(0f, 0f, axisOffset);
            dash.transform.localScale = alongX
                ? new Vector3(dashLength, dashHeight, dashWidth)
                : new Vector3(dashWidth, dashHeight, dashLength);

            Object.Destroy(dash.GetComponent<Collider>());
            ConfigureVisual(dash, color);
        }

        return root;
    }

    public static GameObject CreateTelegraphZone(Transform parent, string name, Vector3 position, float radius, Color color)
    {
        var go = CreateZone(parent, name, position, radius, color);
        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.Destroy(collider);
        return go;
    }

    public static GameObject CreatePatternActor(Transform parent, string name, Vector3 position, int damage = 1)
    {
        return CreateVisualActor(parent, name, position, ResolvePatternActorPrefab(), damage, 0.22f, 0.9f, new Vector3(0f, 0.45f, 0f), solidBody: false);
    }

    public static GameObject CreateChickenHazard(Transform parent, string name, Vector3 position, int damage = 1)
    {
        var actor = CreateVisualActor(parent, name, position, ResolveChickenPrefab(), damage, 0.5f, 2.5f, new Vector3(0f, 0f, 0f), solidBody: false);
        var repel = actor.AddComponent<SoftRepelOnTouch>();
        repel.hideFlags = HideFlags.None;
        return actor;
    }

    private static GameObject CreateVisualActor(
        Transform parent,
        string name,
        Vector3 position,
        GameObject visualPrefab,
        int damage,
        float capsuleRadius,
        float capsuleHeight,
        Vector3 capsuleCenter,
        bool solidBody)
    {
        var actor = new GameObject(name);
        actor.transform.SetParent(parent, false);
        actor.transform.position = position;
        actor.transform.rotation = Quaternion.identity;

        var capsule = actor.AddComponent<CapsuleCollider>();
        capsule.isTrigger = !solidBody;
        capsule.radius = capsuleRadius;
        capsule.height = capsuleHeight;
        capsule.center = capsuleCenter;

        CapsuleCollider damageCapsule = capsule;
        if (solidBody)
        {
            var rigidbody = actor.GetComponent<Rigidbody>();
            if (rigidbody == null)
                rigidbody = actor.AddComponent<Rigidbody>();

            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var damageTrigger = new GameObject("DamageTrigger");
            damageTrigger.transform.SetParent(actor.transform, false);
            damageCapsule = damageTrigger.AddComponent<CapsuleCollider>();
            damageCapsule.isTrigger = true;
        }

        var damageOnTouch = damageCapsule.gameObject.AddComponent<DamageOnTouch>();
        damageOnTouch.damage = damage;
        damageOnTouch.requireTrigger = true;

        AttachVisual(actor.transform, capsule, visualPrefab);
        CopyCapsuleShape(capsule, damageCapsule);
        return actor;
    }

    public static GameObject CreateZone(Transform parent, string name, Vector3 position, float radius, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = new Vector3(radius * 2f, 0.1f, radius * 2f);

        var collider = go.GetComponent<Collider>();
        collider.isTrigger = true;

        ConfigureVisual(go, color);
        return go;
    }

    public static GameObject CreateProjectile(Transform parent, string name, Vector3 position, float radius, Color color, int damage = 1)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * (radius * 2f);

        var collider = go.GetComponent<Collider>();
        collider.isTrigger = true;

        var damageOnTouch = go.AddComponent<DamageOnTouch>();
        damageOnTouch.damage = damage;
        damageOnTouch.requireTrigger = true;
        damageOnTouch.enabled = false;

        ConfigureVisual(go, color);
        return go;
    }

    public static void ConfigureVisual(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null)
            return;

        renderer.material = new Material(renderer.sharedMaterial);
        renderer.material.color = color;
    }

    public static void SetColor(GameObject go, Color color)
    {
        if (go == null)
            return;

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = color;
    }

    private static void AttachVisual(Transform actorRoot, CapsuleCollider capsule, GameObject visualPrefab)
    {
        if (visualPrefab == null)
        {
            var fallback = CreateBodyHazard(actorRoot, "ActorFallback", actorRoot.position + Vector3.up * 0.45f, new Vector3(0.4f, 0.9f, 0.4f), new Color(0.76f, 0.2f, 0.08f));
            var fallbackCollider = fallback.GetComponent<Collider>();
            if (fallbackCollider != null)
                Object.Destroy(fallbackCollider);
            var fallbackDamage = fallback.GetComponent<DamageOnTouch>();
            if (fallbackDamage != null)
                Object.Destroy(fallbackDamage);
            return;
        }

        var visual = Object.Instantiate(visualPrefab, actorRoot);
        visual.name = "Visual";

        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;

        foreach (var rigidbody in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true;
            Object.Destroy(rigidbody);
        }

        foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            Object.Destroy(collider);
        }

        //AlignVisualToGround(actorRoot, visual.transform, capsule);
    }

    private static void AlignVisualToGround(Transform actorRoot, Transform visualRoot, CapsuleCollider capsule)
    {
        if (!AgilitySceneUtility.TryGetWorldBounds(visualRoot, out Bounds bounds))
            return;

        float offset = actorRoot.position.y - bounds.min.y;
        visualRoot.position += Vector3.up * offset;

        if (!AgilitySceneUtility.TryGetWorldBounds(visualRoot, out bounds))
            return;

        Vector3 centerLocal = actorRoot.InverseTransformPoint(bounds.center);
        float radius = Mathf.Max(0.12f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.42f);
        float height = Mathf.Max(radius * 2f, bounds.size.y * 0.92f);

        capsule.center = centerLocal;
        capsule.radius = radius;
        capsule.height = height;
    }

    private static void CopyCapsuleShape(CapsuleCollider source, CapsuleCollider target)
    {
        if (source == null || target == null || ReferenceEquals(source, target))
            return;

        target.direction = source.direction;
        target.center = source.center;
        target.radius = source.radius;
        target.height = source.height;
    }

    private static GameObject ResolvePatternActorPrefab()
    {
        if (_patternActorPrefabOverride != null)
            return _patternActorPrefabOverride;

        return Resources.Load<GameObject>(PatternActorPrefabPath);
    }

    private static GameObject ResolveChickenPrefab()
    {
        return Resources.Load<GameObject>(ChickenPrefabPath);
    }
}
