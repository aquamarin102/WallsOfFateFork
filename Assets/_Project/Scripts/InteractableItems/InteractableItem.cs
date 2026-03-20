using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Data;
using Zenject;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public class InteractableItem : MonoBehaviour, ITriggerable
    {
        [SerializeField] private bool _dependFromQuests = false;

        [Header("Resource Settings")]
        public ResourceType resourceType;
        public int amount = 1;

        [TextArea]
        public string message = "+1 resource";
        public string Key = "key";
        public string localizationFileName = "intaractive-items";

        [Header("Floating Text")]
        public GameObject floatingTextPrefab;
        public Vector3 spawnOffset = new Vector3(0f, 2.5f, 0f);

        [Header("Post-Use Behavior")]
        public bool destroyAfterUse = true;

        [Header("Move-to settings")]
        [Tooltip("На каком расстоянии от предмета игрок останавливается")]
        [SerializeField] private float approachDistance = 1.2f;

        private Transform playerTransform;
        private bool _hasBeenUsed = false;
        public bool HasBeenUsed => _hasBeenUsed;

        private PlayerManager playerManager;

        [Inject]
        private void Construct(PlayerManager playerManager, PlayerMoveController playerController)
        {
            this.playerManager = playerManager;
            this.playerTransform = playerController.transform;
        }

        void Awake()
        {
            CheckUsability();
        }

        private void Update()
        {
            CheckUsability();
        }

        private void CheckUsability()
        {
            CompositeTrigger compositeTrigger = this.gameObject.GetComponent<CompositeTrigger>();

            string sceneName = SceneManager.GetActiveScene().name;
            if (InteractableItemCollection.TryGetItemState(sceneName, gameObject.name, out bool hasBeenUsed))
            {
                _hasBeenUsed = hasBeenUsed;
                if (_hasBeenUsed)
                {
                    if (destroyAfterUse)
                        gameObject.SetActive(false);
                    else
                    {
                        var col = GetComponent<Collider>();
                        if (col) col.enabled = false;
                    }
                    foreach (var o in GetComponentsInChildren<cakeslice.Outline>())
                        o.enabled = false;
                }
            }
        }

        void OnMouseUpAsButton()
        {
            if (_hasBeenUsed) return;
            if (GetComponent<InteractibleItemInfluenceArea>() != null) return;

            var playerGO = GameObject.FindGameObjectWithTag("Player");
            var mover = playerGO?.GetComponent<PlayerMoveController>();
            if (mover == null) return;

            mover.MoveToAndCallback(
                this.transform,
                true,
                () => Interact(),
                approachDistance
            );
        }

        public void ResetForRespawn()
        {
            _hasBeenUsed = false;

            if (TryGetComponent<Collider>(out var col))
                col.enabled = true;

            foreach (var o in GetComponentsInChildren<cakeslice.Outline>())
                o.enabled = true;

            string scene = SceneManager.GetActiveScene().name;
            InteractableItemCollection.SetItemState(scene, gameObject.name, false);
        }

        public void Interact()
        {
            CompositeTrigger compositeTrigger = this.gameObject.GetComponent<CompositeTrigger>();
            if (_hasBeenUsed) return;
            else
            {
                if (_dependFromQuests)
                {
                    if (!compositeTrigger.IsDone) return;
                }
            }
            _hasBeenUsed = true;

            string sceneName = SceneManager.GetActiveScene().name;
            InteractableItemCollection.SetItemState(sceneName, gameObject.name, _hasBeenUsed);

            playerManager.PlayerData.AddResource(resourceType, amount);

            if (floatingTextPrefab != null && playerTransform != null)
            {
                Vector3 worldPos = playerTransform.position + spawnOffset;
                var ftGO = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
                if (ftGO.TryGetComponent<FloatingText>(out var ft))
                    ft.SetText(message);
            }

            if (destroyAfterUse)
                gameObject.SetActive(false);
            else
            {
                var col = GetComponent<Collider>();
                if (col) col.enabled = false;
            }

            InteractableItemCollection.SetItemState(SceneManager.GetActiveScene().name, this.gameObject.name, _hasBeenUsed);

            foreach (var o in GetComponentsInChildren<cakeslice.Outline>())
                o.enabled = false;
        }

        public void Triggered() => Interact();
    }
}
