using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace Game.MiniGame.PowerCheck
{
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(MiniGamePlayer))]
    public class EnemyMove : MonoBehaviour
    {
        [Header("Movement Params")]
        [SerializeField] private MineSpawner _mineSpawner;
        [SerializeField] private MiniGamePlayer _characteristics;

        private Queue<Vector3> _targetsQueue = new Queue<Vector3>();
        private NavMeshAgent _agent;

        // Priority weights for mine types
        private readonly float healPriorityHigh = 10f;
        private readonly float healPriorityLow = 3f;
        private readonly float damagePriority = 4f;
        private readonly float buffPriority = 6f;
        private readonly float trapAvoidancePenalty = -2f;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _characteristics = GetComponent<MiniGamePlayer>();
        }

        private void Start()
        {
            UpdateTargetPoints();
            if (_targetsQueue.Count > 0)
                SetNextDestination();
        }

        private void FixedUpdate()
        {
            if (!_agent.pathPending && _agent.remainingDistance < 0.5f)
            {
                UpdateTargetPoints();
                if (_targetsQueue.Count > 0)
                    SetNextDestination();
            }
        }

        // Build and sort targets by dynamic priority
        private void UpdateTargetPoints()
        {
            var candidates = new List<Mine>();
            candidates.AddRange(_mineSpawner.HealMines.Where(m => m.MineGameObject.activeSelf));
            candidates.AddRange(_mineSpawner.DamageMines.Where(m => m.MineGameObject.activeSelf));
            candidates.AddRange(_mineSpawner.BuffMines.Where(m => m.MineGameObject.activeSelf));
            candidates.AddRange(_mineSpawner.DebuffMines.Where(m => m.MineGameObject.activeSelf));

            Vector3 selfPos = transform.position;
            float currentHealthPct = _characteristics.MaxHealth == 0
                ? 0f
                : (float)_characteristics.Health / _characteristics.MaxHealth;

            // Compute weighted score for each mine
            var scored = candidates.Select(mine => {
                Vector3 pos = mine.MineGameObject.transform.position;
                float dist = Vector3.Distance(selfPos, pos);
                float baseScore = 0f;

                // Heal priorities: higher if low health
                if (_mineSpawner.HealMines.Contains(mine))
                {
                    baseScore = currentHealthPct < 0.5f ? healPriorityHigh : healPriorityLow;
                }
                // Damage: enemy may seek damage mines to hurt player
                else if (_mineSpawner.DamageMines.Contains(mine))
                {
                    baseScore = damagePriority;
                }
                // Buff (speed-up)
                else if (_mineSpawner.BuffMines.Contains(mine))
                {
                    baseScore = buffPriority;
                }
                // Trap (debuff mines) - avoid unless no other options
                else if (_mineSpawner.DebuffMines.Contains(mine))
                {
                    baseScore = trapAvoidancePenalty;
                }

                // Final score: weight by inverse distance
                float score = baseScore / (dist + 1f);
                return new { Position = pos, Score = score };
            });

            // Order descending by score
            var ordered = scored
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ToList();

            // If no positive scores, fallback to nearest heal or damage
            if (ordered.Count == 0)
            {
                var fallback = candidates
                    .OrderBy(m => Vector3.Distance(selfPos, m.MineGameObject.transform.position))
                    .FirstOrDefault();
                _targetsQueue.Clear();
                if (fallback != null)
                    _targetsQueue.Enqueue(fallback.MineGameObject.transform.position);
                return;
            }

            // Enqueue sorted positions, clear old
            _targetsQueue.Clear();
            foreach (var entry in ordered)
                _targetsQueue.Enqueue(entry.Position);
        }

        private void SetNextDestination()
        {
            if (_targetsQueue.Count > 0)
            {
                Vector3 nextPos = _targetsQueue.Dequeue();
                _agent.SetDestination(nextPos);
            }
        }
    }
}

