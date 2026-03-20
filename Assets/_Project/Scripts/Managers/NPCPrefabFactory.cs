using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace Game
{
    public class NPCPrefabFactory
    {
        private readonly List<GameObject> prefabs;
        private readonly DiContainer container; 
        public readonly Dictionary<string, GameObject> instances = new();

        public NPCPrefabFactory(List<GameObject> prefabs, DiContainer container)
        {
            this.prefabs = prefabs;
            this.container = container;
        }

        public GameObject GetPrefab(string name)
        {
            return prefabs.FirstOrDefault(p => p.name == name);
        }

        public GameObject Create(string name, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            GameObject prefab = GetPrefab(name);
            if (prefab == null)
            {
                Debug.LogError($"Префаб {name} не найден!");
                return null;
            }

            GameObject instance = container.InstantiatePrefab(prefab, position, rotation, parent);
            //instance.name = $"{name}_{System.Guid.NewGuid().ToString().Substring(0, 4)}";
            instance.name = $"{name}";

            instances[name] = instance;

            //Debug.Log($"Создан NPC {instance.name} на позиции {position}");
            return instance;
        }

        public GameObject GetInstance(string name)
        {
            if (instances.TryGetValue(name, out GameObject instance))
            {
                return instance;
            }

            Debug.LogError($"NPC {name} еще не создан или не существует!");
            return null;
        }

        public bool HasInstance(string name)
        {
            return instances.ContainsKey(name);
        }
    }
}