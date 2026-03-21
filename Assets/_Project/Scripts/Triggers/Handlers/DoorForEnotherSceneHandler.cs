using Game.Data;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Zenject;
using System.Threading.Tasks;

namespace Game
{

    internal class DoorForEnotherSceneHandler : MonoBehaviour
    {
        /*{
            "sceneName": "Level2",
            "spawnPosition": {"x": 10.5, "y": 0, "z": 20.3},
            "spawnEulerAngles": {"x": 0, "y": 90, "z": 0},
            "dayNumber": 2
            }
         */

        [SerializeField] private List<DoorInfluenceArea> influenceArias;
        private LoadingManager loadingManager;
        private GameflowManager gameflowManager;

        [Inject]
        public void Construct([InjectOptional] LoadingManager loadingManager, [InjectOptional] GameflowManager gameflowManager)
        {
            this.loadingManager = loadingManager;
            this.gameflowManager = gameflowManager;
        }

        private void OnEnable()
        {
            foreach (var area in influenceArias)
            {
                area.OnDoorInteracted.Subscribe(HandleAsync);
            }
        }

        private void OnDisable()
        {
            foreach (var area in influenceArias)
            {
                area.OnDoorInteracted.Unsubscribe(HandleAsync);
            }
        }

        public async Task HandleAsync((TriggerEvent triggerEvent, DoorParameters doorParams) data)
        {
            var (eventData, doorParameters) = data;

            if (!eventData.IsEnteracted) return;
            loadingManager ??= LoadingManager.Instance;
            if (loadingManager == null)
            {
                return;
            }

            if (ShouldTrigger(doorParameters.DayNumber))
            {
                PlayerSpawnData.SpawnPosition = doorParameters.SpawnPosition;
                PlayerSpawnData.SpawnRotation = Quaternion.Euler(doorParameters.SpawnEulerAngles);

                loadingManager.LoadScene(doorParameters.SceneName);

                //Debug.Log($"Переход на сцену: {doorParameters.SceneName}");
            }
        }       

        private bool ShouldTrigger(int targetDayNumber)
        {
            if (targetDayNumber == -1) return true;
            if (gameflowManager == null) return false;
            return targetDayNumber == this.gameflowManager.CurrentDay.Id;
        }
    }
}
