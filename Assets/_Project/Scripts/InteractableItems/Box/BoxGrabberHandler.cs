using Game.Core; // Добавьте этот using для AsyncEvent
using Game.Data;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using System.Threading.Tasks;

namespace Game
{
    internal class BoxGrabberHandler : MonoBehaviour
    {
        [SerializeField] private List<InfluenceArea> influenceArias;

        private BoxMover currentBoxMover;
        private GameObject currentBox;

        private void OnEnable()
        {
            foreach (var area in influenceArias)
                area.OnEventTriggered.Subscribe(HandleAsync); 
        }

        private void OnDisable()
        {
            foreach (var area in influenceArias)
                area.OnEventTriggered.Unsubscribe(HandleAsync); 
        }

        public async Task HandleAsync(TriggerEvent eventData)
        {
            if (eventData.AreaType != InfluenceType.Object || !eventData.IsEnteracted)
                return;

            GameObject box = eventData.TriggerObj;

            if (!box.CompareTag("Box"))
                return;

            ToggleGrab(eventData);

            await Task.CompletedTask; 
        }

        private void ToggleGrab(TriggerEvent eventData)
        {
            GameObject box = eventData.TriggerObj;
            PlayerAnimationController playerAnimator = eventData.PlayerObj.GetComponent<PlayerAnimationController>();
            if (currentBox == box)
            {
                DetachBox();
                playerAnimator?.InteractWith(eventData, false);
            }
            else
            {
                if (currentBox != null)
                    DetachBox();

                playerAnimator?.InteractWith(eventData, true);
                AttachBox(box);
            }
        }

        private void AttachBox(GameObject box)
        {
            currentBox = box;
            currentBoxMover = box.GetComponent<BoxMover>();

            if (currentBoxMover == null)
                currentBoxMover = box.AddComponent<BoxMover>();

            currentBoxMover.StartHolding();
            Debug.Log($"Box grabbed: {box.name}");
        }

        private void DetachBox()
        {
            if (currentBoxMover != null)
                currentBoxMover.StopHolding();

            Debug.Log($"Box released: {currentBox.name}");
            currentBox = null;
            currentBoxMover = null;
        }
    }
}