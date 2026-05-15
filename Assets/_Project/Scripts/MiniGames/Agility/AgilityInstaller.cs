using System.Collections;
using System.Globalization;
using Game;
using Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Zenject;

namespace Game.MiniGame.Agility
{
    public class AgilityInstaller : MonoInstaller, IMiniGameInstaller
    {
        private const float MaxLegacyGroundClearance = 0.05f;

        [Header("Core")]
        [SerializeField] private DexMiniGameController controller;
        [SerializeField] private PatternSequencer sequencer;
        [SerializeField] private GameObject patternRunnerObject;

        [Header("Player")]
        [SerializeField] private Transform playerStartPoint;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerMotor playerMotor;
        [SerializeField] private MovementModifiers playerModifiers;
        [SerializeField] private float playerSpawnHeightOffset = 0.02f;
        [SerializeField] private float sharedGroundOffset = -0.025f;
        [SerializeField] private float playerSpeedMultiplier = 1.18f;
        [SerializeField] private float playerAccelerationMultiplier = 1.12f;
        [SerializeField] private float playerDecelerationMultiplier = 1.12f;
        [SerializeField] private float telegraphSurfaceOffset = 0f;

        [Header("UI")]
        [SerializeField] private SliderTimerManager sliderTimerManager;
        [SerializeField] private PlayerHPManager playerHPManager;

        [Header("Standalone Debug")]
        [SerializeField] private bool autoStartWhenStandalone = true;
        [SerializeField] private float standaloneAutoStartDelay = 0.2f;
        [SerializeField] private KeyCode standaloneRestartKey = KeyCode.Space;

        [Header("Enemy Visuals")]
        [SerializeField] private GameObject patternEnemyPrefab;

        private MiniGameData _gameData;
        private Coroutine _standaloneStartRoutine;
        private bool _isTutorialPending;
        private GameObject _defaultThreatActorPrefab;
        private EntrancePoints _entrancePoints;

        public override void InstallBindings()
        {
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !autoStartWhenStandalone)
                return;

            RestartStandaloneAutoStart();
        }

        public void InitializeWithData(MiniGameData gameData)
        {
            StopStandaloneAutoStart();
            _gameData = gameData;

            if (_isTutorialPending)
                return;

            if (TutorialSheetService.TryShowOnce(
                TutorialSheetDefinitions.AgilityKey,
                TutorialSheetDefinitions.AgilityResourcePath,
                TutorialSheetDefinitions.AgilityEditorAssetPath,
                ContinueAfterTutorial))
            {
                _isTutorialPending = true;
                return;
            }

            ResolveReferences();
            PrepareScene();
            ApplyGameData();
            BindUi();

            if (controller == null)
            {
                Debug.LogError("AgilityInstaller: DexMiniGameController не найден на сцене.");
                return;
            }

            controller.OnEndGame -= OnMiniGameEnded;
            controller.OnEndGame += OnMiniGameEnded;
            controller.StartMiniGame();
        }

        private void ContinueAfterTutorial()
        {
            _isTutorialPending = false;
            InitializeWithData(_gameData);
        }

        public void OnMiniGameEnded(bool playerWin)
        {
            if (controller != null)
                controller.OnEndGame -= OnMiniGameEnded;

            if (MinigameManager.Instance != null)
                MinigameManager.Instance.EndMinigame(playerWin);
        }

        private void OnDestroy()
        {
            StopStandaloneAutoStart();

            if (controller != null)
            {
                controller.OnEndGame -= OnMiniGameEnded;
                controller.OnTimerChanged -= HandleTimerChanged;
            }

            if (playerMotor != null)
                playerMotor.OnDashCooldownChanged -= HandleDashCooldownChanged;

            if (sequencer != null)
            {
                sequencer.OnPatternCoreProgress -= HandlePatternCoreProgress;
                sequencer.OnPatternCoreCompleted -= HandlePatternCoreCompleted;
            }

            if (playerHealth != null)
                playerHealth.OnHpChanged -= HandleHpChanged;
        }

        private void OnDisable()
        {
            StopStandaloneAutoStart();
        }

        private void Update()
        {
            if (!Application.isPlaying || standaloneRestartKey == KeyCode.None)
                return;

            if (!Input.GetKeyDown(standaloneRestartKey))
                return;

            if (!CanStartStandaloneRun())
                return;

            InitializeWithData(null);
        }

        private void ResolveReferences()
        {
            controller ??= AgilitySceneUtility.FindInLoadedScene<DexMiniGameController>();
            sequencer ??= controller != null ? controller.sequencer : AgilitySceneUtility.FindInLoadedScene<PatternSequencer>();
            patternRunnerObject ??= controller != null ? controller.gameObject : null;
            playerHealth ??= AgilitySceneUtility.FindInLoadedScene<PlayerHealth>("Player");
            playerHealth ??= AgilitySceneUtility.FindInLoadedScene<PlayerHealth>();
            playerMotor ??= playerHealth != null ? playerHealth.GetComponent<PlayerMotor>() : AgilitySceneUtility.FindInLoadedScene<PlayerMotor>("Player");
            playerModifiers ??= playerHealth != null ? playerHealth.GetComponent<MovementModifiers>() : AgilitySceneUtility.FindInLoadedScene<MovementModifiers>("Player");
            playerStartPoint ??= AgilitySceneUtility.FindTransform("PlayerSpawnPoint");
            _entrancePoints ??= AgilitySceneUtility.FindInLoadedScene<EntrancePoints>();
            sliderTimerManager ??= AgilitySceneUtility.FindInLoadedScene<SliderTimerManager>();
            playerHPManager ??= AgilitySceneUtility.FindInLoadedScene<PlayerHPManager>();

            if (controller != null && _defaultThreatActorPrefab == null)
                _defaultThreatActorPrefab = controller.threatActorPrefab;
        }

        private void PrepareScene()
        {
            AgilitySceneUtility.ConfigureMiniGamePlacement(sharedGroundOffset, telegraphSurfaceOffset);

            //var legacyProcess = AgilitySceneUtility.FindInLoadedScene<GameProcess>();
            //if (legacyProcess != null)
            //    legacyProcess.enabled = false;

            if (patternRunnerObject != null)
                patternRunnerObject.SetActive(true);
            if (controller != null)
                controller.gameObject.SetActive(true);
            if (sequencer != null)
                sequencer.gameObject.SetActive(true);

            if (playerHealth == null)
                return;

            playerHealth.gameObject.SetActive(true);
            playerHealth.enabled = true;

            if (playerModifiers != null)
                playerModifiers.enabled = true;
            if (playerMotor != null)
            {
                playerMotor.enabled = true;
                playerMotor.SetRuntimeTuning(playerSpeedMultiplier, playerAccelerationMultiplier, playerDecelerationMultiplier);
            }

            foreach (var renderer in playerHealth.GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;

            EnsurePlayerCollider();

            playerHealth.transform.SetPositionAndRotation(ResolvePlayerSpawnPosition(), ResolvePlayerSpawnRotation());

            var rigidbody = playerHealth.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.useGravity = false;
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
                rigidbody.constraints = RigidbodyConstraints.FreezePositionY
                                      | RigidbodyConstraints.FreezeRotationX
                                      | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void ApplyGameData()
        {
            if (controller == null)
                return;

            controller.dex = ResolveDex();
            controller.seed = TryGetInt("seed", out int seed)
                ? seed
                : System.Environment.TickCount;
            ApplyPatternEnemyPrefab();
        }

        private void ApplyPatternEnemyPrefab()
        {
            if (controller == null)
                return;

            if (_defaultThreatActorPrefab == null)
                _defaultThreatActorPrefab = controller.threatActorPrefab;

            controller.threatActorPrefab = patternEnemyPrefab != null
                ? patternEnemyPrefab
                : _defaultThreatActorPrefab;
        }

        private void BindUi()
        {
            if (controller == null || playerHealth == null)
                return;

            controller.OnTimerChanged -= HandleTimerChanged;
            controller.OnTimerChanged += HandleTimerChanged;
            playerHealth.OnHpChanged -= HandleHpChanged;
            playerHealth.OnHpChanged += HandleHpChanged;
            if (sequencer != null)
            {
                sequencer.OnPatternCoreProgress -= HandlePatternCoreProgress;
                sequencer.OnPatternCoreProgress += HandlePatternCoreProgress;
                sequencer.OnPatternCoreCompleted -= HandlePatternCoreCompleted;
                sequencer.OnPatternCoreCompleted += HandlePatternCoreCompleted;
            }

            if (controller.config != null)
            {
                sliderTimerManager?.InitializeTimer(controller.config.runDuration, 3);
                playerHPManager?.InitializeHP(controller.config.startingHp);
            }

            if (playerMotor != null)
            {
                playerMotor.OnDashCooldownChanged -= HandleDashCooldownChanged;
                playerMotor.OnDashCooldownChanged += HandleDashCooldownChanged;
                sliderTimerManager?.InitializeDashCooldown(playerMotor.DashCooldownDuration);
                playerMotor.ForcePublishDashCooldown();
            }
        }

        private void HandleTimerChanged(float remaining, float total)
        {
            sliderTimerManager?.SetRemainingTime(remaining, total);
        }

        private void HandleHpChanged(int hp, int max)
        {
            playerHPManager?.UpdateHP(hp, max);
        }

        private void HandlePatternCoreProgress(int patternIndex, int totalPatterns, float normalized)
        {
            sliderTimerManager?.SetSegmentProgress(patternIndex, totalPatterns, normalized);
        }

        private void HandlePatternCoreCompleted(int patternIndex, int totalPatterns)
        {
            sliderTimerManager?.SnapToDivider(patternIndex + 1, totalPatterns);
        }

        private void HandleDashCooldownChanged(float remaining, float total, bool isReady)
        {
            sliderTimerManager?.SetDashCooldown(remaining, total, isReady);
        }

        private void RestartStandaloneAutoStart()
        {
            StopStandaloneAutoStart();
            _standaloneStartRoutine = StartCoroutine(TryStandaloneStart());
        }

        private void StopStandaloneAutoStart()
        {
            if (_standaloneStartRoutine == null)
                return;

            StopCoroutine(_standaloneStartRoutine);
            _standaloneStartRoutine = null;
        }

        private IEnumerator TryStandaloneStart()
        {
            yield return new WaitForSeconds(standaloneAutoStartDelay);
            _standaloneStartRoutine = null;

            if (!CanStartStandaloneRun(autoStartOnly: true))
                yield break;

            InitializeWithData(null);
        }

        private bool CanStartStandaloneRun(bool autoStartOnly = false)
        {
            if (MinigameManager.Instance != null && MinigameManager.Instance.CurrentGameData != null)
                return false;

            ResolveReferences();
            if (controller == null)
                return false;

            return controller.state switch
            {
                MiniGameState.Idle => true,
                MiniGameState.Win => !autoStartOnly,
                MiniGameState.Lose => !autoStartOnly,
                _ => false
            };
        }

        private Vector3 ResolvePlayerSpawnPosition()
        {
            Vector3 spawnPosition = playerStartPoint != null
                ? playerStartPoint.position
                : playerHealth.transform.position;

            Transform board = AgilitySceneUtility.FindArenaRootTransform();
            float surfaceTopY = AgilitySceneUtility.ResolveArenaSurfaceY(_entrancePoints, board, playerStartPoint, spawnPosition.y);
            float groundClearance = ResolveGroundClearance(playerSpawnHeightOffset, sharedGroundOffset);
            spawnPosition.y = AgilitySceneUtility.ResolveAlignedRootY(playerHealth.transform, surfaceTopY, spawnPosition.y, groundClearance);
            return spawnPosition;
        }

        private static float ResolveGroundClearance(float legacyClearance, float sharedOffset)
        {
            return NormalizeLegacyClearance(legacyClearance) + sharedOffset;
        }

        private static float NormalizeLegacyClearance(float clearance)
        {
            return Mathf.Abs(clearance) <= MaxLegacyGroundClearance ? clearance : 0f;
        }

        private Quaternion ResolvePlayerSpawnRotation()
        {
            if (playerStartPoint == null)
                return Quaternion.Euler(0f, playerHealth.transform.eulerAngles.y, 0f);

            Vector3 euler = playerStartPoint.eulerAngles;
            return Quaternion.Euler(0f, euler.y, 0f);
        }

        private void EnsurePlayerCollider()
        {
            if (playerHealth == null)
                return;

            var existingCollider = playerHealth.GetComponent<Collider>();
            if (existingCollider != null)
            {
                existingCollider.enabled = true;
                return;
            }

            var capsule = playerHealth.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = playerHealth.gameObject.AddComponent<CapsuleCollider>();

            capsule.enabled = true;
            capsule.direction = 1;
            capsule.isTrigger = false;

            if (!AgilitySceneUtility.TryGetWorldBounds(playerHealth.transform, out Bounds bounds))
            {
                capsule.center = new Vector3(0f, 0.45f, 0f);
                capsule.radius = 0.25f;
                capsule.height = 0.9f;
                return;
            }

            Vector3 bottomWorld = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 centerLocal = playerHealth.transform.InverseTransformPoint((bottomWorld + topWorld) * 0.5f);
            Vector3 localBottom = playerHealth.transform.InverseTransformPoint(bottomWorld);
            Vector3 localTop = playerHealth.transform.InverseTransformPoint(topWorld);

            float scaleX = Mathf.Max(0.0001f, Mathf.Abs(playerHealth.transform.lossyScale.x));
            float scaleZ = Mathf.Max(0.0001f, Mathf.Abs(playerHealth.transform.lossyScale.z));
            float worldRadius = Mathf.Max(0.12f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.32f);
            float localRadius = Mathf.Max(0.05f, worldRadius / Mathf.Max(scaleX, scaleZ));
            float localHeight = Mathf.Max(localRadius * 2f, Mathf.Abs(localTop.y - localBottom.y) * 0.82f);

            capsule.center = centerLocal;
            capsule.radius = localRadius;
            capsule.height = localHeight;
        }

        private int ResolveDex()
        {
            string[] keys = { "dex", "Dex", "DEX", "dexterity", "Dexterity" };
            for (int i = 0; i < keys.Length; i++)
            {
                if (TryGetInt(keys[i], out int value))
                    return Mathf.Clamp(value, 1, 10);
            }

            if (_gameData != null && _gameData.difficultyLevel > 0)
                return Mathf.Clamp(_gameData.difficultyLevel * 3, 1, 10);

            return controller != null ? controller.dex : 5;
        }

        private bool TryGetInt(string key, out int value)
        {
            value = 0;
            if (_gameData?.customParameters == null)
                return false;

            if (!_gameData.customParameters.TryGetValue(key, out object raw) || raw == null)
                return false;

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case long l:
                    value = (int)l;
                    return true;
                case float f:
                    value = Mathf.RoundToInt(f);
                    return true;
                case double d:
                    value = Mathf.RoundToInt((float)d);
                    return true;
                case string s:
                    return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                case JValue jValue:
                    return int.TryParse(jValue.ToString(CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                default:
                    return int.TryParse(raw.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
        }
    }
}
