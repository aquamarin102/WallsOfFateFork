using UnityEngine;

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

        private URPOutline[] outlines;
        private Collider[] colliders;
        private InteractableItem interactable;
        private bool isPlayerInTrigger;
        private bool isMouseOver;

        private void Start()
        {
            if (targetObject == null)
                targetObject = gameObject;

            InitializeOutlines();
            InitializeColliders();

            interactable = targetObject.GetComponent<InteractableItem>();

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
                        var renderers = targetObject.GetComponentsInChildren<Renderer>(true);
                        foreach (var renderer in renderers)
                        {
                            var outline = renderer.gameObject.AddComponent<URPOutline>();
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
            colliders = GetComponentsInChildren<Collider>(true);

            if (colliders.Length == 0)
                Debug.LogError($"OutlineTrigger on {gameObject.name} has no colliders!");
        }

        private void Update()
        {
            if (!highlightOnHover || Camera.main == null)
                return;

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool hitThis = false;
            RaycastHit[] hits = Physics.RaycastAll(ray, hoverCheckDistance, hoverLayerMask, QueryTriggerInteraction.Collide);

            foreach (var hit in hits)
            {
                if (BelongsToThisTrigger(hit.collider))
                {
                    hitThis = true;
                    break;
                }
            }

            if (hitThis != isMouseOver)
            {
                isMouseOver = hitThis;
                UpdateHighlightState();
            }
        }

        private bool BelongsToThisTrigger(Collider hitCollider)
        {
            if (hitCollider == null || colliders == null)
                return false;

            foreach (var col in colliders)
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
            bool canHighlight = interactable == null || !interactable.HasBeenUsed;

            bool shouldHighlight = false;

            if (highlightOnTrigger && highlightOnHover)
                shouldHighlight = canHighlight && (isPlayerInTrigger || isMouseOver);
            else if (highlightOnTrigger)
                shouldHighlight = canHighlight && isPlayerInTrigger;
            else if (highlightOnHover)
                shouldHighlight = canHighlight && isMouseOver;

            SetHighlighted(shouldHighlight);
        }

        private void SetHighlighted(bool enabled)
        {
            if (outlines != null)
            {
                foreach (var outline in outlines)
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
            foreach (var outline in outlines)
            {
                if (outline != null)
                    outline.OutlineColor = color;
            }
        }

        public void SetOutlineWidth(float width)
        {
            outlineWidth = width;
            foreach (var outline in outlines)
            {
                if (outline != null)
                    outline.OutlineWidth = width;
            }
        }

        private void OnDestroy()
        {
            SetHighlighted(false);
        }
    }
}
