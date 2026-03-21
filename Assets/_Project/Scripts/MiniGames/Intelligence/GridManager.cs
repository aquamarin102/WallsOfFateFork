using System.Collections.Generic;
using UnityEngine;

namespace Game
{

    public class GridManager : MonoBehaviour
    {
        public int width = 3;
        public int height = 3;
        public List<GridCell> cells = new();

        [Header("Virtual Grid")]
        [SerializeField] private Transform origin;
        [SerializeField] private RouteBoardPlane boardPlane = RouteBoardPlane.XZ;
        [SerializeField] private Vector2 cellSpacing = Vector2.one;
        [SerializeField] private Transform rightCellReference;
        [SerializeField] private Transform forwardCellReference;
        [SerializeField] private float surfaceOffset;
        [SerializeField] private bool autoCollectCellsFromChildren = true;
        [SerializeField] private bool autoCollectOccupantsFromScene = true;

        private readonly Dictionary<Vector2Int, GridCell> _cellLookup = new();
        private readonly Dictionary<Vector2Int, List<RouteGridOccupant>> _occupantLookup = new();

        public int RemainingArguments { get; private set; }
        public bool HasExitCell { get; private set; }

        public void RefreshLayout()
        {
            if ((cells == null || cells.Count == 0) && autoCollectCellsFromChildren)
            {
                cells = new List<GridCell>(GetComponentsInChildren<GridCell>(true));
            }

            _cellLookup.Clear();

            if (cells != null)
            {
                for (int index = 0; index < cells.Count; index++)
                {
                    GridCell cell = cells[index];
                    if (cell == null)
                    {
                        continue;
                    }

                    _cellLookup[cell.GridPosition] = cell;
                }
            }

            BuildOccupantLookup();
            ResetBoardState();
        }

        public void ResetBoardState()
        {
            HasExitCell = false;
            RemainingArguments = 0;

            foreach (KeyValuePair<Vector2Int, GridCell> pair in _cellLookup)
            {
                GridCell cell = pair.Value;
                if (cell == null)
                {
                    continue;
                }

                cell.ResetState();

                if (cell.HasAvailableArgument)
                {
                    RemainingArguments++;
                }

                if (cell.IsExit())
                {
                    HasExitCell = true;
                }
            }

            foreach (KeyValuePair<Vector2Int, List<RouteGridOccupant>> pair in _occupantLookup)
            {
                List<RouteGridOccupant> occupants = pair.Value;
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant == null)
                    {
                        continue;
                    }

                    occupant.ResetState();

                    if (occupant.HasAvailableArgument)
                    {
                        RemainingArguments++;
                    }

                    if (occupant.IsExit)
                    {
                        HasExitCell = true;
                    }
                }
            }
        }

        public bool IsInside(Vector2Int position)
        {
            if (_cellLookup.Count > 0)
            {
                return _cellLookup.ContainsKey(position);
            }

            return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
        }

        public bool IsBlocked(Vector2Int position)
        {
            if (_cellLookup.TryGetValue(position, out GridCell cell) && cell != null && cell.IsBlocked())
            {
                return true;
            }

            if (_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant != null && occupant.BlocksMovement)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsForbidden(Vector2Int position)
        {
            if (_cellLookup.TryGetValue(position, out GridCell cell) && cell != null && cell.IsForbidden())
            {
                return true;
            }

            if (_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant != null && occupant.IsForbidden)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsExit(Vector2Int position)
        {
            if (_cellLookup.TryGetValue(position, out GridCell cell) && cell != null && cell.IsExit())
            {
                return true;
            }

            if (_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant != null && occupant.IsExit)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public int CollectArguments(Vector2Int position)
        {
            int collected = 0;

            if (_cellLookup.TryGetValue(position, out GridCell cell) && cell != null && cell.TryCollectArgument())
            {
                collected++;
            }

            if (_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant != null && occupant.TryCollectArgument())
                    {
                        collected++;
                    }
                }
            }

            if (collected > 0)
            {
                RemainingArguments = Mathf.Max(0, RemainingArguments - collected);
            }

            return collected;
        }

        public void AdvanceTurnState()
        {
            foreach (KeyValuePair<Vector2Int, GridCell> pair in _cellLookup)
            {
                GridCell cell = pair.Value;
                if (cell == null)
                {
                    continue;
                }

                cell.AdvanceTurn();
            }

            foreach (KeyValuePair<Vector2Int, List<RouteGridOccupant>> pair in _occupantLookup)
            {
                List<RouteGridOccupant> occupants = pair.Value;
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant == null)
                    {
                        continue;
                    }

                    occupant.AdvanceTurn(this);
                }
            }
        }

        public Vector3 GetWorldPosition(Vector2Int position)
        {
            if (_cellLookup.TryGetValue(position, out GridCell cell) && cell != null)
            {
                return cell.transform.position + GetSurfaceNormal() * surfaceOffset;
            }

            Transform targetOrigin = origin != null ? origin : transform;
            Vector2 resolvedSpacing = GetResolvedCellSpacing();
            Vector3 localOffset = boardPlane == RouteBoardPlane.XY
                ? new Vector3(position.x * resolvedSpacing.x, position.y * resolvedSpacing.y, 0f)
                : new Vector3(position.x * resolvedSpacing.x, 0f, position.y * resolvedSpacing.y);

            return targetOrigin.TransformPoint(localOffset) + GetSurfaceNormal() * surfaceOffset;
        }

        public bool TryGetGridPositionFromWorld(Vector3 worldPosition, out Vector2Int position)
        {
            if (_cellLookup.Count > 0)
            {
                bool hasCandidate = false;
                float bestDistance = float.MaxValue;
                Vector2Int bestPosition = Vector2Int.zero;

                foreach (KeyValuePair<Vector2Int, GridCell> pair in _cellLookup)
                {
                    GridCell cell = pair.Value;
                    if (cell == null)
                    {
                        continue;
                    }

                    float distance = (cell.transform.position - worldPosition).sqrMagnitude;
                    if (!hasCandidate || distance < bestDistance)
                    {
                        hasCandidate = true;
                        bestDistance = distance;
                        bestPosition = pair.Key;
                    }
                }

                position = bestPosition;
                return hasCandidate;
            }

            Vector2 resolvedSpacing = GetResolvedCellSpacing();
            if (Mathf.Abs(resolvedSpacing.x) < 0.000001f || Mathf.Abs(resolvedSpacing.y) < 0.000001f)
            {
                position = Vector2Int.zero;
                return false;
            }

            Transform targetOrigin = origin != null ? origin : transform;
            Vector3 localPosition = targetOrigin.InverseTransformPoint(worldPosition);
            float rawX = localPosition.x / resolvedSpacing.x;
            float rawY = boardPlane == RouteBoardPlane.XY
                ? localPosition.y / resolvedSpacing.y
                : localPosition.z / resolvedSpacing.y;

            position = new Vector2Int(Mathf.RoundToInt(rawX), Mathf.RoundToInt(rawY));
            return IsInside(position);
        }

        public Vector3 GetSurfaceNormal()
        {
            Transform targetOrigin = origin != null ? origin : transform;
            return boardPlane == RouteBoardPlane.XY ? targetOrigin.forward : targetOrigin.up;
        }

        public Vector3 GetWorldDirection(RouteDirection direction)
        {
            Vector2Int gridOffset = RouteDirectionUtility.ToVector2Int(direction);

            foreach (KeyValuePair<Vector2Int, GridCell> pair in _cellLookup)
            {
                Vector2Int from = pair.Key;
                Vector2Int to = from + gridOffset;
                if (!IsInside(to))
                {
                    continue;
                }

                Vector3 vector = GetWorldPosition(to) - GetWorldPosition(from);
                if (vector.sqrMagnitude > 0.0001f)
                {
                    return vector.normalized;
                }
            }

            Transform targetOrigin = origin != null ? origin : transform;
            Vector2 resolvedSpacing = GetResolvedCellSpacing();
            float horizontalSign = resolvedSpacing.x < 0f ? -1f : 1f;
            float verticalSign = resolvedSpacing.y < 0f ? -1f : 1f;

            return direction switch
            {
                RouteDirection.Up => verticalSign * (boardPlane == RouteBoardPlane.XY ? targetOrigin.up : targetOrigin.forward),
                RouteDirection.Right => horizontalSign * targetOrigin.right,
                RouteDirection.Down => -verticalSign * (boardPlane == RouteBoardPlane.XY ? targetOrigin.up : targetOrigin.forward),
                RouteDirection.Left => -horizontalSign * targetOrigin.right,
                _ => targetOrigin.forward
            };
        }

        private void Awake()
        {
            RefreshLayout();
        }

        private void OnValidate()
        {
            if (!CanRefreshInValidation())
            {
                return;
            }

            RefreshLayout();
        }

        private void BuildOccupantLookup()
        {
            _occupantLookup.Clear();

            if (!autoCollectOccupantsFromScene)
            {
                return;
            }

            RouteGridOccupant[] occupants = FindObjectsOfType<RouteGridOccupant>(true);
            for (int index = 0; index < occupants.Length; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant == null)
                {
                    continue;
                }

                occupant.SyncGridPosition(this);

                if (!_occupantLookup.TryGetValue(occupant.GridPosition, out List<RouteGridOccupant> list))
                {
                    list = new List<RouteGridOccupant>();
                    _occupantLookup.Add(occupant.GridPosition, list);
                }

                list.Add(occupant);
            }
        }

        private Vector2 GetResolvedCellSpacing()
        {
            Vector2 resolvedSpacing = cellSpacing;
            Transform targetOrigin = origin != null ? origin : transform;

            if (rightCellReference != null)
            {
                Vector3 localRight = targetOrigin.InverseTransformPoint(rightCellReference.position);
                if (Mathf.Abs(localRight.x) > 0.000001f)
                {
                    resolvedSpacing.x = localRight.x;
                }
            }

            if (forwardCellReference != null)
            {
                Vector3 localForward = targetOrigin.InverseTransformPoint(forwardCellReference.position);
                float forwardComponent = boardPlane == RouteBoardPlane.XY ? localForward.y : localForward.z;
                if (Mathf.Abs(forwardComponent) > 0.000001f)
                {
                    resolvedSpacing.y = forwardComponent;
                }
            }

            return resolvedSpacing;
        }

        private bool CanRefreshInValidation()
        {
            if (Application.isPlaying)
            {
                return true;
            }

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                return false;
            }

            if (!isActiveAndEnabled)
            {
                return false;
            }

#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer ||
                UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating)
            {
                return false;
            }
#endif

            return true;
        }
    }
}
