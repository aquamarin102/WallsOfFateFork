using System;
using System.Collections.Generic;
using UnityEngine;

public static class RunPlanGenerator
{
    private static readonly List<PatternDefinition> CandidateBuffer = new(32);

    public static RunPlan Generate(
        int dex,
        int seed,
        MiniGameConfig config,
        DexDifficultyProfile profile,
        PatternDatabase db)
    {
        RunPlan scriptedActs = TryGenerateScriptedActs(dex, config, profile, db);
        if (scriptedActs != null)
            return scriptedActs;

        var plan = new RunPlan();
        var rng = new System.Random(seed);

        float dex01 = profile.NormalizeDex(dex);
        float easyShare = profile.easyShare.Evaluate(dex01);
        float mediumShare = profile.mediumShare.Evaluate(dex01);
        float hardShare = profile.hardShare.Evaluate(dex01);
        float maxBudget = profile.maxIntensityBudget.Evaluate(dex01);
        float comboChance = profile.comboChance.Evaluate(dex01);
        int slots = Mathf.Max(1, Mathf.FloorToInt(config.runDuration / config.slotSeconds));

        PatternDefinition last = null;
        PatternTag recentTags = PatternTag.None;
        PatternTier lastTier = PatternTier.Easy;
        float time = 0f;

        for (int i = 0; i < slots; i++)
        {
            if (time >= config.runDuration)
                break;

            float t01 = slots <= 1 ? 1f : i / (float)(slots - 1);
            (float e, float m, float h) = TimeBias(easyShare, mediumShare, hardShare, t01);
            PatternTier targetTier = RollTier(rng, e, m, h);

            PatternDefinition firstPattern = PickPattern(rng, db, dex, targetTier, last, recentTags, maxBudget, 0f);
            if (firstPattern == null)
                break;

            Add(plan, firstPattern, ref time, config.slotSeconds, config.runDuration);
            last = firstPattern;
            recentTags = firstPattern.tags;
            lastTier = firstPattern.tier;

            bool wantCombo = rng.NextDouble() < comboChance && firstPattern.tier != PatternTier.Hard;
            if (wantCombo && time < config.runDuration)
            {
                PatternDefinition secondPattern = PickPattern(rng, db, dex, targetTier, last, recentTags, maxBudget, firstPattern.intensity);
                if (secondPattern != null)
                {
                    Add(plan, secondPattern, ref time, config.slotSeconds * 0.5f, config.runDuration);
                    last = secondPattern;
                    recentTags = secondPattern.tags;
                    lastTier = secondPattern.tier;
                }
            }

            if (time >= config.runDuration)
                break;

            if (lastTier >= PatternTier.Medium && rng.NextDouble() < profile.restFrequency.Evaluate(dex01))
            {
                float restSeconds = Mathf.Lerp(config.slotSeconds * 0.2f, config.slotSeconds * 0.05f, dex01);
                time = Mathf.Min(config.runDuration, time + restSeconds);
            }
        }

        return plan;
    }

    private static void Add(RunPlan plan, PatternDefinition pattern, ref float time, float slotSeconds, float maxRunDuration)
    {
        float duration = Mathf.Min(pattern.duration + pattern.cooldownAfter, slotSeconds);
        duration = Mathf.Min(duration, Mathf.Max(0f, maxRunDuration - time));
        if (duration <= 0f)
            return;

        plan.items.Add(new RunPlanItem
        {
            pattern = pattern,
            startTime = time,
            endTime = time + duration
        });

        time += duration;
    }

    private static (float e, float m, float h) TimeBias(float e, float m, float h, float t01)
    {
        float drift = Mathf.Lerp(0f, 0.25f, t01);
        e = Mathf.Clamp01(e - drift);
        h = Mathf.Clamp01(h + drift);

        float sum = e + m + h;
        if (sum <= 0.0001f)
            return (0.33f, 0.33f, 0.34f);

        return (e / sum, m / sum, h / sum);
    }

    private static PatternTier RollTier(System.Random rng, float e, float m, float h)
    {
        double roll = rng.NextDouble();
        if (roll < e)
            return PatternTier.Easy;

        if (roll < e + m)
            return PatternTier.Medium;

        return PatternTier.Hard;
    }

    private static PatternDefinition PickPattern(
        System.Random rng,
        PatternDatabase db,
        int dex,
        PatternTier tier,
        PatternDefinition last,
        PatternTag recentTags,
        float maxBudget,
        float alreadyUsedBudget)
    {
        CandidateBuffer.Clear();
        CollectCandidates(CandidateBuffer, db, dex, last, recentTags, maxBudget, alreadyUsedBudget, tierOnly: true, tier);

        if (CandidateBuffer.Count == 0)
            CollectCandidates(CandidateBuffer, db, dex, last, recentTags, maxBudget, alreadyUsedBudget, tierOnly: false, tier);

        if (CandidateBuffer.Count == 0)
            return null;

        float dex01 = Mathf.InverseLerp(1f, 10f, dex);
        float totalWeight = 0f;
        for (int i = 0; i < CandidateBuffer.Count; i++)
            totalWeight += Mathf.Max(0.001f, CandidateBuffer[i].weightByDex.Evaluate(dex01));

        double roll = rng.NextDouble() * totalWeight;
        float accumulatedWeight = 0f;
        for (int i = 0; i < CandidateBuffer.Count; i++)
        {
            accumulatedWeight += Mathf.Max(0.001f, CandidateBuffer[i].weightByDex.Evaluate(dex01));
            if (roll <= accumulatedWeight)
                return CandidateBuffer[i];
        }

        return CandidateBuffer[CandidateBuffer.Count - 1];
    }

    private static RunPlan TryGenerateScriptedActs(
        int dex,
        MiniGameConfig config,
        DexDifficultyProfile profile,
        PatternDatabase db)
    {
        PatternDefinition plus = db.FindById("AGILITY_ACT_PLUS");
        PatternDefinition cross = db.FindById("AGILITY_ACT_CROSS");
        PatternDefinition orbit = db.FindById("AGILITY_ACT_ORBIT");

        if (plus == null || cross == null || orbit == null)
            return null;

        float dex01 = profile != null ? profile.NormalizeDex(dex) : Mathf.InverseLerp(1f, 10f, dex);
        float phaseDuration = Mathf.Max(1f, config.runDuration / 3f);

        var plan = new RunPlan();
        float startTime = 0f;
        PatternDefinition[] sourcePatterns = { plus, orbit, cross };

        for (int i = 0; i < sourcePatterns.Length; i++)
        {
            PatternDefinition source = sourcePatterns[i];
            float segmentDuration = i == sourcePatterns.Length - 1
                ? config.runDuration - startTime
                : phaseDuration;
            float telegraph = Mathf.Min(source.telegraphTime, segmentDuration * 0.24f);
            float activeDuration = Mathf.Max(0.85f, segmentDuration - telegraph);

            PatternDefinition timedPattern = CreateTimedPatternDefinition(
                source,
                activeDuration,
                telegraph,
                Mathf.Clamp01(source.intensity + dex01 * (0.08f + i * 0.04f)));
            plan.TrackRuntimeObject(timedPattern);

            plan.items.Add(new RunPlanItem
            {
                pattern = timedPattern,
                startTime = startTime,
                endTime = Mathf.Min(config.runDuration, startTime + segmentDuration)
            });

            startTime += segmentDuration;
        }

        return plan;
    }

    private static PatternDefinition CreateTimedPatternDefinition(
        PatternDefinition source,
        float duration,
        float telegraphTime,
        float intensity)
    {
        var timedPattern = ScriptableObject.CreateInstance<PatternDefinition>();
        timedPattern.hideFlags = HideFlags.DontSave;
        timedPattern.id = source.id;
        timedPattern.tier = source.tier;
        timedPattern.duration = duration;
        timedPattern.telegraphTime = telegraphTime;
        timedPattern.cooldownAfter = 0f;
        timedPattern.intensity = intensity;
        timedPattern.tags = source.tags;
        timedPattern.forbiddenWithTags = source.forbiddenWithTags;
        timedPattern.minDex = source.minDex;
        timedPattern.maxDex = source.maxDex;
        timedPattern.weightByDex = source.weightByDex;
        timedPattern.patternPrefab = source.patternPrefab;
        return timedPattern;
    }

    private static void CollectCandidates(
        List<PatternDefinition> buffer,
        PatternDatabase db,
        int dex,
        PatternDefinition last,
        PatternTag recentTags,
        float maxBudget,
        float alreadyUsedBudget,
        bool tierOnly,
        PatternTier tier)
    {
        int count = db != null ? db.AllCount : 0;
        for (int i = 0; i < count; i++)
        {
            PatternDefinition pattern = db.GetAt(i);
            if (IsCandidate(pattern, dex, last, recentTags, maxBudget, alreadyUsedBudget, tierOnly, tier))
                buffer.Add(pattern);
        }
    }

    private static bool IsCandidate(
        PatternDefinition pattern,
        int dex,
        PatternDefinition last,
        PatternTag recentTags,
        float maxBudget,
        float alreadyUsedBudget,
        bool tierOnly,
        PatternTier tier)
    {
        if (pattern == null)
            return false;

        if (tierOnly && pattern.tier != tier)
            return false;

        if (dex < pattern.minDex || dex > pattern.maxDex)
            return false;

        if (last != null && pattern == last)
            return false;

        if ((pattern.forbiddenWithTags & recentTags) != 0)
            return false;

        return alreadyUsedBudget + pattern.intensity <= maxBudget;
    }
}
