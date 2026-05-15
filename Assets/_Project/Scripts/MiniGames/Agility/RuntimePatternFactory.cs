using System.Collections.Generic;
using UnityEngine;

public static class RuntimePatternFactory
{
    public static List<PatternDefinition> Build(Transform root)
    {
        EnsureCleanRoot(root);

        var patterns = new List<PatternDefinition>(3);
        patterns.Add(BuildPlus(root));
        patterns.Add(BuildCross(root));
        patterns.Add(BuildOrbit(root));
        return patterns;
    }

    private static PatternDefinition BuildPlus(Transform root)
    {
        var template = CreateTemplate<CrossThenPlusPattern>(root, "Runtime_CrossThenPlus_A");
        template.actorHeightOffset = 0.02f;
        template.entryPortion = 0.24f;
        template.exitPortion = 0.2f;
        template.movementLerp = 1.55f;
        template.preMoveTurnDuration = 0.11f;
        template.preMoveHoldDuration = 0.03f;
        template.postEntryHoldDuration = 0.22f;
        template.rimRadiusFactor = 0.62f;
        template.rimPathPadding = 0.6f;
        template.rimMovementLerpMultiplier = 0.55f;
        template.rimArcPortion = 0.62f;
        template.outerRadiusFactor = 0.46f;
        template.centerRadiusFactor = 0.06f;
        template.rotateClockwise = true;
        template.pauseBetweenPhases = 0.08f;
        return CreateDefinition("AGILITY_ACT_PLUS", PatternTier.Easy, 5f, 0.9f, 0f, 0.4f, PatternTag.Body, PatternTag.None, 1, 10, template);
    }

    private static PatternDefinition BuildCross(Transform root)
    {
        var template = CreateTemplate<CrossThenPlusPattern>(root, "Runtime_CrossThenPlus_B");
        template.actorHeightOffset = 0.02f;
        template.entryPortion = 0.24f;
        template.exitPortion = 0.2f;
        template.movementLerp = 1.55f;
        template.preMoveTurnDuration = 0.11f;
        template.preMoveHoldDuration = 0.03f;
        template.postEntryHoldDuration = 0.22f;
        template.rimRadiusFactor = 0.62f;
        template.rimPathPadding = 0.6f;
        template.rimMovementLerpMultiplier = 0.55f;
        template.rimArcPortion = 0.62f;
        template.outerRadiusFactor = 0.46f;
        template.centerRadiusFactor = 0.06f;
        template.rotateClockwise = false;
        template.pauseBetweenPhases = 0.08f;
        return CreateDefinition("AGILITY_ACT_CROSS", PatternTier.Medium, 5f, 0.95f, 0f, 0.52f, PatternTag.Body | PatternTag.Constrain, PatternTag.None, 1, 10, template);
    }

    private static PatternDefinition BuildOrbit(Transform root)
    {
        var template = CreateTemplate<SlowClockwiseVolleyPattern>(root, "Runtime_SlowClockwiseVolley");
        template.actorHeightOffset = 0.02f;
        template.entryPortion = 0.22f;
        template.exitPortion = 0.2f;
        template.movementLerp = 1.45f;
        template.preMoveTurnDuration = 0.11f;
        template.preMoveHoldDuration = 0.03f;
        template.postEntryHoldDuration = 0.24f;
        template.rimRadiusFactor = 0.6f;
        template.rimPathPadding = 0.62f;
        template.rimMovementLerpMultiplier = 0.52f;
        template.rimArcPortion = 0.64f;
        template.angularSpeed = 78f;
        template.desiredStepDuration = 0.44f;
        template.orbitRadiusFactor = 0.38f;
        template.orbitRadiusPadding = 0f;
        template.pulseCount = 2;
        template.innerRadiusFactor = 0.12f;
        return CreateDefinition("AGILITY_ACT_ORBIT", PatternTier.Hard, 5f, 1f, 0f, 0.62f, PatternTag.Body, PatternTag.None, 1, 10, template);
    }

    private static T CreateTemplate<T>(Transform root, string name) where T : PatternBehaviour
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.SetActive(false);
        go.transform.SetParent(root, false);
        return go.AddComponent<T>();
    }

    private static PatternDefinition CreateDefinition(
        string id,
        PatternTier tier,
        float duration,
        float telegraph,
        float cooldown,
        float intensity,
        PatternTag tags,
        PatternTag forbiddenWith,
        int minDex,
        int maxDex,
        PatternBehaviour template)
    {
        var def = ScriptableObject.CreateInstance<PatternDefinition>();
        def.hideFlags = HideFlags.DontSave;
        def.id = id;
        def.tier = tier;
        def.duration = duration;
        def.telegraphTime = telegraph;
        def.cooldownAfter = cooldown;
        def.intensity = intensity;
        def.tags = tags;
        def.forbiddenWithTags = forbiddenWith;
        def.minDex = minDex;
        def.maxDex = maxDex;
        def.weightByDex = AnimationCurve.Linear(0f, 1f, 1f, tier == PatternTier.Hard ? 1.25f : 1f);
        def.patternPrefab = template;
        return def;
    }

    private static void EnsureCleanRoot(Transform root)
    {
        if (root == null || root.childCount == 0)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Object.Destroy(root.GetChild(i).gameObject);
        }
    }
}
