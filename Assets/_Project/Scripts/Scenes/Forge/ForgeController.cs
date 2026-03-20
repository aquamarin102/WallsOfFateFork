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

            dialogueHandler.OnDialogHandled += OnDialogueInteraction;

            Quest thiefQuest = questManager.GetQuest(1);
            QuestState thiefQuestState = questManager.GetQuestState(thiefQuest.Id);
            if (thiefQuestState == QuestState.InProgress)
            {
                npcPrefabFactory.GetInstance(ThiefPrefabName).gameObject.SetActive(true);
                npcPrefabFactory.GetInstance(ChiefGuardfPrefabName).gameObject.SetActive(true);
            }
            else
            {
                npcPrefabFactory.GetInstance(ThiefPrefabName).gameObject.SetActive(false);
                npcPrefabFactory.GetInstance(ChiefGuardfPrefabName).gameObject.SetActive(false);
            }

            TryStartPendingMinigameDialogue();
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            DialogueManager dialogueManager = DialogueManager.Instance;
            if (dialogue == null)
            {
                return;
            }

            if (dialogue.Name == "ChiefGuard")
            {
                Quest thiefQuest = questManager.GetQuest(1);
                if (questManager.GetQuestState(thiefQuest.Id) == QuestState.InProgress)
                {
                    QuestStatus thiefQuestStatus = questManager.GetQuestStatus(thiefQuest.Id);

                    QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 0);
                    thiefQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);

                    if (taskStatus.State == QuestState.Completed)
                    {
                        questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.Completed);
                        questManager.UpdateQuest(thiefQuest.Id, QuestState.Completed);
                    }
                }
            }
            if(dialogue.Name == "Sofa")
            {
                Quest herbalistQuest = questManager.GetQuest(2);
                QuestStatus herbalistQuestStatus = questManager.GetQuestStatus(herbalistQuest.Id);

                QuestTask task = questManager.GetQuestTask(herbalistQuest.Id, 0);
                herbalistQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);

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
                QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);

                QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
                QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);

                blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
                blacksmithQuestStatus.TasksStatusData.TryGetValue(task1.Id, out TaskStatus taskStatus1);
                if (taskStatus.State == QuestState.NotStarted && taskStatus1.State == QuestState.NotStarted)
                {
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task.Id, QuestState.InProgress);
                    var blacksmithDialogueJson = Resources.Load<TextAsset>("Dialogues/NPC/Dialogues/Blacksmith/First");
                    var blacksmithDialogue = JsonConvert.DeserializeObject<DialogueGraph>(blacksmithDialogueJson.text);
                    dialogueManager.StartDialogue(blacksmithDialogue);
                }

                if (taskStatus.State == QuestState.Completed && taskStatus1.State == QuestState.InProgress)
                {
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task1.Id, QuestState.Completed);
                    questManager.UpdateQuest(blacksmithQuest.Id, QuestState.Completed);
                    var blacksmithDialogueJson = Resources.Load<TextAsset>("Dialogues/NPC/Dialogues/Blacksmith/Second");
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
                QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);

                QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
                QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);

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
            Quest blacksmithQuest = questManager.GetQuest(5);
            if (blacksmithQuest == null)
            {
                return;
            }

            if (!questManager.TryConsumePendingMinigameDialogue(blacksmithQuest.Id, out string dialoguePath, out _))
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
    }
}
