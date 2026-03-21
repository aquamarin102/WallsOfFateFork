using Game.Data;
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
            if (dialogueManager != null)
            {
                dialogueManager.OnFinished += OnDialogueFinished;
            }

            if (interactiveItemHandler != null)
            {
                interactiveItemHandler.OnItemHandled += OnQuestItemInteraction;
            }

            Quest thiefQuest = questManager.GetQuest(1);
            if (thiefQuest != null && questManager.GetQuestState(thiefQuest.Id) == QuestState.InProgress)
            {
                GameObject thiefNpc = npcPrefabFactory != null ? npcPrefabFactory.GetInstance(ThiefPrefabName) : null;
                if (thiefNpc != null)
                {
                    thiefNpc.SetActive(false);
                }
            }
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (dialogue == null)
            {
                return;
            }

            if (dialogue.Name == "Thief")
            {
                Quest thiefQuest = questManager.GetQuest(1);
                if (thiefQuest == null)
                {
                    return;
                }

                questManager.UpdateQuest(thiefQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(thiefQuest.Id, 0);
                if (task != null)
                {
                    questManager.UpdateQuestTask(thiefQuest.Id, task.Id, QuestState.InProgress);
                }
            }
        }

        public void OnQuestItemInteraction(InteractableItemParameters itemParameters)
        {
            if (itemParameters.ItemName == "Pouch")
            {
                Quest herbalistQuest = questManager.GetQuest(2);
                if (herbalistQuest == null)
                {
                    return;
                }

                questManager.UpdateQuest(herbalistQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(herbalistQuest.Id, 0);
                if (task != null)
                {
                    questManager.UpdateQuestTask(herbalistQuest.Id, task.Id, QuestState.InProgress);
                }
            }
            if (itemParameters.ItemName == "Key")
            {
                Quest keyMasterQuest = questManager.GetQuest(3);
                if (keyMasterQuest != null && questManager.GetQuestState(keyMasterQuest.Id) == QuestState.InProgress)
                {
                    QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 0);
                    if (task != null)
                    {
                        questManager.UpdateQuestTask(keyMasterQuest.Id, task.Id, QuestState.Completed);
                    }

                    QuestTask task1 = questManager.GetQuestTask(keyMasterQuest.Id, 1);
                    if (task1 != null)
                    {
                        questManager.UpdateQuestTask(keyMasterQuest.Id, task1.Id, QuestState.InProgress);
                    }
                }
            }
            if (itemParameters.ItemName == "Scroll")
            {
                Quest messengerQuest = questManager.GetQuest(4);
                if (messengerQuest == null)
                {
                    return;
                }

                questManager.UpdateQuest(messengerQuest.Id, QuestState.InProgress);
                QuestTask task = questManager.GetQuestTask(messengerQuest.Id, 0);
                if (task != null)
                {
                    questManager.UpdateQuestTask(messengerQuest.Id, task.Id, QuestState.InProgress);
                }
            }
        }
    }
}
