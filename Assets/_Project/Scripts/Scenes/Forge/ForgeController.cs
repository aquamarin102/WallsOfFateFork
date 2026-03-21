using Game.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
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

        [Inject]
        private void Construct(QuestManager questManager, NPCPrefabFactory factory)
        {
            this.questManager = questManager;
            this.npcPrefabFactory = factory;
        }

        private void Start()
        {
            dialogueManager = DialogueManager.Instance;
            if (dialogueManager != null)
            {
                dialogueManager.OnFinished += OnDialogueFinished;
            }

            if (interactiveItemHandler != null)
            {
                interactiveItemHandler.OnItemHandled += OnQuestItemInteraction;
            }

            if (dialogueHandler != null)
            {
                dialogueHandler.OnDialogHandled += OnDialogueInteraction;
            }

            Quest thiefQuest = questManager.GetQuest(1);
            if (thiefQuest != null && questManager.GetQuestState(thiefQuest.Id) == QuestState.InProgress)
            {
                SetNpcActiveIfExists(ThiefPrefabName, true);
                SetNpcActiveIfExists(ChiefGuardfPrefabName, true);
            }
            else
            {
                SetNpcActiveIfExists(ThiefPrefabName, false);
                SetNpcActiveIfExists(ChiefGuardfPrefabName, false);
            }

            TryStartPendingMinigameDialogue();
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (dialogue == null)
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
            if (eventData.TriggerObj.gameObject.name == "Blacksmith")
            {
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
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
            if (itemParameters.ItemName == "BlacksmithChest")
            {
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
        }

        private void TryStartPendingMinigameDialogue()
        {
            string dialoguePath = ConsumePendingMinigameDialoguePath();
            if (string.IsNullOrWhiteSpace(dialoguePath))
            {
                return;
            }

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

            return null;
        }
    }
}
