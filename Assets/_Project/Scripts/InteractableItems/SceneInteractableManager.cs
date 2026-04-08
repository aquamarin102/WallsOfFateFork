using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    [DefaultExecutionOrder(1000)]        // Start сработает после Start всех предметов
    public class SceneInteractableManager : MonoBehaviour
    {
        /* ─────────────  НАСТРОЙКИ  ───────────── */

        [Header("Сколько предметов спавнить (минимум / максимум, включительно)")]
        public int minItemsToEnable = 3;
        public int maxItemsToEnable = 6;

        [Header("Перетащите сюда ВСЕ InteractableItem, которые могут появляться")]
        public List<InteractableItemInfluenceArea> items = new();   // только вручную

        /* ─────────────────────────────────────── */

        public int itemsToEnableThisRun;   // выбранное число для текущего старта

        private void OnValidate()
        {
            // Удалить null-ссылки и дубликаты
            items = items.Where(i => i != null)
                         .Distinct()
                         .ToList();

            // Корректируем диапазон относительно размера списка
            int maxPossible = items.Count;

            if (minItemsToEnable < 1) minItemsToEnable = 1;
            if (maxItemsToEnable < minItemsToEnable)
                maxItemsToEnable = minItemsToEnable;

            if (minItemsToEnable > maxPossible) minItemsToEnable = maxPossible;
            if (maxItemsToEnable > maxPossible) maxItemsToEnable = maxPossible;
        }

        private void Start()
        {
            if (items.Count == 0)
            {
                items = GetComponentsInChildren<InteractableItemInfluenceArea>(true).ToList();
                //Debug.LogWarning($"{name}: список предметов пуст — заполните его в инспекторе!");
                //return;
            }

            // выбираем случайное число предметов (включительно)
            minItemsToEnable = 3;
            maxItemsToEnable = items.Count;
            itemsToEnableThisRun = Random.Range(minItemsToEnable, maxItemsToEnable + 1);

            // ждём один кадр, чтобы все InteractableItem успели выполнить Start
            StartCoroutine(SpawnAtEndOfFrame());
        }

        private System.Collections.IEnumerator SpawnAtEndOfFrame()
        {
            yield return null;            // конец текущего кадра
            SpawnFreshBatch();
        }

        /* ─────────────  СПАВН  ───────────── */

        private void SpawnFreshBatch()
        {
            // 1. Выключаем всё
            foreach (var it in items)
                it.gameObject.SetActive(false);

            // 2. Собираем неиспользованные
            var candidates = items.Where(i => !i.HasBeenUsed).ToList();

            // 3. Если не хватает — «реанимируем» использованные
            int shortage = itemsToEnableThisRun - candidates.Count;
            if (shortage > 0)
            {
                var used = items.Where(i => i.HasBeenUsed).ToList();
                Shuffle(used);

                //for (int i = 0; i < Mathf.Min(shortage, used.Count); i++)
                //{
                //    used[i].ResetForRespawn();
                //    candidates.Add(used[i]);
                //}
            }

            // 4. Перемешиваем
            Shuffle(candidates);

            // 5. Включаем нужное количество
            int count = Mathf.Min(itemsToEnableThisRun, candidates.Count);
            for (int i = 0; i < count; i++)
                candidates[i].gameObject.SetActive(true);
        }

        /* ─────────────  HELPERS  ───────────── */

        private static void Shuffle<T>(IList<T> list)          // Fisher–Yates
        {
            for (int i = 0; i < list.Count; i++)
            {
                int j = Random.Range(i, list.Count);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

}

