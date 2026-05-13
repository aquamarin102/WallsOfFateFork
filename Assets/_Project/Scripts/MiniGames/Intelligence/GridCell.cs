using UnityEngine;

namespace Game
{
    public class GridCell : MonoBehaviour
    {
        public Vector2Int GridPosition;
        public RouteCellType CellType = RouteCellType.Normal;

        [Header("Optional Visuals")]
        [SerializeField] private GameObject collectibleVisual;
        [SerializeField] private Renderer[] tintedRenderers;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool autoTint = true;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color wallColor = new(0.2f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color argumentColor = new(1f, 0.8f, 0.15f, 1f);
        [SerializeField] private Color exitColor = new(0.25f, 0.8f, 0.35f, 1f);
        [SerializeField] private Color forbiddenColor = new(0.8f, 0.2f, 0.2f, 1f);
        [SerializeField] private Color timedBarrierBlockedColor = new(0.82f, 0.22f, 0.22f, 1f);
        [SerializeField] private Color timedBarrierPassableColor = new(0.48f, 0.12f, 0.12f, 1f);
        [SerializeField] private Color collectedArgumentColor = new(0.7f, 0.7f, 0.7f, 1f);
        [SerializeField] private Color previewArgumentColor = new(0.38f, 0.92f, 0.52f, 1f);

        [Header("Argument Rules")]
        [SerializeField, Min(0)] private int argumentSequenceOrder;

        [Header("Timed Barrier")]
        [SerializeField] private bool timedBarrierStartsPassable;

        private bool _argumentCollected;
        private bool _timedBarrierIsPassable;
        private bool _previewHighlighted;
        private MaterialPropertyBlock _propertyBlock;

        public bool HasAvailableArgument => CellType == RouteCellType.Argument && !_argumentCollected;
        public bool IsArgumentCell => CellType == RouteCellType.Argument;
        public int ArgumentSequenceOrder => Mathf.Max(0, argumentSequenceOrder);

        public void ResetState()
        {
            _argumentCollected = false;
            _timedBarrierIsPassable = CellType == RouteCellType.TimedBarrier && timedBarrierStartsPassable;
            _previewHighlighted = false;

            if (collectibleVisual != null)
            {
                collectibleVisual.SetActive(CellType == RouteCellType.Argument);
            }

            RefreshVisual();
        }

        public bool TryCollectArgument()
        {
            if (!HasAvailableArgument)
            {
                return false;
            }

            _argumentCollected = true;

            if (collectibleVisual != null)
            {
                collectibleVisual.SetActive(false);
            }

            RefreshVisual();
            return true;
        }

        public bool IsBlocked()
        {
            return CellType == RouteCellType.Wall ||
                   (CellType == RouteCellType.TimedBarrier && !_timedBarrierIsPassable);
        }

        public bool IsBlockedAtTurn(int turnIndex)
        {
            turnIndex = Mathf.Max(0, turnIndex);

            return CellType == RouteCellType.Wall ||
                   (CellType == RouteCellType.TimedBarrier && !IsTimedBarrierPassableAtTurn(turnIndex));
        }

        public bool IsForbidden()
        {
            return CellType == RouteCellType.Forbidden;
        }

        public bool IsForbiddenAtTurn(int turnIndex)
        {
            return CellType == RouteCellType.Forbidden;
        }

        public bool IsExit()
        {
            return CellType == RouteCellType.Exit;
        }

        public void AdvanceTurn()
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                return;
            }

            _timedBarrierIsPassable = !_timedBarrierIsPassable;
            RefreshVisual();
        }

        public bool IsTimedBarrierPassableAtTurn(int turnIndex)
        {
            if (CellType != RouteCellType.TimedBarrier)
            {
                return false;
            }

            turnIndex = Mathf.Max(0, turnIndex);
            if ((turnIndex & 1) == 0)
            {
                return timedBarrierStartsPassable;
            }

            return !timedBarrierStartsPassable;
        }

        public void SetRoutePreviewHighlighted(bool highlighted)
        {
            if (_previewHighlighted == highlighted)
            {
                return;
            }

            _previewHighlighted = highlighted;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (!autoTint)
            {
                return;
            }

            Color targetColor = CellType switch
            {
                RouteCellType.Wall => wallColor,
                RouteCellType.Argument => _argumentCollected ? collectedArgumentColor : argumentColor,
                RouteCellType.Exit => exitColor,
                RouteCellType.Forbidden => forbiddenColor,
                RouteCellType.TimedBarrier => _timedBarrierIsPassable ? timedBarrierPassableColor : timedBarrierBlockedColor,
                _ => normalColor
            };

            if (IsArgumentCell && !_argumentCollected && _previewHighlighted)
            {
                targetColor = Color.Lerp(targetColor, previewArgumentColor, 0.75f);
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = targetColor;
            }

            if (tintedRenderers == null || tintedRenderers.Length == 0)
            {
                tintedRenderers = GetComponentsInChildren<Renderer>(true);
            }

            for (int index = 0; index < tintedRenderers.Length; index++)
            {
                Renderer targetRenderer = tintedRenderers[index];
                if (targetRenderer == null || targetRenderer.sharedMaterial == null || !targetRenderer.sharedMaterial.HasProperty("_Color"))
                {
                    continue;
                }

                _propertyBlock ??= new MaterialPropertyBlock();
                _propertyBlock.Clear();
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_Color", targetColor);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void Awake()
        {
            ResetState();
        }

        private void OnValidate()
        {
            _timedBarrierIsPassable = CellType == RouteCellType.TimedBarrier && timedBarrierStartsPassable;
            RefreshVisual();
        }
    }
}
