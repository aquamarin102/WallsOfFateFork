using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.MiniGame;

namespace Game
{
    #region Mini-game Data Structures
    [System.Serializable]
    public class MiniGameData
    {
        public MiniGameType miniGameType;
        public string miniGameSceneName;
        public int difficultyLevel;
        public Dictionary<string, object> customParameters;

        public MiniGameData()
        {
            difficultyLevel = 0;
            miniGameType = MiniGameType.None;
            miniGameSceneName = "";
            customParameters = new Dictionary<string, object>();
        }

        public MiniGameData(MiniGameType miniGameTypeIn, string miniGameSceneNameIn, Dictionary<string, object> gameVariables)
        {
            miniGameType = miniGameTypeIn;
            miniGameSceneName = miniGameSceneNameIn;
            customParameters = gameVariables;
            difficultyLevel = 0;
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static MiniGameData FromJson(string json)
        {
            return JsonUtility.FromJson<MiniGameData>(json);
        }
    }
    #endregion

    public class EntryPoint : MonoBehaviour
    {
        private DialogueManager subscribedDialogueManager;

        #region Singleton
        private static EntryPoint _instance;
        public static EntryPoint Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<EntryPoint>();
                    if (_instance == null)
                    {
                        GameObject singleton = new GameObject("EntryPoint");
                        _instance = singleton.AddComponent<EntryPoint>();
                        DontDestroyOnLoad(singleton);
                    }
                }

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstanceOnLoad()
        {
            _ = Instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            SubscribeToDialogueManager();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeFromDialogueManager();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SubscribeToDialogueManager();
        }

        private void SubscribeToDialogueManager()
        {
            DialogueManager dialogueManager = FindObjectOfType<DialogueManager>();
            if (dialogueManager == null || dialogueManager == subscribedDialogueManager)
            {
                return;
            }

            UnsubscribeFromDialogueManager();
            subscribedDialogueManager = dialogueManager;
            subscribedDialogueManager.OnMiniGameStartRequested += LaunchMinigame;
        }

        private void UnsubscribeFromDialogueManager()
        {
            if (subscribedDialogueManager == null)
            {
                return;
            }

            subscribedDialogueManager.OnMiniGameStartRequested -= LaunchMinigame;
            subscribedDialogueManager = null;
        }
        #endregion

        #region MiniGame Management

        public bool IsMinigameActive => MinigameManager.Instance != null && MinigameManager.Instance.transform.gameObject.activeSelf;

        public void LaunchMinigame(MiniGameData launchData, DialogueGraph dialogueGraph)
        {
            if (launchData == null)
            {
                Debug.LogWarning("LaunchMinigame called without minigame data.");
                return;
            }

            if (IsMinigameActive)
            {
                Debug.LogWarning("Minigame is already running.");
                return;
            }

            GameObject minigameManagerObject = new GameObject("MinigameManager");
            MinigameManager minigameManager = minigameManagerObject.AddComponent<MinigameManager>();
            DontDestroyOnLoad(minigameManager.gameObject);

            Debug.Log("Created new MinigameManager");

            minigameManager.StartMinigame(launchData, dialogueGraph);

            Debug.Log($"Minigame started: {launchData.miniGameType}");
        }

        #endregion
    }
}
