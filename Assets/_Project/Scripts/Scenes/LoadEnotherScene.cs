using UnityEngine;
using System.Collections.Generic;
using Zenject;

namespace Game
{
    public class LoadAnotherScene : MonoBehaviour
    {
        [SerializeField] private List<DoorForAnotherScene> doors;
        private LoadingManager loadingManager;

        [Inject]
        public void Construct(LoadingManager loadingManager)
        {
            this.loadingManager = loadingManager;
        }

        private void Start()
        {
            foreach (var door in doors)
            {
                if (door != null)
                {
                    door.OnActivated += LoadScene;
                }
            }
        }

        public void LoadScene(string sceneName)
        {
            loadingManager.LoadScene(sceneName);
        }

        private void OnDestroy()
        {
            foreach (var door in doors)
            {
                if (door != null)
                {
                    door.OnActivated -= LoadScene;
                }
            }
        }
    }

}
