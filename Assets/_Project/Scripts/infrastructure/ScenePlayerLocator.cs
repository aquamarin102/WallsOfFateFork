using UnityEngine;
using Zenject;

namespace Game
{
    public class ScenePlayerLocator : MonoInstaller
    {
        public const string GameplayCameraTransformBindingId = "GameplayCameraTransform";

        public Transform StartPoint;
        public GameObject Prefab;
        public Transform Parent;
        public Transform CameraTransform;

        public override void InstallBindings()
        {
            BindCameraTransform();
            PlayerMoveController playerMoveController = InstantiateMainCharacter();
            ConfigureCameraTargets(playerMoveController);
            BindCameraController();
        }

        private void BindCameraController()
        {
            if (CameraTransform == null)
            {
                Debug.LogError("CameraTransform is not assigned in the inspector.", this);
                return;
            }

            CameraMovementController cameraController = CameraTransform.GetComponent<CameraMovementController>();
            if (cameraController == null)
            {
                Debug.LogError("CameraMovementController was not found on CameraTransform.", CameraTransform);
                return;
            }

            Container.Bind<CameraMovementController>().FromInstance(cameraController).AsSingle();
        }

        private PlayerMoveController InstantiateMainCharacter()
        {
            if (Prefab == null)
            {
                Debug.LogError("Prefab is not assigned in the inspector.", this);
                return null;
            }

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

            return playerMoveController;
        }

        private void BindCameraTransform()
        {
            if (CameraTransform == null)
            {
                Debug.LogError("CameraTransform is not assigned in the inspector.", this);
                return;
            }

            Container.Bind<Transform>()
                .WithId(GameplayCameraTransformBindingId)
                .FromInstance(CameraTransform)
                .AsSingle();
        }

        private void ConfigureCameraTargets(PlayerMoveController playerMoveController)
        {
            if (playerMoveController == null || CameraTransform == null)
            {
                return;
            }

            CameraMovementController cameraController = CameraTransform.GetComponent<CameraMovementController>();
            if (cameraController != null)
            {
                cameraController.SetTarget(playerMoveController.transform);
            }

            CameraObstacleTransparency obstacleTransparency = CameraTransform.GetComponent<CameraObstacleTransparency>();
            if (obstacleTransparency != null)
            {
                obstacleTransparency.SetTarget(playerMoveController.transform);
            }
        }
    }
}
