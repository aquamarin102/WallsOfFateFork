using Game.Data;
using Game.UI;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
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

        [Inject]
        private void Construct(QuestManager questManager, NPCPrefabFactory factory)
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

            dialogueManager = DialogueManager.Instance;
            if (dialogueManager != null)
            {
                dialogueManager.OnFinished += OnDialogueFinished;
            }

            if (interactiveItemHandler != null)
            {
                interactiveItemHandler.OnItemHandled += OnQuestItemInteraction;
            }

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

                SetNpcActiveIfExists(keyMasterName, shouldShowKeyMaster);
            }

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

                SetNpcActiveIfExists(messengerName, shouldShowMessenger);
            }

            TryStartPendingMinigameDialogue();
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (dialogue == null)
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

        private void TryStartPendingMinigameDialogue()
        {
            Quest messengerQuest = questManager.GetQuest(4);
            if (messengerQuest == null)
            {
                return;
            }

            if (!questManager.TryConsumePendingMinigameDialogue(messengerQuest.Id, out string dialoguePath, out _))
            {
                return;
            }

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

        private void SetNpcActiveIfExists(string npcName, bool isActive)
        {
            GameObject npc = FindNpcInstance(npcName);
            if (npc != null)
            {
                npc.SetActive(isActive);
            }
        }

        private GameObject FindNpcInstance(string npcName)
        {
            if (npcPrefabFactory == null || string.IsNullOrWhiteSpace(npcName))
            {
                return null;
            }

            if (npcPrefabFactory.HasInstance(npcName))
            {
                return npcPrefabFactory.GetInstance(npcName);
            }

            string normalizedName = NormalizeNpcName(npcName);
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

            return null;
        }

        private static string NormalizeNpcName(string npcName)
        {
            int separatorIndex = npcName.IndexOf('_');
            return separatorIndex >= 0 ? npcName.Substring(0, separatorIndex) : npcName;
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
        }
    }
}
