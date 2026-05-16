using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class GridManager : MonoBehaviour
    {
        public int width = 3;
        public int height = 3;

        [Header("Virtual Grid")]
        [SerializeField] private Transform origin;
        [SerializeField] private RouteBoardPlane boardPlane = RouteBoardPlane.XZ;
        [SerializeField] private Vector2 cellSpacing = Vector2.one;
        [SerializeField] private Transform rightCellReference;
        [SerializeField] private Transform forwardCellReference;
        [SerializeField] private float surfaceOffset;
        [SerializeField] private bool autoCollectOccupantsFromScene = true;

        private readonly Dictionary<Vector2Int, List<RouteGridOccupant>> _occupantLookup = new();
        public int RemainingArguments { get; private set; }
        public int TotalArguments { get; private set; }
        public bool HasExitCell { get; private set; }
        public bool HasSequencedArguments { get; private set; }

        public void RefreshLayout()
        {
            BuildOccupantLookup();
            ResetBoardState();
        }

        public void ResetBoardState()
        {
            HasExitCell = false;
            RemainingArguments = 0;
            TotalArguments = 0;
            HasSequencedArguments = false;

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

                    if (occupant.IsArgumentOccupant)
                    {
                        TotalArguments++;
                        HasSequencedArguments |= occupant.ArgumentSequenceOrder > 0;
                    }

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
            return position.x >= 0 && position.x < width && position.y >= 0 && position.y < height;
        }

        public bool IsBlocked(Vector2Int position)
        {
            if (!_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                return false;
            }

            for (int index = 0; index < occupants.Count; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant != null && occupant.BlocksMovement)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsBlockedAtTurn(Vector2Int position, int turnIndex)
        {
            if (!_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                return false;
            }

            for (int index = 0; index < occupants.Count; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant != null && occupant.IsBlockedAtTurn(turnIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsForbidden(Vector2Int position)
        {
            if (!_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                return false;
            }

            for (int index = 0; index < occupants.Count; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant != null && occupant.IsForbidden)
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsForbiddenAtTurn(Vector2Int position, int turnIndex)
        {
            if (!_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                return false;
            }

            for (int index = 0; index < occupants.Count; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant != null && occupant.IsForbiddenAtTurn(turnIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsExit(Vector2Int position)
        {
            if (!_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                return false;
            }

            for (int index = 0; index < occupants.Count; index++)
            {
                RouteGridOccupant occupant = occupants[index];
                if (occupant != null && occupant.IsExit)
                {
                    return true;
                }
            }

            return false;
        }

        public int CollectArguments(Vector2Int position)
        {
            int collected = 0;

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

        public int CollectArguments(Vector2Int position, bool useOrderedArguments, ref int nextRequiredSequence)
        {
            if (!useOrderedArguments)
            {
                return CollectArguments(position);
            }

            int collected = 0;

            if (_occupantLookup.TryGetValue(position, out List<RouteGridOccupant> occupants))
            {
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant != null &&
                        occupant.HasAvailableArgument &&
                        occupant.ArgumentSequenceOrder <= 0 &&
                        occupant.TryCollectArgument())
                    {
                        collected++;
                    }
                }
            }

            bool collectedOrderedArgument;
            do
            {
                collectedOrderedArgument = false;

                if (!_occupantLookup.TryGetValue(position, out occupants))
                {
                    continue;
                }

                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant == null ||
                        !occupant.HasAvailableArgument ||
                        occupant.ArgumentSequenceOrder != nextRequiredSequence ||
                        !occupant.TryCollectArgument())
                    {
                        continue;
                    }

                    nextRequiredSequence++;
                    collected++;
                    collectedOrderedArgument = true;
                    break;
                }
            } while (collectedOrderedArgument);

            if (collected > 0)
            {
                RemainingArguments = Mathf.Max(0, RemainingArguments - collected);
            }

            return collected;
        }

        public void AdvanceTurnState()
        {
            AdvanceTurnState(null);
        }

        public void AdvanceTurnState(Vector2Int? protectedPosition)
        {
            foreach (KeyValuePair<Vector2Int, List<RouteGridOccupant>> pair in _occupantLookup)
            {
                List<RouteGridOccupant> occupants = pair.Value;
                for (int index = 0; index < occupants.Count; index++)
                {
                    RouteGridOccupant occupant = occupants[index];
                    if (occupant == null || (protectedPosition.HasValue && pair.Key == protectedPosition.Value))
                    {
                        continue;
                    }

                    occupant.AdvanceTurn(this);
                }
            }
        }

        public Vector3 GetWorldPosition(Vector2Int position)
        {
            Transform targetOrigin = origin != null ? origin : transform;
            Vector2 resolvedSpacing = GetResolvedCellSpacing();
            Vector3 localOffset = boardPlane == RouteBoardPlane.XY
                ? new Vector3(position.x * resolvedSpacing.x, position.y * resolvedSpacing.y, 0f)
                : new Vector3(position.x * resolvedSpacing.x, 0f, position.y * resolvedSpacing.y);

            return targetOrigin.TransformPoint(localOffset) + GetSurfaceNormal() * surfaceOffset;
        }

        public bool TryGetGridPositionFromWorld(Vector3 worldPosition, out Vector2Int position)
        {
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

        public bool TryGetGridPositionFromRay(Ray ray, out Vector2Int position)
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, ~0, QueryTriggerInteraction.Collide);
            if (hits.Length > 0)
            {
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

                for (int index = 0; index < hits.Length; index++)
                {
                    RaycastHit hit = hits[index];
                    RouteGridOccupant occupant = hit.collider.GetComponentInParent<RouteGridOccupant>();
                    if (occupant != null)
                    {
                        position = occupant.GridPosition;
                        return true;
                    }
                }
            }

            Vector3 planePoint = GetBoardPlanePoint();
            Plane boardPlaneWorld = new(GetSurfaceNormal(), planePoint);
            if (!boardPlaneWorld.Raycast(ray, out float enter))
            {
                position = Vector2Int.zero;
                return false;
            }

            Vector3 worldPoint = ray.GetPoint(enter);
            if (!TryGetGridPositionFromWorld(worldPoint, out position))
            {
                return false;
            }

            float maxSnapDistance = Mathf.Max(Mathf.Abs(GetResolvedCellSpacing().x), Mathf.Abs(GetResolvedCellSpacing().y)) * 0.8f;
            if (maxSnapDistance > 0.0001f)
            {
                Vector3 cellCenter = GetWorldPosition(position);
                if ((cellCenter - worldPoint).sqrMagnitude > maxSnapDistance * maxSnapDistance)
                {
                    position = Vector2Int.zero;
                    return false;
                }
            }

            return true;
        }

        public bool TryGetGridPositionFromScreenPoint(Camera camera, Vector2 screenPoint, out Vector2Int position)
        {
            position = Vector2Int.zero;

            if (camera == null || !camera.isActiveAndEnabled)
            {
                return false;
            }

            bool hasCandidate = false;
            float bestDistance = float.MaxValue;
            Vector2Int bestPosition = Vector2Int.zero;

            foreach (Vector2Int gridPosition in EnumerateSelectablePositions())
            {
                Vector3 worldPoint = GetPickWorldPosition(gridPosition);
                Vector3 screenCellPoint = camera.WorldToScreenPoint(worldPoint);
                if (screenCellPoint.z <= 0f)
                {
                    continue;
                }

                float screenDistance = ((Vector2)screenCellPoint - screenPoint).sqrMagnitude;
                if (!hasCandidate || screenDistance < bestDistance)
                {
                    hasCandidate = true;
                    bestDistance = screenDistance;
                    bestPosition = gridPosition;
                }
            }

            if (!hasCandidate)
            {
                return false;
            }

            float maxPickRadius = GetScreenPickRadius(camera, bestPosition);
            if (bestDistance > maxPickRadius * maxPickRadius)
            {
                return false;
            }

            position = bestPosition;
            return true;
        }

        public Vector3 GetSurfaceNormal()
        {
            Transform targetOrigin = origin != null ? origin : transform;
            return boardPlane == RouteBoardPlane.XY ? targetOrigin.forward : targetOrigin.up;
        }

        public Vector3 GetWorldDirection(RouteDirection direction)
        {
            Vector2Int gridOffset = RouteDirectionUtility.ToVector2Int(direction);

            foreach (Vector2Int from in EnumerateSelectablePositions())
            {
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

        public void ApplyRoutePreviewHighlights(ISet<Vector2Int> highlightedPositions)
        {
            foreach (KeyValuePair<Vector2Int, List<RouteGridOccupant>> pair in _occupantLookup)
            {
                bool highlighted = highlightedPositions != null && highlightedPositions.Contains(pair.Key);
                List<RouteGridOccupant> occupants = pair.Value;

                for (int index = 0; index < occupants.Count; index++)
                {
                    occupants[index]?.SetRoutePreviewHighlighted(highlighted);
                }
            }
        }

        private void BuildOccupantLookup()
        {
            _occupantLookup.Clear();

            if (!autoCollectOccupantsFromScene)
            {
                return;
            }

            RouteGridOccupant[] occupants = FindObjectsByType<RouteGridOccupant>(FindObjectsInactive.Include);
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

        private Vector3 GetBoardPlanePoint()
        {
            Transform targetOrigin = origin != null ? origin : transform;
            return targetOrigin.position + GetSurfaceNormal() * surfaceOffset;
        }

        private float GetScreenPickRadius(Camera camera, Vector2Int anchorPosition)
        {
            Vector3 anchorScreenPoint = camera.WorldToScreenPoint(GetPickWorldPosition(anchorPosition));
            float nearestNeighborDistance = float.MaxValue;

            foreach (Vector2Int gridPosition in EnumerateSelectablePositions())
            {
                if ((Mathf.Abs(gridPosition.x - anchorPosition.x) + Mathf.Abs(gridPosition.y - anchorPosition.y)) != 1)
                {
                    continue;
                }

                Vector3 neighborScreenPoint = camera.WorldToScreenPoint(GetPickWorldPosition(gridPosition));
                if (neighborScreenPoint.z <= 0f)
                {
                    continue;
                }

                float distance = Vector2.Distance(anchorScreenPoint, neighborScreenPoint);
                if (distance > 0.001f && distance < nearestNeighborDistance)
                {
                    nearestNeighborDistance = distance;
                }
            }

            if (nearestNeighborDistance < float.MaxValue)
            {
                return Mathf.Max(24f, nearestNeighborDistance * 0.45f);
            }

            return 72f;
        }

        private IEnumerable<Vector2Int> EnumerateSelectablePositions()
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private Vector3 GetPickWorldPosition(Vector2Int position)
        {
            return GetWorldPosition(position);
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
