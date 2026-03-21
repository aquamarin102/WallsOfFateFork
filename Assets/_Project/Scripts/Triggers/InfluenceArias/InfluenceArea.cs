using Game.Core;
using Game.Data;
using Ink.Parsed;
using System;
using UnityEngine;
using UnityEngine.Events;
using Zenject;

namespace Game
{
    public enum InfluenceType
    {
        None = 0,
        Dialog = 1,
        Object = 2
    }

    public enum InfluenceInteractionType
    {
        Stay = 0,
        Enter = 1,
        Exit = 2,
    }

    public class TriggerEvent
    {
        public InfluenceType AreaType;
        public GameObject PlayerObj;
        public GameObject TriggerObj;
        public bool IsEnteracted;
        public string Parameters;

        public TriggerEvent(InfluenceType areaType, GameObject playerObj, GameObject triggerObj, bool isEnteracted, string parameters)
        {
            AreaType = areaType;
            PlayerObj = playerObj;
            IsEnteracted = isEnteracted;
            TriggerObj = triggerObj;
            Parameters = parameters;
        }
    }

    [RequireComponent(typeof(BoxCollider))]
    public class InfluenceArea : MonoBehaviour
    {
        public InfluenceType AreaType;
        public InfluenceInteractionType InteractionType;
        public GameObject triggerObject;
        public AsyncEvent<TriggerEvent> OnEventTriggered { get; } = new AsyncEvent<TriggerEvent>();
        [TextArea(3, 40)]
        public string Parameters;
        public ITriggerHandler Handler;

        public int questId = -1; // от какого квеста зависит 
        public int questTaskId = -1; // от какого таска зависит 
        //[SerializeField] private OutlineTrigger outlineTrigger;

        private BoxCollider boxCollider;
        private int lastProcessedInteractPressId;
        //private bool interactPressedThisFrame;

        //private void Reset()
        //{
        //    boxCollider = GetComponent<BoxCollider>();
        //    boxCollider.isTrigger = true;
        //    boxCollider.size = new Vector3(5f, 5f, 5f);
        //    boxCollider.center = Vector3.zero;
        //}

        //private void Start()
        //{
        //    Handler = FindObjectOfType<BoxGrabberHandler>();
        //}

        private QuestManager questManager;

        [Inject]
        public void Construct(QuestManager questManager)
        {
            this.questManager = questManager;
        }

        private void Start()
        {
            if (triggerObject == null)
            {
                triggerObject = gameObject;
            }

            //interactPressedThisFrame = InputManager.GetInstance().GetInteractPressed();

            //if (interactPressedThisFrame)
            //{
            //    interactPressedBuffered = true;
            //}
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {

                if (InteractionType == InfluenceInteractionType.Enter)
                {
                    _ = InvokeEventAsync(other);
                }
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (InteractionType == InfluenceInteractionType.Stay)
                {
                    _ = InvokeEventAsync(other);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                if (InteractionType == InfluenceInteractionType.Exit)
                {
                    _ = InvokeEventAsync(other);
                }
            }
        }

        protected bool ConsumeInteractPress()
        {
            var inputManager = InputManager.GetInstance();
            return inputManager != null && inputManager.TryConsumeInteractPress(ref lastProcessedInteractPressId);
        }

        public virtual async System.Threading.Tasks.Task InvokeEventAsync(Collider obj)
        {
            bool interacted = ConsumeInteractPress();
            TriggerEvent eventData = new TriggerEvent(AreaType, obj.gameObject, triggerObject, interacted, Parameters);

            if (interacted)
            {
                Debug.Log("player interacted");
            }

            bool shouldInvoke = ShouldInvokeEvent();

            if (shouldInvoke)
            {
                await OnEventTriggered.InvokeAsync(eventData);
            }
        }

        private bool ShouldInvokeEvent()
        {
            if (questId <= -1 || questTaskId <= -1)
            {
                return true;
            }

            Quest quest = questManager?.GetQuest(questId);
            if (quest == null)
            {
                Debug.LogWarning($"Quest with ID {questId} not found");
                return false;
            }

            QuestStatus questStatus = questManager.GetQuestStatus(quest.Id);
            if (questStatus == null)
            {
                Debug.LogWarning($"Quest status for quest {questId} not found");
                return false;
            }

            QuestTask task = questManager.GetQuestTask(quest.Id, questTaskId);
            if (task == null)
            {
                Debug.LogWarning($"Task {questTaskId} in quest {questId} not found");
                return false;
            }

            // Безопасное получение статуса задачи
            if (!questStatus.TasksStatusData.TryGetValue(task.Id, out TaskStatus taskStatus))
            {
                Debug.LogWarning($"Task status for task {task.Id} not found");
                return false;
            }

            // Вызываем событие только если задача не начата
            return taskStatus.State == QuestState.InProgress;
        }

        public virtual async System.Threading.Tasks.Task InvokeDirectInteractionAsync(GameObject playerObj)
        {
            GameObject targetObject = triggerObject != null ? triggerObject : gameObject;
            TriggerEvent eventData = new TriggerEvent(AreaType, playerObj, targetObject, true, Parameters);
            await OnEventTriggered.InvokeAsync(eventData);
        }
    }
}
