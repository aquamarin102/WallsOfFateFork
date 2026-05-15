using System;
using System.Collections;
using UnityEngine;

public class DexMiniGameController : MonoBehaviour
{
    [Header("Config")]
    public MiniGameConfig config;
    public DexDifficultyProfile difficultyProfile;
    public PatternDatabase patternDatabase;

    [Header("Refs")]
    public PatternSequencer sequencer;
    public PlayerHealth playerHealth;
    public MovementModifiers playerModifiers;

    [Header("Visuals")]
    public GameObject threatActorPrefab;

    [Header("Runtime")]
    public MiniGameState state = MiniGameState.Idle;

    [Tooltip("Dex игрока (подайте сюда из вашей RPG-системы)")]
    public int dex = 5;

    public int seed = 12345;
    public bool autoResolveSceneRefs = true;
    public bool buildRuntimePatterns = true;

    private Coroutine _runRoutine;
    private Transform _runtimePatternRoot;
    private bool _runtimePatternsInitialized;
    private RunPlan _activePlan;

    public float TimeLeft { get; private set; }

    public event Action<bool> OnEndGame;
    public event Action<MiniGameState> OnStateChanged;
    public event Action<float, float> OnTimerChanged;

    public void StartMiniGame()
    {
        ResolveReferences();
        AgilityHazardFactory.SetPatternActorPrefab(threatActorPrefab);
        EnsureRuntimePatterns();
        DisposeActivePlan();

        if (config == null || difficultyProfile == null || patternDatabase == null || sequencer == null || playerHealth == null)
        {
            Debug.LogError("DexMiniGameController: запуск отменён, не хватает обязательных ссылок.");
            return;
        }

        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }

        sequencer.StopActivePattern();
        _runRoutine = StartCoroutine(RunFlow());
    }

    private IEnumerator RunFlow()
    {
        SetState(MiniGameState.Countdown);

        playerHealth.ResetTo(config.startingHp, config.iFramesSeconds);
        sequencer.Init(playerHealth, playerModifiers);
        PublishTimer(config.runDuration);

        float cd = config.countdownSeconds;
        while (cd > 0f)
        {
            cd -= Time.deltaTime;
            yield return null;
        }

        SetState(MiniGameState.SafePhase);
        yield return new WaitForSeconds(config.safePhaseSeconds);

        var plan = RunPlanGenerator.Generate(dex, seed, config, difficultyProfile, patternDatabase);
        _activePlan = plan;

        SetState(MiniGameState.Running);

        float timeLeft = config.runDuration;
        PublishTimer(timeLeft);
        var patternsRoutine = StartCoroutine(sequencer.Play(plan));

        while (timeLeft > 0f && !playerHealth.IsDead)
        {
            timeLeft -= Time.deltaTime;
            PublishTimer(timeLeft);
            yield return null;
        }

        if (patternsRoutine != null)
            StopCoroutine(patternsRoutine);

        sequencer.StopActivePattern();
        PublishTimer(Mathf.Max(0f, timeLeft));

        SetState(playerHealth.IsDead ? MiniGameState.Lose : MiniGameState.Win);
        OnEndGame?.Invoke(state == MiniGameState.Win);
        DisposeActivePlan();
        _runRoutine = null;
    }

    private void OnDisable()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }

        if (sequencer != null)
            sequencer.StopActivePattern();

        DisposeActivePlan();
    }

    private void OnDestroy()
    {
        ResetRuntimePatterns();
    }

    private void ResolveReferences()
    {
        if (!autoResolveSceneRefs)
            return;

        if (sequencer == null)
            sequencer = AgilitySceneUtility.FindInLoadedScene<PatternSequencer>();
        if (playerHealth == null)
            playerHealth = AgilitySceneUtility.FindInLoadedScene<PlayerHealth>("Player");
        if (playerHealth == null)
            playerHealth = AgilitySceneUtility.FindInLoadedScene<PlayerHealth>();
        if (playerModifiers == null && playerHealth != null)
            playerModifiers = playerHealth.GetComponent<MovementModifiers>();
        if (playerModifiers == null)
            playerModifiers = AgilitySceneUtility.FindInLoadedScene<MovementModifiers>("Player");
        if (patternDatabase == null)
            Debug.LogWarning("DexMiniGameController: PatternDatabase не назначен.");
        if (config == null)
            Debug.LogWarning("DexMiniGameController: MiniGameConfig не назначен.");
        if (difficultyProfile == null)
            Debug.LogWarning("DexMiniGameController: DexDifficultyProfile не назначен.");
    }

    private void EnsureRuntimePatterns()
    {
        if (!buildRuntimePatterns || patternDatabase == null || _runtimePatternsInitialized)
            return;

        if (_runtimePatternRoot == null || !_runtimePatternRoot.gameObject.scene.IsValid())
        {
            var root = new GameObject("RuntimePatterns");
            root.hideFlags = HideFlags.HideInHierarchy;
            root.transform.SetParent(transform, false);
            root.SetActive(true);
            _runtimePatternRoot = root.transform;
        }

        patternDatabase.ReplaceRuntimePatterns(RuntimePatternFactory.Build(_runtimePatternRoot));
        _runtimePatternsInitialized = true;
    }

    private void SetState(MiniGameState nextState)
    {
        state = nextState;
        OnStateChanged?.Invoke(state);
    }

    private void PublishTimer(float remaining)
    {
        TimeLeft = Mathf.Max(0f, remaining);
        if (config != null)
            OnTimerChanged?.Invoke(TimeLeft, config.runDuration);
    }

    private void DisposeActivePlan()
    {
        if (_activePlan == null)
            return;

        _activePlan.DisposeRuntimeObjects();
        _activePlan = null;
    }

    private void ResetRuntimePatterns()
    {
        patternDatabase?.ClearRuntimePatterns();
        _runtimePatternRoot = null;
        _runtimePatternsInitialized = false;
    }
}
