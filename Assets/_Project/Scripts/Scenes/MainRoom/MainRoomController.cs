using Game.Data;
using Game.UI;
using System;
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
        [SerializeField] private string KeyMasterName;

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

            Quest keyMasterQuest = questManager.GetQuest(3);
            QuestStatus keyMasterQuestStatus = questManager.GetQuestStatus(keyMasterQuest.Id);

            QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 1);
            keyMasterQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);

            if (taskStatus.State == QuestState.InProgress)
            {
                npcPrefabFactory.GetInstance(KeyMasterName).gameObject.SetActive(true);
            }
            else
            {
                npcPrefabFactory.GetInstance(KeyMasterName).gameObject.SetActive(false);

            }

            TutorialSheetService.TryShowOnce(
                TutorialSheetDefinitions.MainRoomKey,
                TutorialSheetDefinitions.MainRoomResourcePath,
                TutorialSheetDefinitions.MainRoomEditorAssetPath,
                null);

        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (dialogue.Name == "Messenger")
            {
                Quest messengerQuest = questManager.GetQuest(4);
                QuestStatus messengerQuestStatus = questManager.GetQuestStatus(messengerQuest.Id);

                QuestTask task = questManager.GetQuestTask(messengerQuest.Id, 0);

                messengerQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);

                if (taskStatus.State == QuestState.InProgress)
                {
                    questManager.UpdateQuestTask(messengerQuest.Id, task.Id, QuestState.Completed);
                    questManager.UpdateQuest(messengerQuest.Id, QuestState.Completed);
                }
            }
            if (dialogue.Name == "KeyMaster")
            {
                Quest keyMasterQuest = questManager.GetQuest(3);
                if (questManager.GetQuestState(keyMasterQuest.Id) == QuestState.InProgress)
                {
                    QuestStatus keyMasterQuestStatus = questManager.GetQuestStatus(keyMasterQuest.Id);

                    QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 1);
                    QuestTask task1 = questManager.GetQuestTask(keyMasterQuest.Id, 2);

                    keyMasterQuestStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus);
                    keyMasterQuestStatus.TasksStatusData.TryGetValue(task1.Id, out TaskStatus taskStatus1);

                    if (taskStatus.State == QuestState.Completed && taskStatus1.State == QuestState.InProgress)
                    {
                        questManager.UpdateQuestTask(keyMasterQuest.Id, task1.Id, QuestState.Completed);
                        questManager.UpdateQuest(keyMasterQuest.Id, QuestState.Completed);
                    }

                }
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
        }
    }
}
