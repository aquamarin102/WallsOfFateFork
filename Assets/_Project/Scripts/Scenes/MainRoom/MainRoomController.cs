using Game.Data;
using Game.UI;
using Newtonsoft.Json;
using System;
using System.Collections;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class MainRoomController : MonoBehaviour
    {
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private InteractiveItemHandler interactiveItemHandler;
        [SerializeField] private string keyMasterName;
        [SerializeField] private string messengerName;

        private QuestManager questManager;
        private NPCPrefabFactory npcPrefabFactory;
        private bool initialSceneStateApplied;
        private bool subscribedToDialogueEvents;
        private bool subscribedToItemEvents;
        private bool pendingDialogueChecked;

        [Inject]
        private void Construct([InjectOptional] QuestManager questManager, [InjectOptional] NPCPrefabFactory factory)
        {
            this.questManager = questManager;
            this.npcPrefabFactory = factory;
        }

        private void Start()
        {
            TutorialSheetService.TryShowOnce(
                TutorialSheetDefinitions.MainRoomKey,
                TutorialSheetDefinitions.MainRoomResourcePath,
                TutorialSheetDefinitions.MainRoomEditorAssetPath,
                null);

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
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (!TryResolveDependencies() || dialogue == null)
            {
                return;
            }

            if (dialogue.Name == "Messenger")
            {
                if (DialogueContainsMinigameTransition(dialogue))
                {
                    return;
                }

                Quest messengerQuest = questManager.GetQuest(4);
                if (messengerQuest == null)
                {
                    return;
                }

                QuestStatus messengerQuestStatus = questManager.GetQuestStatus(messengerQuest.Id);
                QuestTask task = questManager.GetQuestTask(messengerQuest.Id, 0);
                if (messengerQuestStatus == null || task == null)
                {
                    return;
                }

                if (!messengerQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus))
                {
                    return;
                }

                if (taskStatus.State == QuestState.InProgress)
                {
                    questManager.UpdateQuestTask(messengerQuest.Id, task.Id, QuestState.Completed);
                    questManager.UpdateQuest(messengerQuest.Id, QuestState.Completed);
                }
            }

            if (dialogue.Name == "KeyMaster")
            {
                Quest keyMasterQuest = questManager.GetQuest(3);
                if (keyMasterQuest == null || questManager.GetQuestState(keyMasterQuest.Id) != QuestState.InProgress)
                {
                    return;
                }

                QuestStatus keyMasterQuestStatus = questManager.GetQuestStatus(keyMasterQuest.Id);
                QuestTask dialogueTask = questManager.GetQuestTask(keyMasterQuest.Id, 1);
                if (keyMasterQuestStatus == null || dialogueTask == null)
                {
                    return;
                }

                if (!keyMasterQuestStatus.TasksStatusData.TryGetValue(dialogueTask.Id, out TaskStatus taskStatus))
                {
                    return;
                }

                if (taskStatus.State == QuestState.InProgress)
                {
                    questManager.UpdateQuestTask(keyMasterQuest.Id, dialogueTask.Id, QuestState.Completed);
                    questManager.UpdateQuest(keyMasterQuest.Id, QuestState.Completed);
                }
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
        }

        private void TryStartPendingMinigameDialogueIfNeeded()
        {
            if (pendingDialogueChecked || !TryResolveDependencies())
            {
                return;
            }

            Quest messengerQuest = questManager.GetQuest(4);
            if (messengerQuest == null)
            {
                pendingDialogueChecked = true;
                return;
            }

            if (!questManager.TryConsumePendingMinigameDialogue(messengerQuest.Id, out string dialoguePath, out _))
            {
                pendingDialogueChecked = true;
                return;
            }

            pendingDialogueChecked = true;
            StartCoroutine(StartPendingDialogueNextFrame(dialoguePath));
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
                Debug.LogError($"MainRoomController: failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<DialogueGraph>(textAsset.text);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"MainRoomController: failed to deserialize dialogue graph at path: {dialoguePath}. Error: {ex.Message}");
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
        }

        private void TryApplyInitialSceneState()
        {
            if (initialSceneStateApplied || !TryResolveDependencies())
            {
                return;
            }

            bool keyMasterReady = true;
            Quest keyMasterQuest = questManager.GetQuest(3);
            if (keyMasterQuest != null)
            {
                QuestStatus keyMasterQuestStatus = questManager.GetQuestStatus(keyMasterQuest.Id);
                QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 1);

                bool shouldShowKeyMaster =
                    keyMasterQuestStatus != null &&
                    task != null &&
                    keyMasterQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus) &&
                    taskStatus.State == QuestState.InProgress;

                keyMasterReady = SetNpcActiveIfExists(keyMasterName, shouldShowKeyMaster);
            }

            bool messengerReady = true;
            Quest messengerQuest = questManager.GetQuest(4);
            if (messengerQuest != null)
            {
                QuestStatus messengerQuestStatus = questManager.GetQuestStatus(messengerQuest.Id);
                QuestTask task = questManager.GetQuestTask(messengerQuest.Id, 0);

                bool shouldShowMessenger =
                    messengerQuestStatus != null &&
                    task != null &&
                    messengerQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus messengerTaskStatus) &&
                    messengerTaskStatus.State == QuestState.InProgress;

                messengerReady = SetNpcActiveIfExists(messengerName, shouldShowMessenger);
            }

            initialSceneStateApplied = keyMasterReady && messengerReady;
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
