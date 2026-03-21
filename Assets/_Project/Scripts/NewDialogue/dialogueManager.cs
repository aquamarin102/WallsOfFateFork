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
        [SerializeField] private float characterRevealDelay = 0.045f;
        [SerializeField] private float nextSentenceDelay = 0.18f;
        [SerializeField] private float firstSentenceDelay = 0.1f;
        [SerializeField] private float optionTextScale = 0.8f;

        private List<GameObject> spawnedPanels = new();

        private static DialogueManager _instance;
        private PlayerManager playerManager;

        private GameObject optionPrefab;
        private LimitY scrollController;
        private Coroutine queuedSentenceRoutine;
        private Coroutine activeTypingRoutine;
        private TMP_Text activeTextComponent;

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

            if (SpawnPoint != null)
            {
                scrollController = SpawnPoint.GetComponent<LimitY>();
            }

            playerManager ??= PlayerManager.Instance;
            _ = EntryPoint.Instance;
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

        private void StopActiveTyping()
        {
            if (activeTypingRoutine != null)
            {
                StopCoroutine(activeTypingRoutine);
                activeTypingRoutine = null;
            }

            activeTextComponent = null;
        }

        private void ClearOptionsList()
        {
            if (OptionsList == null)
            {
                return;
            }

            foreach (Transform child in OptionsList.transform)
            {
                Destroy(child.gameObject);
            }
        }

        private void RefreshDialogueLayout(bool scrollToLatest = false, bool immediate = false)
        {
            if (SpawnPoint == null)
            {
                return;
            }

            scrollController ??= SpawnPoint.GetComponent<LimitY>();

            if (scrollController != null)
            {
                if (scrollToLatest)
                {
                    scrollController.ScrollToLatest(immediate);
                }
                else
                {
                    scrollController.RefreshLayoutAndClamp();
                }

                return;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(SpawnPoint);
        }

        private void ResetDialogueScroll()
        {
            scrollController ??= SpawnPoint != null ? SpawnPoint.GetComponent<LimitY>() : null;

            if (scrollController != null)
            {
                scrollController.ResetScrollPosition();
                return;
            }

            RefreshDialogueLayout();
        }

        private void QueueCurrentSentenceDisplay(float delay)
        {
            if (queuedSentenceRoutine != null)
            {
                StopCoroutine(queuedSentenceRoutine);
            }

            queuedSentenceRoutine = StartCoroutine(DisplayCurrentSentenceAfterDelay(delay));
        }

        private IEnumerator DisplayCurrentSentenceAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            queuedSentenceRoutine = null;

            if (!IsInDialogue || currentSentence == null)
            {
                yield break;
            }

            DisplayCurrentSentence();
        }

        private void FocusSentencePanel(GameObject sentencePanel, bool immediate = false)
        {
            if (sentencePanel == null || SpawnPoint == null)
            {
                RefreshDialogueLayout(scrollToLatest: true, immediate: immediate);
                return;
            }

            scrollController ??= SpawnPoint.GetComponent<LimitY>();

            if (scrollController != null)
            {
                RectTransform sentenceRect = sentencePanel.GetComponent<RectTransform>();
                scrollController.FocusOnChild(sentenceRect, immediate);
                return;
            }

            RefreshDialogueLayout(scrollToLatest: true, immediate: immediate);
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
            playerManager ??= PlayerManager.Instance;
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

            if (IsInDialogue && currentOptions.Count == 0 && IsFastForwardInputPressed())
            {
                FastForwardDialogue();
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
            currentOptions.Clear();

            StopActiveTyping();
            ClearSpawnedPanels();
            ClearOptionsList();

            if (resourcesUI != null) {
                resourcesUI.SetActive(false);
            }

            if (DialogueUI != null)
            {
                Sprite mySprite = Resources.Load<Sprite>("Characters/Portraits/" + this.currentDialogue.Portrait);
                NPCPortrait.sprite = mySprite;

                DialogueUI.SetActive(true);
            }

            ResetDialogueScroll();
            currentSentence = currentDialogue.Sentences[0];
            QueueCurrentSentenceDisplay(firstSentenceDelay);
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
                sentencePanel = Instantiate(panelPrefab, SpawnPoint, false);
                sentencePanel.transform.SetAsLastSibling();
                spawnedPanels.Add(sentencePanel);

                return true;
            }

            return false;
        }

        private bool IsFastForwardInputPressed()
        {
            return Input.GetMouseButtonDown(0) ||
                   Input.GetMouseButtonDown(1) ||
                   Input.GetMouseButtonDown(2) ||
                   Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private void FastForwardDialogue()
        {
            if (activeTypingRoutine != null)
            {
                CompleteCurrentSentenceImmediately();
                return;
            }

            if (queuedSentenceRoutine != null)
            {
                StopCoroutine(queuedSentenceRoutine);
                queuedSentenceRoutine = null;
                DisplayCurrentSentence();
            }
        }

        private void CompleteCurrentSentenceImmediately()
        {
            if (activeTypingRoutine != null)
            {
                StopCoroutine(activeTypingRoutine);
                activeTypingRoutine = null;
            }

            if (activeTextComponent != null)
            {
                activeTextComponent.maxVisibleCharacters = activeTextComponent.textInfo.characterCount;
            }

            activeTextComponent = null;
            AdvanceToNextSentence(immediate: true);
        }

        public void SelectOption(int optionIndex)
        {           
            var optionSentence = currentOptions[optionIndex];

            ClearOptionsList();
            currentOptions.Clear();

            currentSentence = optionSentence;
            QueueCurrentSentenceDisplay(nextSentenceDelay * 0.5f);
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
                    FocusSentencePanel(sentencePanel, immediate: true);
                    activeTypingRoutine = StartCoroutine(TypeSentence(currentSentence.Text, sentenceText));
                }
            }
        }

        private void UpdateResources()
        {
            if (currentSentence == null)
                return;

            playerManager ??= PlayerManager.Instance;
            if (playerManager?.PlayerData == null)
            {
                return;
            }

            playerManager.PlayerData.AddResource(ResourceType.Gold, currentSentence.Gold);
            playerManager.PlayerData.AddResource(ResourceType.Food, currentSentence.Food);
            playerManager.PlayerData.AddResource(ResourceType.PeopleSatisfaction, currentSentence.PeopleSatisfaction);
            playerManager.PlayerData.AddResource(ResourceType.CastleStrength, currentSentence.CastleStrength);
        }

        private void ProcessNextSentence()
        {
            AdvanceToNextSentence();
        }

        private void AdvanceToNextSentence(bool immediate = false)
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

            if (immediate)
            {
                DisplayCurrentSentence();
                return;
            }

            QueueCurrentSentenceDisplay(nextSentenceDelay);
        }

        private void LoadAllOptions()
        {
            currentOptions.Clear();

            if (currentDialogue == null) return;

            if (OptionsList == null || optionPrefab == null)
            {
                return;
            }

            ClearOptionsList();
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
                if (optionTextComponent != null)
                {
                    ApplyOptionTextSizing(optionTextComponent);
                    optionTextComponent.text = optionCounter + ". " + currentSentence.Text;
                }

                optionCounter++;

                if (currentDialogue.Sentences.IndexOf(currentSentence) + 1 >= currentDialogue.Sentences.Count)
                {
                    break;
                }

                currentSentence = currentDialogue.Sentences[currentDialogue.Sentences.IndexOf(currentSentence) + 1];
            }

            RefreshDialogueLayout(scrollToLatest: true);
        }

        private void ApplyOptionTextSizing(TMP_Text optionTextComponent)
        {
            float clampedScale = Mathf.Clamp(optionTextScale, 0.5f, 1f);

            if (optionTextComponent.enableAutoSizing)
            {
                optionTextComponent.fontSizeMin = Mathf.Max(1f, optionTextComponent.fontSizeMin * clampedScale);
                optionTextComponent.fontSizeMax = Mathf.Max(optionTextComponent.fontSizeMin, optionTextComponent.fontSizeMax * clampedScale);
                return;
            }

            optionTextComponent.fontSize = Mathf.Max(1f, optionTextComponent.fontSize * clampedScale);
        }

        private void CloseDialogue()
        {
            StopActiveTyping();
            ClearOptionsList();
            currentOptions.Clear();

            if (queuedSentenceRoutine != null)
            {
                StopCoroutine(queuedSentenceRoutine);
                queuedSentenceRoutine = null;
            }

            StartCoroutine(CloseDialogueWithDelay(1f));
        }

        private IEnumerator CloseDialogueWithDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            var finishedDialogue = currentDialogue;
            bool shouldStartMinigame = startMinigame && miniGameData != null;
            MiniGameData launchData = miniGameData;
            EntryPoint entryPoint = shouldStartMinigame ? EntryPoint.Instance : null;

            if (resourcesUI != null)
            {
                resourcesUI.gameObject.SetActive(true);
            }

            if (DialogueUI != null)
            {
                DialogueUI.SetActive(false);
            }

            StopActiveTyping();
            ClearSpawnedPanels();
            queuedSentenceRoutine = null;

            IsInDialogue = false;
            currentDialogue = null;
            currentSentence = null;
            startMinigame = false;
            miniGameData = null;

            OnFinished?.Invoke(finishedDialogue);
            if (shouldStartMinigame)
            {
                if (OnMiniGameStartRequested != null)
                {
                    OnMiniGameStartRequested.Invoke(launchData, finishedDialogue);
                }
                else
                {
                    entryPoint?.LaunchMinigame(launchData, finishedDialogue);
                }
            }
        }

        IEnumerator TypeSentence(string textToType, TMP_Text textComponent)
        {
            if (textComponent == null) yield break;

            activeTextComponent = textComponent;
            textComponent.text = textToType;
            textComponent.maxVisibleCharacters = 0;
            textComponent.ForceMeshUpdate();

            FocusSentencePanel(textComponent.transform.parent.gameObject, immediate: true);

            int totalVisibleCharacters = textComponent.textInfo.characterCount;

            if (totalVisibleCharacters == 0)
            {
                activeTextComponent = null;
                activeTypingRoutine = null;
                ProcessNextSentence();
                yield break;
            }

            for (int visibleCharacters = 1; visibleCharacters <= totalVisibleCharacters; visibleCharacters++)
            {
                textComponent.maxVisibleCharacters = visibleCharacters;
                yield return new WaitForSecondsRealtime(characterRevealDelay);
            }

            textComponent.maxVisibleCharacters = totalVisibleCharacters;
            activeTextComponent = null;
            activeTypingRoutine = null;

            ProcessNextSentence();
        }

        private void OnDestroy()
        {
            StopActiveTyping();

            if (queuedSentenceRoutine != null)
            {
                StopCoroutine(queuedSentenceRoutine);
            }

            OnFinished = null;
            OnMiniGameStartRequested = null;
            ClearSpawnedPanels();
        }
        #endregion
    }
}
