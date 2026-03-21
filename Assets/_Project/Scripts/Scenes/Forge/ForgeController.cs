using Game.Data;
using Newtonsoft.Json;
using System;
using System.Collections;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class ForgeController : MonoBehaviour
    {
        private static readonly int[] PendingMinigameQuestIds = { 1, 2, 5 };

        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private InteractiveItemHandler interactiveItemHandler;
        [SerializeField] private DialogueHandler dialogueHandler;
        [SerializeField] private string ThiefPrefabName;
        [SerializeField] private string ChiefGuardfPrefabName;

        private QuestManager questManager;
        private NPCPrefabFactory npcPrefabFactory;
        private bool initialSceneStateApplied;
        private bool subscribedToDialogueEvents;
        private bool subscribedToItemEvents;
        private bool subscribedToDialogueHandlerEvents;
        private bool pendingDialogueChecked;

        [Inject]
        private void Construct([InjectOptional] QuestManager questManager, [InjectOptional] NPCPrefabFactory factory)
        {
            this.questManager = questManager;
            this.npcPrefabFactory = factory;
        }

        private void Start()
        {
            TryResolveDependencies();
            TrySubscribeToSceneEvents();
            TryApplyInitialSceneState();
            TryStartPendingMinigameDialogueIfNeeded();
        }

        private void Update()
        {
            if (initialSceneStateApplied &&
                subscribedToDialogueEvents &&
                (interactiveItemHandler == null || subscribedToItemEvents) &&
                (dialogueHandler == null || subscribedToDialogueHandlerEvents) &&
                pendingDialogueChecked)
            {
                return;
            }

            TryResolveDependencies();
            TrySubscribeToSceneEvents();
            TryApplyInitialSceneState();
            TryStartPendingMinigameDialogueIfNeeded();
        }

        private void OnDestroy()
        {
            if (subscribedToDialogueEvents && dialogueManager != null)
            {
                dialogueManager.OnFinished -= OnDialogueFinished;
            }

            if (subscribedToItemEvents && interactiveItemHandler != null)
            {
                interactiveItemHandler.OnItemHandled -= OnQuestItemInteraction;
            }

            if (subscribedToDialogueHandlerEvents && dialogueHandler != null)
            {
                dialogueHandler.OnDialogHandled -= OnDialogueInteraction;
            }
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (!TryResolveDependencies() || dialogue == null)
            {
                return;
            }

            bool hasMinigameTransition = DialogueContainsMinigameTransition(dialogue);

            if (dialogue.Name == "ChiefGuard" || dialogue.Name == "CaptainGuard")
            {
                if (hasMinigameTransition)
                {
                    return;
                }

                Quest thiefQuest = questManager.GetQuest(1);
                if (thiefQuest == null || questManager.GetQuestState(thiefQuest.Id) != QuestState.InProgress)
                {
                    return;
                }

                QuestStatus thiefQuestStatus = questManager.GetQuestStatus(thiefQuest.Id);
                QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 0);
                if (thiefQuestStatus == null || task == null)
                {
                    return;
                }

                if (!thiefQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus))
                {
                    return;
                }

                if (taskStatus.State == QuestState.InProgress || taskStatus.State == QuestState.Completed)
                {
                    questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.Completed);
                    questManager.UpdateQuest(thiefQuest.Id, QuestState.Completed);
                }
            }

            if (dialogue.Name == "Sofa")
            {
                if (hasMinigameTransition)
                {
                    return;
                }

                Quest herbalistQuest = questManager.GetQuest(2);
                if (herbalistQuest == null)
                {
                    return;
                }

                QuestStatus herbalistQuestStatus = questManager.GetQuestStatus(herbalistQuest.Id);
                QuestTask task = questManager.GetQuestTask(herbalistQuest.Id, 0);
                if (herbalistQuestStatus == null || task == null)
                {
                    return;
                }

                if (!herbalistQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus))
                {
                    return;
                }

                if (taskStatus.State == QuestState.InProgress)
                {
                    questManager.UpdateQuest(herbalistQuest.Id, QuestState.Completed);
                    questManager.UpdateQuestTask(herbalistQuest.Id, task.Id, QuestState.Completed);
                }
            }
        }

        public void OnDialogueInteraction(TriggerEvent eventData)
        {
            if (!TryResolveDependencies() || eventData?.TriggerObj == null)
            {
                return;
            }

            if (eventData.TriggerObj.gameObject.name != "Blacksmith")
            {
                return;
            }

            Quest blacksmithQuest = questManager.GetQuest(5);
            if (blacksmithQuest == null)
            {
                return;
            }

            QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);
            QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
            QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);
            if (blacksmithQuestStatus == null || task == null || task1 == null)
            {
                return;
            }

            blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
            blacksmithQuestStatus.TasksStatusData.TryGetValue(task1.Id, out TaskStatus taskStatus1);

            if (taskStatus.State == QuestState.NotStarted && taskStatus1.State == QuestState.NotStarted)
            {
                questManager.UpdateQuestTask(blacksmithQuest.Id, task.Id, QuestState.InProgress);
                questManager.UpdateQuest(blacksmithQuest.Id, QuestState.InProgress);

                var blacksmithDialogueJson = Resources.Load<TextAsset>("Dialogues/NPC/Blacksmith/First");
                var blacksmithDialogue = JsonConvert.DeserializeObject<DialogueGraph>(blacksmithDialogueJson.text);
                dialogueManager.StartDialogue(blacksmithDialogue);
            }

            if (taskStatus.State == QuestState.Completed && taskStatus1.State == QuestState.InProgress)
            {
                questManager.UpdateQuestTask(blacksmithQuest.Id, task1.Id, QuestState.Completed);
                questManager.UpdateQuest(blacksmithQuest.Id, QuestState.Completed);

                var blacksmithDialogueJson = Resources.Load<TextAsset>("Dialogues/NPC/Blacksmith/Second");
                var blacksmithDialogue = JsonConvert.DeserializeObject<DialogueGraph>(blacksmithDialogueJson.text);
                dialogueManager.StartDialogue(blacksmithDialogue);
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
            if (!TryResolveDependencies() || itemParameters == null)
            {
                return;
            }

            if (itemParameters.ItemName != "BlacksmithChest")
            {
                return;
            }

            Quest blacksmithQuest = questManager.GetQuest(5);
            if (blacksmithQuest == null)
            {
                return;
            }

            QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);
            QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
            QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);
            if (blacksmithQuestStatus == null || task == null || task1 == null)
            {
                return;
            }

            blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
            blacksmithQuestStatus.TasksStatusData.TryGetValue(task1.Id, out TaskStatus taskStatus1);
            if (taskStatus.State == QuestState.InProgress && taskStatus1.State == QuestState.NotStarted)
            {
                questManager.UpdateQuestTask(blacksmithQuest.Id, task.Id, QuestState.Completed);
                questManager.UpdateQuestTask(blacksmithQuest.Id, task1.Id, QuestState.InProgress);
            }
        }

        private void TryStartPendingMinigameDialogueIfNeeded()
        {
            if (pendingDialogueChecked || !TryResolveDependencies())
            {
                return;
            }

            string dialoguePath = ConsumePendingMinigameDialoguePath();
            if (string.IsNullOrWhiteSpace(dialoguePath))
            {
                pendingDialogueChecked = true;
                return;
            }

            pendingDialogueChecked = true;
            StartCoroutine(StartPendingDialogueNextFrame(dialoguePath));
        }

        private string ConsumePendingMinigameDialoguePath()
        {
            foreach (int questId in PendingMinigameQuestIds)
            {
                Quest quest = questManager.GetQuest(questId);
                if (quest == null)
                {
                    continue;
                }

                if (questManager.TryConsumePendingMinigameDialogue(quest.Id, out string dialoguePath, out _))
                {
                    return dialoguePath;
                }
            }

            return null;
        }

        private IEnumerator StartPendingDialogueNextFrame(string dialoguePath)
        {
            yield return null;

            if (dialogueManager == null)
            {
                dialogueManager = DialogueManager.Instance;
            }

            if (dialogueManager == null)
            {
                yield break;
            }

            while (dialogueManager.IsInDialogue)
            {
                yield return null;
            }

            DialogueGraph dialogueGraph = LoadDialogueGraph(dialoguePath);
            if (dialogueGraph == null)
            {
                yield break;
            }

            dialogueManager.StartDialogue(dialogueGraph);
        }

        private DialogueGraph LoadDialogueGraph(string dialoguePath)
        {
            TextAsset textAsset = Resources.Load<TextAsset>(dialoguePath);
            if (textAsset == null)
            {
                Debug.LogError($"ForgeController: failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<DialogueGraph>(textAsset.text);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"ForgeController: failed to deserialize dialogue graph at path: {dialoguePath}. Error: {ex.Message}");
                return null;
            }
        }

        private static bool DialogueContainsMinigameTransition(DialogueGraph dialogue)
        {
            return dialogue?.Sentences != null &&
                   dialogue.Sentences.Exists(sentence => sentence != null && sentence.StartMinigame);
        }

        private bool TryResolveDependencies()
        {
            questManager ??= QuestManager.Instance;
            dialogueManager ??= DialogueManager.Instance;
            return questManager != null;
        }

        private void TrySubscribeToSceneEvents()
        {
            if (!subscribedToDialogueEvents && dialogueManager != null)
            {
                dialogueManager.OnFinished += OnDialogueFinished;
                subscribedToDialogueEvents = true;
            }

            if (!subscribedToItemEvents && interactiveItemHandler != null)
            {
                interactiveItemHandler.OnItemHandled += OnQuestItemInteraction;
                subscribedToItemEvents = true;
            }

            if (!subscribedToDialogueHandlerEvents && dialogueHandler != null)
            {
                dialogueHandler.OnDialogHandled += OnDialogueInteraction;
                subscribedToDialogueHandlerEvents = true;
            }
        }

        private void TryApplyInitialSceneState()
        {
            if (initialSceneStateApplied || !TryResolveDependencies())
            {
                return;
            }

            Quest thiefQuest = questManager.GetQuest(1);
            bool shouldShowThiefSide = thiefQuest != null && questManager.GetQuestState(thiefQuest.Id) == QuestState.InProgress;

            bool thiefReady = SetNpcActiveIfExists(ThiefPrefabName, shouldShowThiefSide);
            bool chiefGuardReady = SetNpcActiveIfExists(ChiefGuardfPrefabName, shouldShowThiefSide);

            initialSceneStateApplied = thiefReady && chiefGuardReady;
        }

        private bool SetNpcActiveIfExists(string npcName, bool isActive)
        {
            if (string.IsNullOrWhiteSpace(npcName))
            {
                return true;
            }

            GameObject npc = FindNpcInstance(npcName);
            if (npc == null)
            {
                return false;
            }

            npc.SetActive(isActive);
            return true;
        }

        private GameObject FindNpcInstance(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
            {
                return null;
            }

            if (npcPrefabFactory != null && npcPrefabFactory.HasInstance(npcName))
            {
                return npcPrefabFactory.GetInstance(npcName);
            }

            string normalizedName = NormalizeNpcName(npcName);
            if (npcPrefabFactory != null)
            {
                foreach (var entry in npcPrefabFactory.instances)
                {
                    if (entry.Value == null)
                    {
                        continue;
                    }

                    if (string.Equals(entry.Key, npcName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(NormalizeNpcName(entry.Key), normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                Transform[] children = rootObjects[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    Transform child = children[childIndex];
                    if (child == null)
                    {
                        continue;
                    }

                    if (string.Equals(child.name, npcName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(NormalizeNpcName(child.name), normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }

        private static string NormalizeNpcName(string npcName)
        {
            int separatorIndex = npcName.IndexOf('_');
            return separatorIndex >= 0 ? npcName.Substring(0, separatorIndex) : npcName;
        }
    }
}
