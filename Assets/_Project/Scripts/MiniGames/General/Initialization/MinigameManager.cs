using Game.Data;
using Ink.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game.MiniGame
{
    [System.Serializable]
    public enum MiniGameType
    {
        None = 0,
        PowerCheck = 1,
        Agility = 2,
        Intelligence = 3
    }


    public class MinigameManager : MonoBehaviour
    {
        public static MinigameManager Instance { get; private set; }

        [Header("Minigame Scenes")]
        [SerializeField] private string castleDefenseScene = "CastleDefenseMinigame";

        private MiniGameData _currentGameData;
        private Dictionary<string, object> _lastResult;
        private string _previousScene;

        public MiniGameData CurrentGameData => _currentGameData;

        private PlayerManager playerManager;
        private LoadingManager loadingManager;

        private Transform playerTransform;
        private Vector3 previousPosition;
        private Quaternion previousRotation;
        private DialogueGraph dialogueGraph;

        [Inject]
        public void Construct(PlayerManager playerManager, LoadingManager loadingManager, PlayerMoveController playerController)
        {
            this.playerTransform = playerController.transform;
            this.playerManager = playerManager;
            this.loadingManager = loadingManager;
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }          

        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public void StartMinigame(MiniGameData gameData, DialogueGraph dialogueGraph)
        {

            this.dialogueGraph = dialogueGraph;
            _currentGameData = gameData;
            _previousScene = SceneManager.GetActiveScene().name;

            if(playerTransform != null)
            {
                previousPosition = playerTransform.position;
                previousRotation = playerTransform.rotation;
            }

            string sceneToLoad = gameData.miniGameSceneName;

            if (!string.IsNullOrEmpty(sceneToLoad))
            {
                //SceneManager.LoadScene(sceneToLoad);
                loadingManager.LoadScene(sceneToLoad);
            }
            else
            {
                Debug.LogError($"Не определена сцена для мини-игры типа: {gameData.miniGameType}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Invoke(nameof(InitializeMinigame), 0.1f);
        }

        private void InitializeMinigame()
        {
            IMiniGameInstaller installer = FindInstallerByInterface();
            if (installer != null)
            {
                installer.InitializeWithData(_currentGameData);
            }
            else
            {
                Debug.LogError("MiniGameInstaller не найден на сцене мини-игры!");
            }
        }

        IMiniGameInstaller FindInstallerByInterface()
        {
            MonoBehaviour[] allBehaviours = FindObjectsOfType<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in allBehaviours)
            {
                if (behaviour is IMiniGameInstaller installer)
                {
                    return installer;
                }
            }

            Debug.LogWarning("IMiniGameInstaller не найден на сцене!");
            return null;
        }

        public void EndMinigame(bool playerWon)
        {

            string outcomeKey = playerWon ? "Win" : "Lose";

            try
            {
                if (_currentGameData.customParameters.TryGetValue(outcomeKey, out object outcomeObj) &&
                    outcomeObj is Newtonsoft.Json.Linq.JObject outcomeJObject)
                {
                    if (outcomeJObject.TryGetValue("Resources", out Newtonsoft.Json.Linq.JToken resourcesToken) &&
                        resourcesToken is Newtonsoft.Json.Linq.JObject resourcesJObject)
                    {
                        var resourcesDict = resourcesJObject.ToObject<Dictionary<string, string>>();

                        if (resourcesDict != null)
                        {
                            // Обрабатываем ресурсы;
                            ProcessResources(resourcesDict);
                        }
                    }
                }
                else
                {
                    Debug.Log($"No '{outcomeKey}' configuration found in minigame data");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error processing minigame result: {e.Message}");
            }

            PlayerSpawnData.SpawnPosition = previousPosition;
            PlayerSpawnData.SpawnRotation = previousRotation;

            loadingManager.LoadScene(_previousScene);
            Destroy(this.gameObject);
        }

        private void ProcessResources(Dictionary<string, string> resourcesDict)
        {
            foreach (var resource in resourcesDict)
            {
                if (!string.IsNullOrEmpty(resource.Key))
                    playerManager.PlayerData.AddResource(Enum.Parse<ResourceType>(resource.Key), Convert.ToInt32(resource.Key));
            }
        }
    }
}

