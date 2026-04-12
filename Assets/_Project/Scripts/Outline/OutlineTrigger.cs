using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game
{
    [RequireComponent(typeof(Collider))]
    public class OutlineTrigger : MonoBehaviour
    {
        public enum OutlineMethod
        {
            MaterialSwap,
            RendererFeature,
            ChildOutlines
        }

        [Header("Outline Settings")]
        [SerializeField] private OutlineMethod outlineMethod = OutlineMethod.MaterialSwap;
        [SerializeField] private Color outlineColor = Color.yellow;
        [SerializeField][Range(0, 10)] private float outlineWidth = 2f;

        [Header("Hover Settings")]
        [SerializeField] private float hoverCheckDistance = 100f;
        [SerializeField] private LayerMask hoverLayerMask = ~0;

        [Header("References")]
        [SerializeField] private GameObject targetObject;
        [SerializeField] private bool highlightOnHover = true;
        [SerializeField] private bool highlightOnTrigger = true;
        [SerializeField] private bool highlightOnGlobalHold = true;
        [SerializeField] private float fallbackInteractionRadius = 1.6f;

        private URPOutline[] outlines;
        private Collider[] colliders;
        private InteractableItem interactable;
        private InteractableItemInfluenceArea interactableItemArea;
        private PlayerMoveController playerController;
        private bool isPlayerInTrigger;
        private bool isPlayerInInteractionRadius;
        private bool isMouseOver;
        private bool isGlobalHighlightHeld;
        private bool canHighlight = true;
        private float nextPlayerLookupTime;

        private void Start()
        {
            if (targetObject == null)
                targetObject = gameObject;

            InitializeOutlines();
            InitializeColliders();

            interactable = FindRelatedComponent<InteractableItem>();
            interactableItemArea = FindRelatedComponent<InteractableItemInfluenceArea>();
            canHighlight = CanHighlight();

            SetHighlighted(false);
        }

        private void InitializeOutlines()
        {
            switch (outlineMethod)
            {
                case OutlineMethod.MaterialSwap:
                case OutlineMethod.RendererFeature:
                    outlines = targetObject.GetComponentsInChildren<URPOutline>(true);
                    if (outlines.Length == 0)
                    {
                        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer renderer in renderers)
                        {
                            URPOutline outline = renderer.gameObject.AddComponent<URPOutline>();
                            outline.OutlineColor = outlineColor;
                            outline.OutlineWidth = outlineWidth;
                        }

                        outlines = targetObject.GetComponentsInChildren<URPOutline>(true);
                    }
                    break;

                case OutlineMethod.ChildOutlines:
                    outlines = targetObject.GetComponentsInChildren<URPOutline>(true);
                    break;
            }
        }

        private void InitializeColliders()
        {
            colliders = GetRelatedComponents<Collider>();

            if (colliders.Length == 0)
                Debug.LogError($"OutlineTrigger on {gameObject.name} has no colliders!");
        }

        private void Update()
        {
            bool changed = false;

            if (highlightOnHover)
                changed |= UpdateHoverState();

            if (highlightOnTrigger)
                changed |= UpdateInteractionRadiusState();

            bool globalHighlightHeld = highlightOnGlobalHold && IsGlobalHighlightHeld();
            if (globalHighlightHeld != isGlobalHighlightHeld)
            {
                isGlobalHighlightHeld = globalHighlightHeld;
                changed = true;
            }

            bool currentCanHighlight = CanHighlight();
            if (currentCanHighlight != canHighlight)
            {
                canHighlight = currentCanHighlight;
                changed = true;
            }

            if (changed)
                UpdateHighlightState();
        }

        private bool UpdateHoverState()
        {
            if (Camera.main == null || !TryReadPointerPosition(out Vector2 pointerPosition))
                return SetMouseOver(false);

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return SetMouseOver(false);

            Ray ray = Camera.main.ScreenPointToRay(pointerPosition);
            bool hitThis = false;
            RaycastHit[] hits = Physics.RaycastAll(ray, hoverCheckDistance, hoverLayerMask, QueryTriggerInteraction.Collide);

            foreach (RaycastHit hit in hits)
            {
                if (BelongsToThisTrigger(hit.collider))
                {
                    hitThis = true;
                    break;
                }
            }

            return SetMouseOver(hitThis);
        }

        private bool UpdateInteractionRadiusState()
        {
            bool playerInInteractionRadius = IsPlayerInsideInteractionRadius();
            if (playerInInteractionRadius == isPlayerInInteractionRadius)
                return false;

            isPlayerInInteractionRadius = playerInInteractionRadius;
            return true;
        }

        private bool SetMouseOver(bool value)
        {
            if (value == isMouseOver)
                return false;

            isMouseOver = value;
            return true;
        }

        private bool IsPlayerInsideInteractionRadius()
        {
            if (!TryGetPlayerController(out PlayerMoveController controller))
                return false;

            Vector3 playerPosition = controller.transform.position;
            float interactionRadius = Mathf.Max(0.01f, Mathf.Max(controller.InteractionRadius, fallbackInteractionRadius));
            float interactionRadiusSqr = interactionRadius * interactionRadius;

            if (colliders != null)
            {
                foreach (Collider candidateCollider in colliders)
                {
                    if (candidateCollider == null ||
                        !candidateCollider.enabled ||
                        !candidateCollider.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Vector3 closestPoint = candidateCollider.ClosestPoint(playerPosition);
                    closestPoint.y = playerPosition.y;
                    if ((closestPoint - playerPosition).sqrMagnitude <= interactionRadiusSqr)
                        return true;
                }
            }

            Transform targetTransform = targetObject != null ? targetObject.transform : transform;
            Vector3 delta = targetTransform.position - playerPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= interactionRadiusSqr;
        }

        private bool TryGetPlayerController(out PlayerMoveController controller)
        {
            if (playerController != null && playerController.gameObject.activeInHierarchy)
            {
                controller = playerController;
                return true;
            }

            if (Time.unscaledTime < nextPlayerLookupTime)
            {
                controller = null;
                return false;
            }

            nextPlayerLookupTime = Time.unscaledTime + 0.5f;
            playerController = UnityEngine.Object.FindAnyObjectByType<PlayerMoveController>();
            controller = playerController;
            return controller != null;
        }

        private static bool IsGlobalHighlightHeld()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.isPressed)
                return true;

            Mouse mouse = Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                position = pointer.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        private bool BelongsToThisTrigger(Collider hitCollider)
        {
            if (hitCollider == null || colliders == null)
                return false;

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                if (col == hitCollider)
                    return true;
            }

            return false;
        }

        private void UpdateHighlightState()
        {
            canHighlight = CanHighlight();
            bool shouldHighlight = canHighlight &&
                ((highlightOnTrigger && (isPlayerInTrigger || isPlayerInInteractionRadius)) ||
                 (highlightOnHover && isMouseOver) ||
                 isGlobalHighlightHeld);

            SetHighlighted(shouldHighlight);
        }

        private bool CanHighlight()
        {
            if (interactable != null && interactable.HasBeenUsed)
                return false;

            if (interactableItemArea != null && interactableItemArea.HasBeenUsed)
                return false;

            return targetObject == null || targetObject.activeInHierarchy;
        }

        private void SetHighlighted(bool enabled)
        {
            if (outlines != null)
            {
                foreach (URPOutline outline in outlines)
                {
                    if (outline != null)
                    {
                        outline.enabled = enabled;
                        outline.SetHighlighted(enabled);
                    }
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (PlayerObjectUtility.TryGetPlayerObject(other, out _))
            {
                isPlayerInTrigger = true;
                UpdateHighlightState();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (PlayerObjectUtility.TryGetPlayerObject(other, out _))
            {
                isPlayerInTrigger = false;
                UpdateHighlightState();
            }
        }

        public void ForceHighlight(bool enabled)
        {
            SetHighlighted(enabled);
        }

        public void SetOutlineColor(Color color)
        {
            outlineColor = color;
            foreach (URPOutline outline in outlines)
            {
                if (outline != null)
                    outline.OutlineColor = color;
            }
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = width;
            foreach (URPOutline outline in outlines)
            {
                if (outline != null)
                    outline.OutlineWidth = width;
            }
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
        }

        private T FindRelatedComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            if (component != null)
                return component;

            component = GetComponentInParent<T>();
            if (component != null)
                return component;

            component = GetComponentInChildren<T>(true);
            if (component != null)
                return component;

            if (targetObject != null && targetObject != gameObject)
            {
                component = targetObject.GetComponent<T>();
                if (component != null)
                    return component;

                component = targetObject.GetComponentInParent<T>();
                if (component != null)
                    return component;

                component = targetObject.GetComponentInChildren<T>(true);
            }

            return component;
        }

        private T[] GetRelatedComponents<T>() where T : Component
        {
            List<T> components = new();
            AddComponents(GetComponentsInChildren<T>(true), components);

            if (targetObject != null && targetObject != gameObject)
                AddComponents(targetObject.GetComponentsInChildren<T>(true), components);

            return components.ToArray();
        }

        private static void AddComponents<T>(T[] source, List<T> destination) where T : Component
        {
            foreach (T component in source)
            {
                if (component != null && !destination.Contains(component))
                    destination.Add(component);
            }
        }
    }
}
