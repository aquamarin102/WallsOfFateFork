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
        private bool initialSceneStateApplied;
        private bool subscribedToDialogueEvents;
        private bool subscribedToItemEvents;

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
        }

        private void Update()
        {
            if (initialSceneStateApplied &&
                subscribedToDialogueEvents &&
                (interactiveItemHandler == null || subscribedToItemEvents))
            {
                return;
            }

            TryResolveDependencies();
            TrySubscribeToSceneEvents();
            TryApplyInitialSceneState();
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

        private void TryApplyInitialSceneState()
        {
            if (initialSceneStateApplied || questManager == null)
            {
                return;
            }

            Quest thiefQuest = questManager.GetQuest(1);
            if (thiefQuest != null && questManager.GetQuestState(thiefQuest.Id) == QuestState.InProgress)
            {
                GameObject thiefNpc = FindNpcInstance(ThiefPrefabName);
                if (thiefNpc != null)
                {
                    thiefNpc.SetActive(false);
                }
            }

            initialSceneStateApplied = true;
        }

        public void OnDialogueFinished(DialogueGraph dialogue)
        {
            if (!TryResolveDependencies())
            {
                return;
            }

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
            if (!TryResolveDependencies() || itemParameters == null)
            {
                return;
            }

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

            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            for (int index = 0; index < rootObjects.Length; index++)
            {
                Transform[] children = rootObjects[index].GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < children.Length; childIndex++)
                {
                    Transform child = children[childIndex];
                    if (child != null && child.name == npcName)
                    {
                        return child.gameObject;
                    }
                }
            }

            return null;
        }
    }
}
