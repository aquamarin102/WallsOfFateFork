using System.Collections.Generic;
using Game.UI;
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
            Debug.Log($"Инициализация мини-игры: {gameData.miniGameType}");
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

            // Если данных нет, пробуем получить их из MinigameManager
            if (_gameData == null && MinigameManager.Instance != null)
            {
                _gameData = MinigameManager.Instance.CurrentGameData;
            }

            // Применяем настройки сложности
            if (_gameData != null && _gameData.customParameters.ContainsKey("difficulty"))
            {
                ApplyDifficulty((int)_gameData.customParameters["difficulty"]);
            }

            // Инициализируем игру
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
            if (FXPlayerObject != null)
            {
                FXPlayerObject.Initialize(player.GetComponent<MiniGamePlayer>());
            }
            if (FXEnemyObject != null)
            {
                FXEnemyObject.Initialize(enemy.GetComponent<MiniGamePlayer>());
            }
            if (GameProcessObject != null)
            {
                GameProcessObject.Initialize(FXPlayerObject, FXEnemyObject);
            }
            if (EndGameScreenObject != null)
                EndGameScreenObject.OnEndGame += OnMiniGameEnded;
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

            if (PlayerPrefab == null || StartPoint == null) return playerObj;

            playerObj = Instantiate(PlayerPrefab, StartPoint.position,
                PlayerPrefab.transform.rotation, Parent);

            PlayerInstance = playerObj.GetComponent<PlayerMove>();

            // Создаем HP бар для игрока
            if (PlayerHPBarPrefab != null && CanvasTransform != null)
            {
                //Slider playerHealthBar = Instantiate(PlayerHPBarPrefab, CanvasTransform);
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
            if (EnemyPrefab == null || SpawnPoint == null) return enemyObj;

            if (_gameData != null)
            {
                string pathToEnemyPrefab = (string)_gameData.customParameters["EnemyPrefab"];
                if (!string.IsNullOrEmpty(pathToEnemyPrefab))
                    EnemyPrefab = Resources.Load<GameObject>(pathToEnemyPrefab);
            }

            enemyObj = Instantiate(EnemyPrefab, SpawnPoint.position, EnemyPrefab.transform.rotation, Parent);

            enemyObj.name = enemyObj.GetComponent<MiniGamePlayer>().GetName();
            EnemyInstance = enemyObj.GetComponent<AIController>();

            // Создаем HP бар для врага
            if (EnemyHPBarPrefab != null && CanvasTransform != null)
            {
                //Slider enemyHealthBar = Instantiate(EnemyHPBarPrefab, CanvasTransform);
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
            if (MinigameManager.Instance != null) MinigameManager.Instance.EndMinigame(playerWin);
        }

        void OnDestroy()
        {
            if (EndGameScreenObject != null)
            {
                EndGameScreenObject.OnEndGame -= OnMiniGameEnded;
            }
        }

    }
}
