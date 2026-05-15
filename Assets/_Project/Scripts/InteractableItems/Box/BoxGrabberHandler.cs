using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Game
{
    internal class BoxGrabberHandler : MonoBehaviour
    {
        [SerializeField] private List<InfluenceArea> influenceArias;

        private void OnEnable()
        {
            foreach (InfluenceArea area in influenceArias)
                area.OnEventTriggered.Subscribe(HandleAsync);
        }

        private void OnDisable()
        {
            foreach (InfluenceArea area in influenceArias)
                area.OnEventTriggered.Unsubscribe(HandleAsync);
        }

        public async Task HandleAsync(TriggerEvent eventData)
        {
            if (eventData.AreaType != InfluenceType.Object || !eventData.IsEnteracted)
                return;

            GameObject boxObject = eventData.TriggerObj;
            if (boxObject == null || !boxObject.CompareTag("Box"))
                return;

            PlayerMoveController playerMoveController = eventData.PlayerObj != null
                ? eventData.PlayerObj.GetComponent<PlayerMoveController>()
                : null;
            BoxMover boxMover = boxObject.GetComponent<BoxMover>();

            if (playerMoveController != null && boxMover != null)
                playerMoveController.HandleBoxInteraction(boxMover);

            await Task.CompletedTask;
        }
    }
}
