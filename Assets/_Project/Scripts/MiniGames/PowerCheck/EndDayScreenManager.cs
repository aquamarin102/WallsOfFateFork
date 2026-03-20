using System;
using System.Collections;
using UnityEngine;

namespace Game.MiniGame.PowerCheck
{
    public class EndDayScreenManager : MonoBehaviour
    {
        [SerializeField] private GameObject WinScreen;
        [SerializeField] private GameObject LooseScreen;
        [SerializeField] private GameObject GameProcess;
        [SerializeField] private DexMiniGameController AgilityController;
        [SerializeField] private ExecutionManager InteligenceController;
        [SerializeField] private float displayTime = 3f;

        public Action<bool> OnEndGame;
        bool playerWonGlobal;

        void Start()
        {

            if (GameProcess != null) GameProcess.GetComponent<GameProcess>().OnEndGame += DisplayEndScreen;
            if (AgilityController != null) AgilityController.OnEndGame += DisplayEndScreen;
            if (InteligenceController != null) InteligenceController.OnEndGame += DisplayEndScreen;
            WinScreen.SetActive(false);
            LooseScreen.SetActive(false);
        }

        private void DisplayEndScreen(bool playerWon)
        {
            Time.timeScale = 0f;
            GameObject endScreen = playerWon ? WinScreen : LooseScreen;
            playerWonGlobal = playerWon;

            endScreen.SetActive(true);

            StartCoroutine(HideScreenAfterDelay(endScreen, displayTime));
        }

        private IEnumerator HideScreenAfterDelay(GameObject screen, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            screen.SetActive(false);
            Time.timeScale = 1f;
            OnEndGame?.Invoke(playerWonGlobal);
        }

        void OnDestroy()
        {
            if (GameProcess != null)
                GameProcess.GetComponent<GameProcess>().OnEndGame -= DisplayEndScreen;
        }
    }
}
