using Game.Core;
using Game.Data;
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace Game
{
    public class InteractibleItemInfluenceArea : InfluenceArea
    {
        [Header("Item Parameters")]
        [SerializeField] private InteractableItemParameters itemParameters;

        [Header("Item State")]
        [SerializeField] private bool hasBeenUsed = false;

        public AsyncEvent<(TriggerEvent, InteractableItemParameters)> OnItemInteracted { get; } = new();
        public bool HasBeenUsed => hasBeenUsed;
        private QuestManager questManager;

        [Inject]
        public void Construct(QuestManager questManager)
        {
            this.questManager = questManager;
        }

        private void Start()
        {
            LoadItemState();
            if (triggerObject != null) triggerObject = this.gameObject;
            if (!hasBeenUsed) this.gameObject.SetActive(false);
        }

        public void MarkAsUsed()
        {
            if (hasBeenUsed) return;

            hasBeenUsed = true;
            SaveItemState();
        }

        public void ResetForRespawn()
        {
            hasBeenUsed = false;

            if (TryGetComponent<Collider>(out var col))
                col.enabled = true;

            foreach (var o in GetComponentsInChildren<cakeslice.Outline>())
                o.enabled = true;

            string scene = SceneManager.GetActiveScene().name;
            InteractableItemCollection.SetItemState(scene, gameObject.name, false);
        }

        private void LoadItemState()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            InteractableItemCollection.TryGetItemState(sceneName, gameObject.name, out hasBeenUsed);
        }

        private void SaveItemState()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            InteractableItemCollection.SetItemState(sceneName, gameObject.name, hasBeenUsed);
        }

        public override async Task InvokeEventAsync(Collider obj)
        {
            if (!hasBeenUsed)
            {
                bool interacted = ConsumeInteractPress();
                TriggerEvent eventData = new TriggerEvent(
                    AreaType,
                    obj.gameObject,
                    triggerObject,
                    interacted,
                    string.Empty
                );

                if (itemParameters.questId > -1 && itemParameters.questTaskId > -1)
                {
                    Quest quest = questManager.GetQuest(itemParameters.questId);
                    QuestStatus questStatus = questManager.GetQuestStatus(quest.Id);

                    QuestTask task = questManager.GetQuestTask(quest.Id, itemParameters.questTaskId);
                    questStatus.TasksStatusData.TryGetValue(task.Id, out Game.Data.TaskStatus taskStatus);

                    if (taskStatus.State != QuestState.NotStarted)
                    {
                        if (interacted)
                        {
                            MarkAsUsed();
                        }
                        await OnItemInteracted.InvokeAsync((eventData, itemParameters));
                    }
                }
                else
                {

                    if (interacted)
                    {
                        MarkAsUsed();
                    }

                    await OnItemInteracted.InvokeAsync((eventData, itemParameters));
                }

            }
        }

        public override async Task InvokeDirectInteractionAsync(GameObject playerObj)
        {
            if (hasBeenUsed)
                return;

            GameObject targetObject = triggerObject != null ? triggerObject : gameObject;
            TriggerEvent eventData = new TriggerEvent(
                AreaType,
                playerObj,
                targetObject,
                true,
                string.Empty
            );

            if (itemParameters.questId > -1 && itemParameters.questTaskId > -1)
            {
                Quest quest = questManager.GetQuest(itemParameters.questId);
                QuestStatus questStatus = questManager.GetQuestStatus(quest.Id);

                QuestTask task = questManager.GetQuestTask(quest.Id, itemParameters.questTaskId);
                questStatus.TasksStatusData.TryGetValue(task.Id, out Game.Data.TaskStatus taskStatus);

                if (taskStatus.State != QuestState.NotStarted)
                {
                    MarkAsUsed();
                    await OnItemInteracted.InvokeAsync((eventData, itemParameters));
                }
            }
            else
            {
                MarkAsUsed();
                await OnItemInteracted.InvokeAsync((eventData, itemParameters));
            }

        }
    }
}
