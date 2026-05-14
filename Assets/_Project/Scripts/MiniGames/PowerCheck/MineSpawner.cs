using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Zenject;

namespace Game.MiniGame.PowerCheck
{
    public class MineSpawner : MonoBehaviour, Unity.VisualScripting.IInitializable
    {
        // =====================================================================
        // Spawn area settings
        // =====================================================================
        [Header("Spawn Area Settings")]
        [SerializeField] private Transform CenterPoint;
        public Vector2 spawnAreaSize = new Vector2(10, 10);

        [SerializeField] private Transform parentTransform;
        [SerializeField, Tooltip("Минимальная дистанция до запрещённых точек (игрок, враг и т.д.) при спавне")]
        private float allowedDistanseForForrbidenSpawnPoint = 2f;
        [SerializeField, Tooltip("Y‑координата, на которой появляются мины")]
        private float yPositionOfSpawnMine = 0f;

        [Header("Proximity Rules")]
        [SerializeField, Tooltip("Минимальная дистанция между любыми двумя минами при спавне")]
        private float minDistanceBetweenMines = 1.25f;

        // =====================================================================
        // Spawn visual
        // =====================================================================
        [Header("Spawn Animation")]
        [SerializeField, Tooltip("Длительность анимации появления (сек.)")]
        private float spawnAnimationDuration = 0.35f;

        // =========================================
        // Prefab scale cache
        // =========================================
        private Dictionary<Mine, Vector3> _originalScales = new Dictionary<Mine, Vector3>();

        // =====================================================================
        // Mine prefabs
        // =====================================================================
        [Header("Mine Prefabs")]
        [SerializeField] private GameObject HealMinePrefab;
        [SerializeField] private GameObject DamageMinePrefab;
        [SerializeField] private GameObject BuffMinePrefab;
        [SerializeField] private GameObject DebuffMinePrefab;

        // =====================================================================
        // Per‑type spawn rules
        // =====================================================================
        [System.Serializable]
        private class MineSpawnConfig
        {
            public int initialCount;
            public int maxCount;
            public Vector2 spawnIntervalRange;
            public float RandomInterval => Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        [Header("Heal mines rules")]
        [SerializeField] private MineSpawnConfig healConfig = new MineSpawnConfig { initialCount = 3, maxCount = 6, spawnIntervalRange = new Vector2(8f, 12f) };
        [Header("Damage mines rules")]
        [SerializeField] private MineSpawnConfig damageConfig = new MineSpawnConfig { initialCount = 4, maxCount = 8, spawnIntervalRange = new Vector2(5f, 10f) };
        [Header("Speed‑buff mines rules")]
        [SerializeField] private MineSpawnConfig buffConfig = new MineSpawnConfig { initialCount = 2, maxCount = 4, spawnIntervalRange = new Vector2(12f, 18f) };
        [Header("Trap (debuff) mines rules")]
        [SerializeField] private MineSpawnConfig debuffConfig = new MineSpawnConfig { initialCount = 10, maxCount = 30, spawnIntervalRange = new Vector2(1f, 4f) };

        [Header("Low Population Recovery")]
        [SerializeField, Tooltip("Если активных фишек на поле меньше этого числа, спавнер начнёт добирать их быстрее.")]
        private int minimumTotalActiveMines = 12;
        [SerializeField, Tooltip("Сколько фишек максимум добавить за одну проверку восстановления.")]
        private int recoverySpawnBatchSize = 3;
        [SerializeField, Tooltip("Как часто проверять, не стало ли фишек слишком мало.")]
        private float recoveryCheckInterval = 0.75f;

        // =====================================================================
        // Specific parameters
        // =====================================================================
        [SerializeField] private float healCooldown;
        [SerializeField] private float damageCooldown;
        [SerializeField] private float speedBufCooldown;
        [SerializeField] private float speedBuf;
        [SerializeField] private float buffTime;
        [SerializeField] private int buffTimeBeforeExplosion;
        [SerializeField] private float buffRadiusOfExplosion;
        [SerializeField] private uint buffDamage;
        [SerializeField] private float speedDebufCooldown;
        [SerializeField] private float speedDebuf;
        [SerializeField] private float debuffTime;
        [SerializeField] private int debuffTimeBeforeExplosion;
        [SerializeField] private float debuffRadiusOfExplosion;
        [SerializeField] private uint debuffDamage;

        // =====================================================================
        // Mine lists
        // =====================================================================
        private MineList healMineList;
        private MineList damageMineList;
        private MineList buffMineList;
        private MineList debuffMineList;

        private List<Transform> _forbiddenSpawnPoints = new List<Transform>();

        private bool _isInitialized = false;

        public IReadOnlyList<Mine> HealMines => healMineList.Minelist;
        public IReadOnlyList<Mine> DamageMines => damageMineList.Minelist;
        public IReadOnlyList<Mine> BuffMines => buffMineList.Minelist;
        public IReadOnlyList<Mine> DebuffMines => debuffMineList.Minelist;

        // Dependency injection через метод
        public void SetForbiddenSpawnPoints(List<Transform> forbiddenPoints)
        {
            _forbiddenSpawnPoints.Clear();
            _forbiddenSpawnPoints.AddRange(forbiddenPoints);
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            InitializeMines();
            StartingSpawn();
            _isInitialized = true;
        }

        private void InitializeMines()
        {
            if (healMineList == null) healMineList = new MineList(healConfig.maxCount);
            if (damageMineList == null) damageMineList = new MineList(damageConfig.maxCount);
            if (buffMineList == null) buffMineList = new MineList(buffConfig.maxCount);
            if (debuffMineList == null) debuffMineList = new MineList(debuffConfig.maxCount);

            healMineList.InitializeMines(HealMinePrefab, healCooldown, (n, c, go) => new HealMine(n, c, go));
            damageMineList.InitializeMines(DamageMinePrefab, damageCooldown, (n, c, go) => new DamageMine(n, c, go));
            buffMineList.InitializeSpeedBuffMines(BuffMinePrefab, speedBufCooldown, speedBuf, buffTime, buffTimeBeforeExplosion, buffRadiusOfExplosion, buffDamage, false);
            debuffMineList.InitializeSpeedBuffMines(DebuffMinePrefab, speedDebufCooldown, speedDebuf, debuffTime, debuffTimeBeforeExplosion, debuffRadiusOfExplosion, debuffDamage, true);

            CacheOriginalScales();
        }

        public void ClearAllMineObjects()
        {
            ClearMineObjects(healMineList);
            ClearMineObjects(damageMineList);
            ClearMineObjects(buffMineList);
            ClearMineObjects(debuffMineList);

            _originalScales.Clear();
        }

        private void ClearMineObjects(MineList mineList)
        {
            if (mineList == null) return;

            foreach (var mine in mineList.Minelist)
            {
                if (mine.MineGameObject != null)
                {
                    Destroy(mine.MineGameObject);
                }
            }
            mineList.Minelist.Clear();
        }

        private void CacheOriginalScales()
        {
            _originalScales.Clear();
            foreach (var mine in EnumerateAllMines())
                _originalScales[mine] = mine.MineGameObject.transform.localScale;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            ClearAllMineObjects();
        }

        private void StartingSpawn()
        {
            SpawnInitialMines(healMineList, 0, healConfig.initialCount);
            SpawnInitialMines(damageMineList, 1, damageConfig.initialCount);
            SpawnInitialMines(buffMineList, 2, buffConfig.initialCount);
            SpawnInitialMines(debuffMineList, 3, debuffConfig.initialCount);

            StartCoroutine(SpawnRoutine(healMineList, 0, healConfig));
            StartCoroutine(SpawnRoutine(damageMineList, 1, damageConfig));
            StartCoroutine(SpawnRoutine(buffMineList, 2, buffConfig));
            StartCoroutine(SpawnRoutine(debuffMineList, 3, debuffConfig));
            StartCoroutine(RecoveryRoutine());
        }

        private IEnumerator SpawnRoutine(MineList list, uint typeId, MineSpawnConfig cfg)
        {
            yield return null;
            while (true)
            {
                yield return new WaitForSeconds(cfg.RandomInterval);

                int activeCount = CountActiveMines(list);
                if (typeId == 3 && activeCount >= cfg.maxCount)
                {
                    Mine toRemove = FindActiveMine(list);
                    if (toRemove != null)
                    {
                        toRemove.SetActive(false);
                    }
                }
                else if (activeCount >= cfg.maxCount)
                {
                    continue;
                }

                TrySpawnMine(list, typeId);
            }
        }

        private IEnumerator RecoveryRoutine()
        {
            if (minimumTotalActiveMines <= 0)
            {
                yield break;
            }

            yield return null;
            while (true)
            {
                yield return new WaitForSeconds(recoveryCheckInterval);

                int totalDeficit = Mathf.Max(0, minimumTotalActiveMines - CountAllActiveMines());
                int positiveDeficit = CountPositiveMineDeficit();
                int remainingToSpawn = Mathf.Min(recoverySpawnBatchSize, Mathf.Max(totalDeficit, positiveDeficit));
                if (remainingToSpawn <= 0)
                {
                    continue;
                }

                // Сначала стараемся вернуть поле к стартовому балансу, а уже потом просто добираем общее количество.
                TrySpawnRecoveryMine(healMineList, 0, healConfig.initialCount, ref remainingToSpawn);
                TrySpawnRecoveryMine(damageMineList, 1, damageConfig.initialCount, ref remainingToSpawn);
                TrySpawnRecoveryMine(buffMineList, 2, buffConfig.initialCount, ref remainingToSpawn);

                if (remainingToSpawn <= 0)
                {
                    continue;
                }

                TrySpawnRecoveryMine(healMineList, 0, healConfig.maxCount, ref remainingToSpawn);
                TrySpawnRecoveryMine(damageMineList, 1, damageConfig.maxCount, ref remainingToSpawn);
                TrySpawnRecoveryMine(buffMineList, 2, buffConfig.maxCount, ref remainingToSpawn);
                TrySpawnRecoveryMine(debuffMineList, 3, debuffConfig.maxCount, ref remainingToSpawn);
            }
        }

        private void SpawnInitialMines(MineList list, uint typeId, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                TrySpawnMine(list, typeId);
            }
        }

        private bool SpawnMineAnimated(Mine mine)
        {
            if (!TryFindValidPosition(out Vector3 pos))
            {
                return false;
            }

            mine.MineGameObject.transform.position = pos;
            if (parentTransform != null) mine.MineGameObject.transform.SetParent(parentTransform);

            Vector3 targetScale = _originalScales.TryGetValue(mine, out var s) ? s : Vector3.one;
            mine.MineGameObject.transform.localScale = Vector3.zero;

            Collider[] colliders = mine.MineGameObject.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = false;
            }

            mine.SetActive(true);
            StartCoroutine(AnimateScale(mine.MineGameObject.transform, targetScale));
            return true;
        }

        private IEnumerator AnimateScale(Transform t, Vector3 to)
        {
            float elapsed = 0f;
            while (elapsed < spawnAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float p = Mathf.Clamp01(elapsed / spawnAnimationDuration);
                t.localScale = Vector3.Lerp(Vector3.zero, to, EasingOutBack(p));
                yield return null;
            }
            t.localScale = to;

            Collider[] colliders = t.gameObject.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                collider.enabled = true;
            }
        }

        private float EasingOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private Mine FindActiveMine(MineList list)
        {
            foreach (var m in list.Minelist)
                if (m.MineGameObject.activeSelf) return m;
            return null;
        }

        private bool TryFindValidPosition(out Vector3 position)
        {
            Vector3 cand = default;
            int safe = 0;
            do
            {
                int x = Mathf.RoundToInt(Random.Range(CenterPoint.position.x - spawnAreaSize.x * 0.5f, CenterPoint.position.x + spawnAreaSize.x * 0.5f));
                int z = Mathf.RoundToInt(Random.Range(CenterPoint.position.z - spawnAreaSize.y * 0.5f, CenterPoint.position.z + spawnAreaSize.y * 0.5f));
                cand = new Vector3(x, yPositionOfSpawnMine, z);
                safe++;
            }
            while (!IsPositionFree(cand) && safe < 1000);

            position = cand;
            return safe < 1000 || IsPositionFree(cand);
        }

        private bool IsPositionFree(Vector3 pos)
        {
            foreach (var f in _forbiddenSpawnPoints)
                if (f != null && Vector3.Distance(pos, f.position) < allowedDistanseForForrbidenSpawnPoint)
                    return false;

            foreach (var m in EnumerateAllMines())
                if (m.MineGameObject.activeSelf && Vector3.Distance(pos, m.MineGameObject.transform.position) < minDistanceBetweenMines)
                    return false;
            return true;
        }

        private IEnumerable<Mine> EnumerateAllMines()
        {
            if (healMineList != null)
                foreach (var m in healMineList.Minelist) yield return m;
            if (damageMineList != null)
                foreach (var m in damageMineList.Minelist) yield return m;
            if (buffMineList != null)
                foreach (var m in buffMineList.Minelist) yield return m;
            if (debuffMineList != null)
                foreach (var m in debuffMineList.Minelist) yield return m;
        }

        private int CountActiveMines(MineList list)
        {
            int c = 0;
            foreach (var m in list.Minelist) if (m.MineGameObject.activeSelf) c++;
            return c;
        }

        private int CountAllActiveMines()
        {
            return CountActiveMines(healMineList) +
                   CountActiveMines(damageMineList) +
                   CountActiveMines(buffMineList) +
                   CountActiveMines(debuffMineList);
        }

        private int CountPositiveMineDeficit()
        {
            return Mathf.Max(0, healConfig.initialCount - CountActiveMines(healMineList)) +
                   Mathf.Max(0, damageConfig.initialCount - CountActiveMines(damageMineList)) +
                   Mathf.Max(0, buffConfig.initialCount - CountActiveMines(buffMineList));
        }

        private Mine FindInactiveMine(MineList list)
        {
            foreach (var m in list.Minelist) if (!m.MineGameObject.activeSelf) return m;
            return null;
        }

        private void TrySpawnRecoveryMine(MineList list, uint typeId, int desiredActiveCount, ref int remainingToSpawn)
        {
            if (remainingToSpawn <= 0 || CountActiveMines(list) >= desiredActiveCount)
            {
                return;
            }

            if (TrySpawnMine(list, typeId))
            {
                remainingToSpawn--;
            }
        }

        private bool TrySpawnMine(MineList list, uint typeId)
        {
            Mine candidate = FindInactiveMine(list);
            if (candidate == null && list.Minelist.Count < GetMaxCount(typeId))
            {
                AddMineByType(typeId);
                candidate = list.Minelist[^1];
            }

            if (candidate == null || candidate.MineGameObject.activeSelf)
            {
                return false;
            }

            return SpawnMineAnimated(candidate);
        }

        private int GetMaxCount(uint typeId)
        {
            return typeId switch
            {
                0 => healConfig.maxCount,
                1 => damageConfig.maxCount,
                2 => buffConfig.maxCount,
                3 => debuffConfig.maxCount,
                _ => 0
            };
        }

        private void AddMineByType(uint type)
        {
            switch (type)
            {
                case 0:
                    healMineList.AddMine(HealMinePrefab, healCooldown, (n, cd, go) => new HealMine(n, cd, go));
                    break;
                case 1:
                    damageMineList.AddMine(DamageMinePrefab, damageCooldown, (n, cd, go) => new DamageMine(n, cd, go));
                    break;
                case 2:
                    buffMineList.AddMine(BuffMinePrefab, speedBufCooldown, speedBuf, buffTime, buffTimeBeforeExplosion,
                        buffRadiusOfExplosion, buffDamage,
                        (n, cd, go, s, cd2, tbe, r, d) => new BuffSpeedMine(n, cd, go, s, cd2, tbe, r, d, false));
                    break;
                case 3:
                    debuffMineList.AddMine(DebuffMinePrefab, speedDebufCooldown, speedDebuf, debuffTime, debuffTimeBeforeExplosion,
                        debuffRadiusOfExplosion, debuffDamage,
                        (n, cd, go, s, cd2, tbe, r, d) => new BuffSpeedMine(n, cd, go, s, cd2, tbe, r, d, true));
                    break;
            }
            CacheOriginalScales();
        }
    }
}
