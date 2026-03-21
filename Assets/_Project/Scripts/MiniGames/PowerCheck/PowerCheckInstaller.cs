using System.Collections.Generic;
using Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MiniGame.PowerCheck
{
    public class PowerCheckInstaller : MonoBehaviour, IMiniGameInstaller
    {
        [Header("Player Settings")]
        public Transform StartPoint;
        public GameObject PlayerPrefab;
        public Transform Parent;
        public Transform CameraTransform;
        public FXManager FXPlayerObject;

        [Header("HP Bar Settings")]
        public Transform CanvasTransform;
        public Slider PlayerHPBarPrefab;
        public Slider EnemyHPBarPrefab;

        [Header("Enemy Settings")]
        public Transform SpawnPoint;
        public GameObject EnemyPrefab;
        public FXManager FXEnemyObject;

        [Header("Game Settings")]
        public MineSpawner MineSpawnerObject;
        public GameProcess GameProcessObject;
        public EndDayScreenManager EndGameScreenObject;

        [Header("References (Auto-filled)")]
        public PlayerMove PlayerInstance;
        public AIController EnemyInstance;

        private MiniGameData _gameData;
        private bool _isInitializationPending;
        private bool _isInitialized;

        void Start()
        {
            BeginInitialization();
        }

        public void InitializeWithData(MiniGameData gameData)
        {
            _gameData = gameData;
            Debug.Log($"Инициализация мини-игры: {gameData?.miniGameType ?? MiniGameType.None}");
            BeginInitialization();
        }

        private void BeginInitialization()
        {
            if (_isInitialized || _isInitializationPending)
                return;

            if (TutorialSheetService.TryShowOnce(
                TutorialSheetDefinitions.StrengthKey,
                TutorialSheetDefinitions.StrengthResourcePath,
                TutorialSheetDefinitions.StrengthEditorAssetPath,
                ContinueInitialization))
            {
                _isInitializationPending = true;
                return;
            }

            ContinueInitialization();
        }

        private void ContinueInitialization()
        {
            _isInitializationPending = false;
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized)
                return;

            _isInitialized = true;

            if (_gameData == null && MinigameManager.Instance != null)
            {
                _gameData = MinigameManager.Instance.CurrentGameData;
            }

            if (TryGetIntParameter("difficulty", out int difficulty))
            {
                ApplyDifficulty(difficulty);
            }

            InitializeCameraTransform();
            BindMineSpawner();
            BindGameProcess();
            GameObject player = CreatePlayer();
            GameObject enemy = CreateEnemy();
            SetupForbiddenSpawnPoints();

            if (MineSpawnerObject != null)
            {
                MineSpawnerObject.Initialize();
            }
            if (FXPlayerObject != null && player != null)
            {
                FXPlayerObject.Initialize(player.GetComponent<MiniGamePlayer>());
            }
            if (FXEnemyObject != null && enemy != null)
            {
                FXEnemyObject.Initialize(enemy.GetComponent<MiniGamePlayer>());
            }
            if (GameProcessObject != null)
            {
                GameProcessObject.Initialize(FXPlayerObject, FXEnemyObject);
            }
            if (EndGameScreenObject != null)
            {
                EndGameScreenObject.OnEndGame -= OnMiniGameEnded;
                EndGameScreenObject.OnEndGame += OnMiniGameEnded;
            }
        }

        private void ApplyDifficulty(int difficulty)
        {
            Debug.Log($"Установлена сложность: {difficulty}");
        }

        private void BindMineSpawner()
        {
            if (MineSpawnerObject == null)
            {
                MineSpawnerObject = FindObjectOfType<MineSpawner>();
            }
        }

        private void BindGameProcess()
        {
            if (GameProcessObject == null)
            {
                GameProcessObject = FindObjectOfType<GameProcess>();
            }
        }

        private GameObject CreatePlayer()
        {
            GameObject playerObj = null;

            if (PlayerPrefab == null || StartPoint == null)
            {
                return playerObj;
            }

            playerObj = Instantiate(PlayerPrefab, StartPoint.position, PlayerPrefab.transform.rotation, Parent);

            PlayerInstance = playerObj.GetComponent<PlayerMove>();

            if (PlayerHPBarPrefab != null && CanvasTransform != null)
            {
                HealthBarManager healthBarManager = playerObj.GetComponent<HealthBarManager>();
                if (healthBarManager != null)
                {
                    healthBarManager.SetHealthBar(PlayerHPBarPrefab);
                }
            }

            return playerObj;
        }

        private GameObject CreateEnemy()
        {
            GameObject enemyObj = null;
            if (EnemyPrefab == null || SpawnPoint == null)
            {
                return enemyObj;
            }

            if (TryGetStringParameter("EnemyPrefab", out string pathToEnemyPrefab))
            {
                EnemyPrefab = Resources.Load<GameObject>(pathToEnemyPrefab) ?? EnemyPrefab;
            }

            enemyObj = Instantiate(EnemyPrefab, SpawnPoint.position, EnemyPrefab.transform.rotation, Parent);

            MiniGamePlayer enemyPlayer = enemyObj.GetComponent<MiniGamePlayer>();
            if (enemyPlayer != null)
            {
                enemyObj.name = enemyPlayer.GetName();
            }

            EnemyInstance = enemyObj.GetComponent<AIController>();

            if (EnemyHPBarPrefab != null && CanvasTransform != null)
            {
                HealthBarManager healthBarManager = enemyObj.GetComponent<HealthBarManager>();
                if (healthBarManager != null)
                {
                    healthBarManager.SetHealthBar(EnemyHPBarPrefab);
                }
            }

            return enemyObj;
        }

        private void InitializeCameraTransform()
        {
            if (CameraTransform == null)
            {
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    CameraTransform = mainCamera.transform;
                }
            }
        }

        private void SetupForbiddenSpawnPoints()
        {
            if (MineSpawnerObject != null && PlayerInstance != null && EnemyInstance != null)
            {
                var forbiddenPoints = new List<Transform>
                {
                    PlayerInstance.transform,
                    EnemyInstance.transform
                };

                MineSpawnerObject.SetForbiddenSpawnPoints(forbiddenPoints);
            }
        }

        public void OnMiniGameEnded(bool playerWin)
        {
            Debug.Log($"Мини-игра завершена! Победил ли игрок? {playerWin}");
            if (MinigameManager.Instance != null)
            {
                MinigameManager.Instance.EndMinigame(playerWin);
            }
        }

        void OnDestroy()
        {
            if (EndGameScreenObject != null)
            {
                EndGameScreenObject.OnEndGame -= OnMiniGameEnded;
            }
        }

        private bool TryGetIntParameter(string key, out int value)
        {
            value = default;

            if (_gameData?.customParameters == null ||
                !_gameData.customParameters.TryGetValue(key, out object rawValue) ||
                rawValue == null)
            {
                return false;
            }

            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                    value = (int)longValue;
                    return true;
                case float floatValue:
                    value = Mathf.RoundToInt(floatValue);
                    return true;
                case double doubleValue:
                    value = Mathf.RoundToInt((float)doubleValue);
                    return true;
                case string stringValue:
                    return int.TryParse(stringValue, out value);
                case JValue jValue:
                    return int.TryParse(jValue.ToString(), out value);
                default:
                    return false;
            }
        }

        private bool TryGetStringParameter(string key, out string value)
        {
            value = null;

            if (_gameData?.customParameters == null ||
                !_gameData.customParameters.TryGetValue(key, out object rawValue) ||
                rawValue == null)
            {
                return false;
            }

            switch (rawValue)
            {
                case string stringValue:
                    value = stringValue;
                    return !string.IsNullOrWhiteSpace(value);
                case JValue jValue:
                    value = jValue.ToObject<string>();
                    return !string.IsNullOrWhiteSpace(value);
                default:
                    value = rawValue.ToString();
                    return !string.IsNullOrWhiteSpace(value);
            }
        }
    }
}
