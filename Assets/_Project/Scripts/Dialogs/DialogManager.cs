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
    public class DialogManager : MonoBehaviour
    {
        #region MainInfo
        private DialogGraph currentDialog;
        private Sentence currentSentence;
        private List<Sentence> currentOptions = new();
        #endregion

        private GameObject dialogContainer;
        private RectTransform spawnPoint;
        private LimitY scrollController;
        private GameObject optionsList;
        private Image npcPortrait;

        #region Utility
        [SerializeField] private float characterRevealDelay = 0.025f;
        [SerializeField] private float nextSentenceDelay = 0.35f;
        [SerializeField] private float firstSentenceDelay = 0.1f;
        [SerializeField] private float optionTextScale = 0.8f;

        private readonly List<GameObject> spawnedPanels = new();

        private PlayerManager playerManager;

        private GameObject optionPrefab;
        private GameObject playerPanelPrefab;
        private GameObject npcPanelPrefab;

        private Coroutine queuedSentenceRoutine;
        private Coroutine activeTypingRoutine;
        private TMP_Text activeTextComponent;

        public static DialogManager Instance { get; private set; }

        bool startMinigame;
        MiniGameData miniGameData;
        #endregion

        #region Events
        public event Action<DialogGraph> OnFinished;
        public event Action<DialogGraph> OnStarted;
        public event Action<MiniGameData, DialogGraph> OnMiniGameStartRequested;
        #endregion

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        public bool Active => currentDialog != null;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            dialogContainer = transform.Find("DialogContainer").gameObject;

            var scrollContent = dialogContainer.transform.Find("Scroll/View/Content");

            spawnPoint = scrollContent.transform as RectTransform;
            scrollController = scrollContent.GetComponent<LimitY>();
            optionsList = dialogContainer.transform.Find("OptionsPanel/OptionsContainer/View/OptionsList").gameObject;
            npcPortrait = dialogContainer.transform.Find("NPCPortrait").GetComponent<Image>();
        }

        private void Start()
        {
            optionPrefab = Resources.Load<GameObject>("UI/Dialog/Option");
            npcPanelPrefab = Resources.Load<GameObject>("UI/Dialog/NPCPanelMedium");
            playerPanelPrefab = Resources.Load<GameObject>("UI/Dialog/PlayerPanelMedium");
        }

        private void OnDestroy()
        {
            StopActiveTyping();

            if (queuedSentenceRoutine != null)
            {
                StopCoroutine(queuedSentenceRoutine);
            }

            OnFinished = null;
            OnStarted = null;
            OnMiniGameStartRequested = null;
            Instance = null;
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

                return;
            }

            if (Active && currentOptions.Count == 0 && IsFastForwardInputPressed())
            {
                FastForwardDialogue();
            }
        }

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
            currentOptions.Clear();

            if (optionsList == null)
            {
                return;
            }

            foreach (Transform child in optionsList.transform)
            {
                Destroy(child.gameObject);
            }
        }

        private void RefreshDialogueLayout(bool scrollToLatest = false, bool immediate = false)
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

            if (currentSentence == null)
            {
                yield break;
            }

            DisplayCurrentSentence();
        }

        private void FocusSentencePanel(GameObject sentencePanel, bool immediate = false)
        {
            RectTransform sentenceRect = sentencePanel.GetComponent<RectTransform>();
            scrollController.FocusOnChild(sentenceRect, immediate);
            return;
        }

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

        public void StartDialog(DialogGraph dialog)
        {          
            if (Active || dialog == null)
            {
                return;
            }

            currentDialog = dialog;

            startMinigame = false;
            miniGameData = null;

            StopActiveTyping();
            ClearSpawnedPanels();
            ClearOptionsList();

            Sprite npcPortraitImage = Resources.Load<Sprite>("Characters/Portraits/" + this.currentDialog.Portrait);
            npcPortrait.sprite = npcPortraitImage;

            dialogContainer.SetActive(true);
            OnStarted?.Invoke(currentDialog);

            scrollController.ResetScrollPosition();
            currentSentence = dialog.Sentences[0];
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
                panelPrefab = playerPanelPrefab;
            }
            else
            {
                panelPrefab = npcPanelPrefab;
            }

            if (panelPrefab != null && spawnPoint != null)
            {
                sentencePanel = Instantiate(panelPrefab, spawnPoint, false);
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
                // Keep the next-line pause readable while the player clicks through text.
                return;
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
            AdvanceToNextSentence();
        }

        public void SelectOption(int optionIndex)
        {           
            var optionSentence = currentOptions[optionIndex];

            ClearOptionsList();

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
                TMP_Text nameText = sentencePanel.transform.Find("Name")?.GetComponent<TMP_Text>();

                if (sentenceText != null)
                {
                    FocusSentencePanel(sentencePanel, immediate: true);
                    activeTypingRoutine = StartCoroutine(TypeSentence(currentSentence.Text, sentenceText));
                    nameText.text = currentDialog.CharacterName;
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
            AdvanceToNextSentence();
        }

        private void AdvanceToNextSentence(bool immediate = false)
        {
            var nextSentence = currentDialog.Sentences.Find(s => s.Id == currentSentence.NextSentenceId);
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
            ClearOptionsList();

            var optionCounter = 1;
            while (currentSentence.IsOption)
            {
                currentOptions.Add(currentSentence);
                var optionObject = Instantiate(optionPrefab);
                optionObject.transform.SetParent(optionsList.transform, false);

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

                if (currentDialog.Sentences.IndexOf(currentSentence) + 1 >= currentDialog.Sentences.Count)
                {
                    break;
                }

                currentSentence = currentDialog.Sentences[currentDialog.Sentences.IndexOf(currentSentence) + 1];
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
            ClearOptionsList();

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

            var finishedDialogue = currentDialog;
            bool shouldStartMinigame = startMinigame && miniGameData != null;
            MiniGameData launchData = miniGameData;

            dialogContainer.SetActive(false);

            StopActiveTyping();
            ClearSpawnedPanels();
            queuedSentenceRoutine = null;

            currentDialog = null;
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
                    //entryPoint?.LaunchMinigame(launchData, finishedDialogue);
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
    }
}
