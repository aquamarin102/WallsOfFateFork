using System;
using UnityEngine;
using Zenject;

namespace Game
{
    public class ScenePlayerLocator : MonoInstaller
    {
        public Transform StartPoint;
        public GameObject Prefab;
        public Transform Parent;
        public Transform CameraTransform;

        public override void InstallBindings()
        {
            BindCameraTransform();
            InstantiateMainCharacter();
            BindCameraController();
        }

        private void BindCameraController()
        {
            Container.Bind<CameraMovementController>().FromComponentInHierarchy().AsSingle();
        }

        private void InstantiateMainCharacter()
        {
            if (Prefab == null)
            {
                Debug.LogError("Prefab не назначен в инспекторе!", this);
                return;
            }

            // Определяем начальную позицию: берём из PlayerSpawnData, если она задана
            Vector3 spawnPosition = PlayerSpawnData.SpawnPosition != Vector3.zero
                ? PlayerSpawnData.SpawnPosition
                : StartPoint.position;
            Quaternion spawnRotation = PlayerSpawnData.SpawnRotation != Quaternion.identity
                ? PlayerSpawnData.SpawnRotation
                : StartPoint.rotation;

            PlayerMoveController playerMoveController = Container
                .InstantiatePrefabForComponent<PlayerMoveController>(Prefab, spawnPosition, spawnRotation, Parent);

            PlayerObjectUtility.NormalizeSpawnedPlayer(playerMoveController);

            Container
                .Bind<PlayerMoveController>()
                .FromInstance(playerMoveController)
                .AsSingle();
        }

        private void BindCameraTransform()
        {
            Container.Bind<Transform>().FromInstance(CameraTransform).AsSingle();
        }
    }
}
