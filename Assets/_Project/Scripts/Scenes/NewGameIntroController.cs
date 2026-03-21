using System;
using System.Collections;
using UnityEngine;
using Zenject;

namespace Game
{
    public class NewGameIntroController : MonoBehaviour
    {
        private LoadingManager loadingManager;

        [Inject]
        private void Construct(LoadingManager loadingManager)
        {
            this.loadingManager = loadingManager;
        }

        private void Start()
        {
            StartCoroutine(WaitForInput());
        }

        private IEnumerator WaitForInput()
        {
            Time.timeScale = 0f;

            float timer = 0f;
            while (true)
            {
                if (Input.anyKeyDown)
                    break;
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            loadingManager.LoadScene("CreateCharacter");
            Time.timeScale = 1f;
        }
    }

}