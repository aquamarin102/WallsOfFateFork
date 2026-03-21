using Game.Core;
using System;
using UnityEngine;
using System.Threading.Tasks;

namespace Game
{
    public class DoorInfluenceArea : InfluenceArea
    {
        [Header("Door Settings")]
        [SerializeField] private DoorParameters doorParameters;

        public AsyncEvent<(TriggerEvent, DoorParameters)> OnDoorInteracted { get; } = new();

        public override async Task InvokeEventAsync(Collider obj)
        {
            if (!TryGetPlayerObject(obj, out GameObject playerObject))
            {
                return;
            }

            bool interacted = ConsumeInteractPress();

            string json = JsonUtility.ToJson(doorParameters);

            TriggerEvent eventData = new TriggerEvent(
                AreaType,
                playerObject,
                triggerObject ?? gameObject,
                interacted,
                json
            );

            await base.InvokeEventAsync(obj);

            await OnDoorInteracted.InvokeAsync((eventData, doorParameters));
        }

        public override async Task InvokeDirectInteractionAsync(GameObject playerObj)
        {
            GameObject targetObject = triggerObject != null ? triggerObject : gameObject;
            string json = JsonUtility.ToJson(doorParameters);

            TriggerEvent eventData = new TriggerEvent(
                AreaType,
                playerObj,
                targetObject,
                true,
                json
            );

            await OnEventTriggered.InvokeAsync(eventData);
            await OnDoorInteracted.InvokeAsync((eventData, doorParameters));
        }
    }
}
