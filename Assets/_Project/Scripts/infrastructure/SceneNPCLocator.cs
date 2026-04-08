using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class SceneNPCLocator : MonoInstaller
    {
        public List<GameObject> NPC;
        public Transform Parent; 

        public List<Transform> StartPoints; 

        public override void InstallBindings()
        {
            InstantiateNPSPrefab();
        }

        private void InstantiateNPSPrefab()
        {
            if (NPC.Count != StartPoints.Count)
            {
                Debug.LogError("Количество NPC и точек старта должно быть одинаковым!");
                return;
            }

            Container.Bind<NPCPrefabFactory>()
                .AsSingle()
                .WithArguments(NPC);

            var factory = Container.Resolve<NPCPrefabFactory>();

            for (int i = 0; i < NPC.Count; i++)
            {
                GameObject prefab = NPC[i];
                Transform startPoint = StartPoints[i];

                // Проверяем наличие нужных компонентов на префабе или его дочерних объектах
                //bool shouldInstantiate = CheckQuestConditions(prefab);

                Vector3 spawnPosition = startPoint.position;
                Quaternion spawnRotation = startPoint.rotation;
                string npcName = prefab.name;

                factory.Create(npcName, spawnPosition, spawnRotation, Parent);
                
                //if (/*shouldInstantiate || */!EnableCheck[i])
                //{
                //    // Получаем позицию и поворот из Transform точки старта

                //    // Инстанцируем NPC
                //    // GameObject instance = Instantiate(prefab, spawnPosition, spawnRotation, Parent);
                //    // Container.Bind<GameObject>()
                //    //.WithId(prefab.name)
                //    //.FromInstance(prefab)
                //    //.AsCached();
                //}
                //else
                //{
                //    Debug.Log($"NPC {prefab.name} не создан, так как не выполнены условия квеста.");
                //}
            }
        }
    }
}
