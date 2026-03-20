using Game;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game
{
    [RequireComponent(typeof(BoxCollider))]
    internal class StartDayDialogueTriggerZone : MonoBehaviour
    {
        private int lastProcessedInteractPressId;

        public InfluenceType AreaType;
        public InfluenceInteractionType InteractionType;
        public GameObject triggerObject;
        public event Action<TriggerEvent> OnEventTriggered;
        [TextArea]
        public string Parameters;

        private BoxCollider boxCollider;

        private void Reset()
        {
            boxCollider = GetComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(5f, 5f, 5f);
            boxCollider.center = Vector3.zero;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (InteractionType == InfluenceInteractionType.Enter)
            {
                InvokeEvent(other);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (InteractionType == InfluenceInteractionType.Stay)
            {
                InvokeEvent(other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (InteractionType == InfluenceInteractionType.Exit)
            {
                InvokeEvent(other);
            }
        }

        private void InvokeEvent(Collider obj)
        {
            bool interacted = InputManager.GetInstance().TryConsumeInteractPress(ref lastProcessedInteractPressId);
            GameObject targetObject = triggerObject != null ? triggerObject : gameObject;
            TriggerEvent eventData = new TriggerEvent(AreaType, obj.gameObject, targetObject, interacted, Parameters);
            OnEventTriggered?.Invoke(eventData);
        }

        public void InvokeDirectInteraction(GameObject playerObj)
        {
            GameObject targetObject = triggerObject != null ? triggerObject : gameObject;
            TriggerEvent eventData = new TriggerEvent(AreaType, playerObj, targetObject, true, Parameters);
            OnEventTriggered?.Invoke(eventData);
        }
    }
}
