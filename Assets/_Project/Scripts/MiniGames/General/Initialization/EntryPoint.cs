using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
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
        [Inject] private DiContainer container;

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

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            DialogueManager.Instance.OnMiniGameStartRequested += LaunchMinigame;

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        #endregion

        #region MiniGame Management

        public bool IsMinigameActive => MinigameManager.Instance != null && MinigameManager.Instance.transform.gameObject.activeSelf;

        [Inject]
        private void Construct(DiContainer container)
        {
            this.container = container;
        }

        public void LaunchMinigame(MiniGameData launchData, DialogueGraph dialogueGraph)
        {
            if (IsMinigameActive)
            {
                Debug.LogWarning("Мини-игра уже запущена!");
                return;
            }

            //GameObject managerObj = new GameObject("MinigameManager");
            //MinigameManager minigameManager = managerObj.AddComponent<MinigameManager>();
            //DontDestroyOnLoad(managerObj);
            MinigameManager minigameManager = container.InstantiateComponentOnNewGameObject<MinigameManager>("MinigameManager");
            DontDestroyOnLoad(minigameManager.gameObject);

            Debug.Log("Создан новый MinigameManager");

            string jsonData = launchData.ToJson();

            minigameManager.StartMinigame(launchData, dialogueGraph);

            Debug.Log($"Мини-игра запущена: {launchData.miniGameType}");
        }

        #endregion
    }
}
