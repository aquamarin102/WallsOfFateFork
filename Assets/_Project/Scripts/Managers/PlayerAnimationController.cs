using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public class PlayerAnimationController : MonoBehaviour
    {
        private PlayerAnimator playerAnimator;

        [Tooltip("Минимальная пауза между повторными нажатиями, сек.")]
        [SerializeField] private float interactCooldown = 0.4f;
        private float _nextTimeCanInteract = 0f;
        private bool _interactBuffered;
        private void Awake()
        {
            playerAnimator = GetComponent<PlayerAnimator>();
            if (playerAnimator == null)
            {
                Debug.LogError("InteractManager: Не найден компонент PlayerAnimator!");
            }

        }

        public void InteractWith(TriggerEvent eventData, bool on_ofPushing)
        {
            if (eventData.TriggerObj == null) return;                

            GameObject go = eventData.TriggerObj.gameObject;
            if (go.CompareTag("PickupFloor")) playerAnimator.PlayPickupFloor();
            else if (go.CompareTag("PickupBody")) playerAnimator.PlayPickupBody();
            else if (go.CompareTag("Chest")) playerAnimator.PlayOpenChest();
            else if (go.CompareTag("Box") && on_ofPushing) playerAnimator.StartPushing();
            else if (go.CompareTag("Box") && !on_ofPushing) playerAnimator.StopPushing();
        }
    }
}