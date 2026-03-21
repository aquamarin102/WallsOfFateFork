using Game.Data;
using UnityEngine;
using Zenject;

namespace Game.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private const string CharacterCreationSceneName = "CreateCharacter";
        private const string FallbackGameplaySceneName = "StartDay";

        [Header("Buttons")]
        public GameObject startGameButton;
        public GameObject continueGameButton;
        public GameObject settingsButton;
        public GameObject exitGameButton;

        [Header("UI Panels")]
        public GameObject newGameConfirmationPanel;        
        public GameObject exitConfirmationPanel;
        public GameObject settingsPanel;

        public string firstScene;

        public static event System.Action NewGameStarted;

        private LoadingManager loadingManager;
        private SaveLoadManager saveLoadManager;

        [Inject]
        public void Construct(LoadingManager loadingManager, SaveLoadManager saveLoadManager)
        {
            this.loadingManager = loadingManager;
            this.saveLoadManager = saveLoadManager;
        }

        private void Awake()
        {
            if (saveLoadManager.CanLoad())
            {
                continueGameButton.SetActive(true);
            }
            else
            {
                continueGameButton.SetActive(false);
            }
        }

        public void OnStartGameButtonClick()
        {
            if (saveLoadManager.CanLoad())
            {
                newGameConfirmationPanel.SetActive(true);
            }
            else
            {
                StartGame();
            }
        }

        public void OnContinueGameButtonClick()
        {
            saveLoadManager.LoadGame();

            loadingManager.LoadScene(ResolveEntryScene());
        }

        public void OnSettingsButtonClick()
        {
            settingsPanel.SetActive(true);
        }

        public void OnExitButtonClick()
        {
            exitConfirmationPanel.SetActive(true);
        }

        public void StartGame()
        {
            saveLoadManager.Clear();

            NewGameStarted?.Invoke();

            loadingManager.LoadScene(ResolveEntryScene());
        }

        private string ResolveEntryScene()
        {
            if (string.IsNullOrWhiteSpace(firstScene) || firstScene == CharacterCreationSceneName)
            {
                return FallbackGameplaySceneName;
            }

            return firstScene;
        }
    }
}
