using Game.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class CellarController : MonoBehaviour
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
                npcPrefabFactory.GetInstance(ThiefPrefabName).gameObject.SetActive(false);
            }
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {

            if (dialogue.Name == "Thief")
            {
                Quest thiefQuest = questManager.GetQuest(1);
                questManager.UpdateQuest(thiefQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 0);
                questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.InProgress);
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
            if (itemParameters.ItemName == "Pouch")
            {
                Quest herbalistQuest = questManager.GetQuest(2);
                questManager.UpdateQuest(herbalistQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(herbalistQuest.Id, 0);
                questManager.UpdateQuestTask(herbalistQuest.Id, task.Id, QuestState.InProgress);
            }
            if (itemParameters.ItemName == "Key")
            {
                Quest keyMasterQuest = questManager.GetQuest(3);
                if(questManager.GetQuestState(keyMasterQuest.Id) == QuestState.InProgress)
                {
                    QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 0);
                    questManager.UpdateQuestTask(keyMasterQuest.Id, task.Id, QuestState.Completed);

                    QuestTask task1 = questManager.GetQuestTask(keyMasterQuest.Id, 1);
                    questManager.UpdateQuestTask(keyMasterQuest.Id, task1.Id, QuestState.InProgress);
                }
            }
            if (itemParameters.ItemName == "Scroll")
            {
                Quest messengerQuest = questManager.GetQuest(4);
                questManager.UpdateQuest(messengerQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(messengerQuest.Id, 0);
                questManager.UpdateQuestTask(messengerQuest.Id, task.Id, QuestState.InProgress);
            }
        }
    }
}
