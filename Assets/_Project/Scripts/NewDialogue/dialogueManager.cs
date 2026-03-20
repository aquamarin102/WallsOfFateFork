using Game.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game
{
    public class DialogueManager : MonoBehaviour
    {
        #region UI
        [SerializeField] GameObject DialogueUI;
        [SerializeField] GameObject PlayerPanel;
        [SerializeField] GameObject NPCPanel;
        [SerializeField] Image NPCPortrait;
        [SerializeField] RectTransform SpawnPoint;
        [SerializeField] GameObject resourcesUI;
        [SerializeField] GameObject OptionsList;
        #endregion

        #region MainInfo
        private DialogueGraph currentDialogue;
        private Sentence currentSentence;
        private List<Sentence> currentOptions = new();
        #endregion

        #region Utility
        public bool IsInDialogue = false;
        private readonly WaitForSeconds typingDelay = new(0.03f);

        private List<GameObject> spawnedPanels = new();

        private static DialogueManager _instance;
        private PlayerManager playerManager;

        private GameObject optionPrefab;

        public static DialogueManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<DialogueManager>();

                    if (_instance == null)
                    {
                        Debug.LogError("No DialogueManager found in scene!");
                        return null;
                    }
                }
                return _instance;
            }
            private set
            {
                _instance = value;
            }
        }

        bool startMinigame;
        MiniGameData miniGameData;
        #endregion

        #region Events
        public event Action<DialogueGraph> OnFinished;
        public event Action<MiniGameData, DialogueGraph> OnMiniGameStartRequested;
        #endregion

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        private void Awake()
        {
            if (_instance == null)
            {
                Instance = this;
                _instance = this;
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (DialogueUI == null)
            {
                Debug.LogError("DialogueUI is not assigned in DialogueManager!", this);
            }
        }

        #region PannelsUI

        private void ClearSpawnedPanels()
        {
            foreach (GameObject panel in spawnedPanels)
            {
                if (panel != null)
                {
                    Destroy(panel);
                }
            }
            spawnedPanels.Clear();
        }
        #endregion

        #region MiniGame

        void ConfigMiniGame()
        {
            if (currentSentence.StartMinigame)
            {
                startMinigame = true;
                miniGameData = GetParamsOfMinigame();
            }
        }

        MiniGameData GetParamsOfMinigame()
        {
            MiniGame.MiniGameType minigameType = currentSentence.MiniGameType;
            string sceneName = currentSentence.MiniGameSceneName;
            Dictionary<string, object> minigameParams = currentSentence.MinigameParams;

            return new MiniGameData(minigameType, sceneName, minigameParams);
        }
        #endregion

        #region MainLogic
        private void Start()
        {
            optionPrefab = Resources.Load<GameObject>("UI/Dialogues/Option");

            if (DialogueUI != null)
            {
                DialogueUI.SetActive(false);
            }
        }

        private void Update()
        {
            if (currentOptions.Count > 0)
            {
                for (int i = 0; i < currentOptions.Count; i++)
                {
                    if (Input.GetKeyDown(Enum.Parse<KeyCode>((49 + i).ToString())))
                    {
                        SelectOption(i);
                    }
                }
            }
        }

        public void StartDialogue(DialogueGraph currentDialogue)
        {          
            if (IsInDialogue || currentDialogue == null)
            {
                return;
            }

            this.currentDialogue = currentDialogue;
            IsInDialogue = true;
            startMinigame = false;
            miniGameData = null;

            ClearSpawnedPanels();

            if (resourcesUI != null) {
                resourcesUI.SetActive(false);
            }

            if (DialogueUI != null)
            {
                Sprite mySprite = Resources.Load<Sprite>("Characters/Portraits/" + this.currentDialogue.Portrait);
                NPCPortrait.sprite = mySprite;

                DialogueUI.SetActive(true);
            }

            currentSentence = currentDialogue.Sentences[0];
            DisplayCurrentSentence();
        }

        private bool TryInstantiatePannel(out GameObject sentencePanel)
        {
            sentencePanel = null;

            if (currentSentence == null)
                return false;

            GameObject panelPrefab;

            if (currentSentence.IsPlayer)
            {
                panelPrefab = PlayerPanel;
            }
            else
            {
                panelPrefab = NPCPanel;
            }

            if (panelPrefab != null && SpawnPoint != null)
            {
                sentencePanel = Instantiate(panelPrefab, SpawnPoint);

                sentencePanel.transform.localPosition = Vector3.zero;

                spawnedPanels.Add(sentencePanel);

                return true;
            }

            return false;
        }

        public void SelectOption(int optionIndex)
        {           
            var optionSentence = currentOptions[optionIndex];

            foreach (Transform child in OptionsList.transform)
            {
                Destroy(child.gameObject);
            }

            currentSentence = optionSentence;
            DisplayCurrentSentence(); 
        }

        public void DisplayCurrentSentence()
        {
            if (currentSentence == null) return;

            UpdateResources();
            ConfigMiniGame();

            bool pannelSpawned = TryInstantiatePannel(out GameObject sentencePanel);

            if (pannelSpawned)
            {
                TMP_Text sentenceText = sentencePanel.transform.Find("Text")?.GetComponent<TMP_Text>();

                if (sentenceText != null)
                {
                    StartCoroutine(TypeSentence(currentSentence.Text, sentenceText));
                }
            }
        }

        private void UpdateResources()
        {
            if (currentSentence == null)
                return;

            playerManager.PlayerData.AddResource(ResourceType.Gold, currentSentence.Gold);
            playerManager.PlayerData.AddResource(ResourceType.Food, currentSentence.Food);
            playerManager.PlayerData.AddResource(ResourceType.PeopleSatisfaction, currentSentence.PeopleSatisfaction);
            playerManager.PlayerData.AddResource(ResourceType.CastleStrength, currentSentence.CastleStrength);
        }

        private void ProcessNextSentence()
        {
            var nextSentence = currentDialogue.Sentences.Find(s => s.Id == currentSentence.NextSentenceId);
            if (nextSentence == null)
            {
                CloseDialogue();
                return;
            }

            currentSentence = nextSentence;
            if (currentSentence.IsOption)
            {
                LoadAllOptions();
                return;
            }

            DisplayCurrentSentence();
        }

        private void LoadAllOptions()
        {
            if (currentDialogue == null) return;

            foreach (Transform child in OptionsList.transform)
            {
                Destroy(child.gameObject);
            }

            currentOptions.Clear();
            var optionCounter = 1;

            while (currentSentence.IsOption)
            {
                currentOptions.Add(currentSentence);
                var optionObject = Instantiate(optionPrefab);
                optionObject.transform.SetParent(OptionsList.transform, false);

                Button optionButton = optionObject.GetComponent<Button>();
                var capturedIndex = optionCounter - 1;
                optionButton.onClick.AddListener(() => SelectOption(capturedIndex));

                TMP_Text optionTextComponent = optionObject.transform.GetComponent<TMP_Text>();
                optionTextComponent.text = optionCounter + ". " + currentSentence.Text;

                optionCounter++;

                if (currentDialogue.Sentences.IndexOf(currentSentence) + 1 >= currentDialogue.Sentences.Count)
                {
                    break;
                }

                currentSentence = currentDialogue.Sentences[currentDialogue.Sentences.IndexOf(currentSentence) + 1];
            }
        }

        private void CloseDialogue()
        {
            foreach (Transform child in OptionsList.transform)
            {
                Destroy(child.gameObject);
            }

            StartCoroutine(CloseDialogueWithDelay(1f));
        }

        private IEnumerator CloseDialogueWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            var finishedDialogue = currentDialogue;
            bool shouldStartMinigame = startMinigame && miniGameData != null;
            MiniGameData launchData = miniGameData;

            if (resourcesUI != null)
            {
                resourcesUI.gameObject.SetActive(true);
            }

            if (DialogueUI != null)
            {
                DialogueUI.SetActive(false);
            }

            ClearSpawnedPanels();

            IsInDialogue = false;
            currentDialogue = null;
            currentSentence = null;
            startMinigame = false;
            miniGameData = null;

            OnFinished?.Invoke(finishedDialogue);
            if (shouldStartMinigame)
            {
                OnMiniGameStartRequested?.Invoke(launchData, finishedDialogue);
            }
        }

        IEnumerator TypeSentence(string textToType, TMP_Text textComponent)
        {
            if (textComponent == null) yield break;

            textComponent.text = "";
            foreach (var c in textToType)
            {
                yield return typingDelay;
                textComponent.text += c;
            }

            ProcessNextSentence();
        }

        private void OnDestroy()
        {
            OnFinished = null;
            OnMiniGameStartRequested = null;
            ClearSpawnedPanels();
        }
        #endregion
    }
}
