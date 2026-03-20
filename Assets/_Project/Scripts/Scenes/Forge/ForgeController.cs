using Game.Data;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class ForgeController : MonoBehaviour
    {
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private InteractiveItemHandler interactiveItemHandler;
        [SerializeField] private string ThiefPrefabName;

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
            dialogueManager.OnFinished += OnDialogueFinished;

            interactiveItemHandler.OnItemHandled += OnQuestItemInteraction;

            Quest thiefQuest = questManager.GetQuest(1);
            QuestState thiefQuestState = questManager.GetQuestState(thiefQuest.Id);
            if (thiefQuestState == QuestState.InProgress)
            {
                npcPrefabFactory.GetInstance(ThiefPrefabName).gameObject.SetActive(true);
            }
            else
            {
                npcPrefabFactory.GetInstance(ThiefPrefabName).gameObject.SetActive(false);
            }
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {

            if (dialogue.Name == "Thief")
            {
                Quest thiefQuest = questManager.GetQuest(1);
                QuestStatus thiefQuestStatus = questManager.GetQuestStatus(thiefQuest.Id);

                QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 0);
                questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.Completed);

                QuestTask task1 = questManager.GetQuestTask(thiefQuest.Id, 1);
                questManager.UpdateQuestTask(thiefQuest.Id, task1.Id, QuestState.InProgress);
            }
            if (dialogue.Name == "ChiefGuard")
            {
                Quest thiefQuest = questManager.GetQuest(1);

                QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 1);
                questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.Completed);

                questManager.UpdateQuest(thiefQuest.Id, QuestState.Completed);

            }
            if(dialogue.Name == "Herbalist")
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
            if(dialogue.Name == "Blacksmith")
            {
                Quest blacksmithQuest = questManager.GetQuest(4);
                QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);

                QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
                QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);

                blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
                blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus1);
                if (taskStatus.State == QuestState.NotStarted && taskStatus1.State == QuestState.NotStarted)
                {
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task.Id, QuestState.InProgress);
                }
                if (taskStatus.State == QuestState.Completed && taskStatus1.State == QuestState.InProgress)
                {
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task1.Id, QuestState.Completed);
                    questManager.UpdateQuest(blacksmithQuest.Id, QuestState.Completed);
                }
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
            if (itemParameters.ItemName == "BlacksmithChest")
            {
                Quest blacksmithQuest = questManager.GetQuest(4);
                QuestStatus blacksmithQuestStatus = questManager.GetQuestStatus(blacksmithQuest.Id);

                QuestTask task = questManager.GetQuestTask(blacksmithQuest.Id, 0);
                QuestTask task1 = questManager.GetQuestTask(blacksmithQuest.Id, 1);

                blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
                blacksmithQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus1);
                if (taskStatus.State == QuestState.InProgress && taskStatus1.State == QuestState.NotStarted)
                {
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task.Id, QuestState.Completed);
                    questManager.UpdateQuestTask(blacksmithQuest.Id, task1.Id, QuestState.InProgress);
                }
            }
        }
    }
}