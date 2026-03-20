using Game.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem.Interactions;
using Zenject;

namespace Game
{
    public class InteractiveItemHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _floatingTextPrefab;
        [SerializeField] private List<InteractibleItemInfluenceArea> influenceArias;

        public Action<InteractableItemParameters> OnItemHandled;

        private PlayerManager playerManager;
        private QuestManager questManager;

        [Inject]
        private void Construct(PlayerManager playerManager, QuestManager questManager)
        {
            this.playerManager = playerManager;
            this.questManager = questManager;
        }

        private void Start()
        {
            foreach (var area in influenceArias)
            {
                area.OnItemInteracted.Subscribe(HandleAsync);
            }
        }


        private void OnDestroy()
        {
            foreach (var area in influenceArias)
            {
                area.OnItemInteracted.Unsubscribe(HandleAsync);
            }
        }

        public async Task HandleAsync((TriggerEvent eventData, InteractableItemParameters itemParameters) data)
        {

            var (eventData, itemParameters) = data;

            if (!eventData.IsEnteracted) return;

            PlayChestAnimation chestAnimaton = eventData.TriggerObj.GetComponent<PlayChestAnimation>();
            chestAnimaton?.Triggered(eventData);

            PlayerAnimatinController playerAnimator = eventData.PlayerObj.GetComponent<PlayerAnimatinController>();
            playerAnimator?.InteractWith(eventData, false);

            ShowFloatingText(eventData.PlayerObj, itemParameters);
            HandlePostUseBehavior(eventData.TriggerObj, itemParameters);
            UpdateResources(eventData.PlayerObj, itemParameters);

            OnItemHandled?.Invoke(itemParameters);
        }

        private void UpdateResources(GameObject player, InteractableItemParameters parameters)
        {
            playerManager.PlayerData.AddResource(parameters.ResourceType, parameters.Amount);
        }


        private void ShowFloatingText(GameObject playerObj, InteractableItemParameters parameters)
        {
            if (_floatingTextPrefab == null || playerObj == null) return;

            Vector3 worldPos = playerObj.transform.position + parameters.SpawnOffset;
            var ftGO = Instantiate(_floatingTextPrefab, worldPos, Quaternion.identity);

            if (ftGO.TryGetComponent<FloatingText>(out var ft))
            {
                ft.SetText(parameters.Message);
            }
        }

        private void HandlePostUseBehavior(GameObject itemObj, InteractableItemParameters parameters)
        {
            if (parameters.DestroyAfterUse)
            {
                itemObj.SetActive(false);
            }
            //else
            //{
            //    var collider = itemObj.GetComponent<Collider>();
            //    if (collider != null) collider.enabled = false;
            //}        
        }
    }
}
