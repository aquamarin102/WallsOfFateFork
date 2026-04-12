using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public class ExitConfirmationPanelController : MonoBehaviour
    {
        private const string MainMenuSceneName = "MainMenu";

        [SerializeField] private GameObject panelRoot;

        public Button confirmButton;
        public Button declineButton;

        private LoadingManager loadingManager;

        [Inject]
        private void Construct([InjectOptional] LoadingManager loadingManager)
        {
            this.loadingManager = loadingManager;
        }

        private void Awake()
        {
            ResolvePanelRoot();
        }

        private void Start()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(QuitToMenuGame);
                confirmButton.onClick.AddListener(QuitToMenuGame);
            }

            if (declineButton != null)
            {
                declineButton.onClick.RemoveListener(HideExitPanel);
                declineButton.onClick.AddListener(HideExitPanel);
            }
        }

        private void Update()
        {
            if (IsPanelVisible() && Input.GetKeyDown(KeyCode.Escape))
            {
                HideExitPanel();
            }
        }

        private void OnEnable()
        {
            if (IsPanelVisible())
            {
                SelectDeclineButton();
            }
        }

        public void ShowExitPanel()
        {
            SetPanelActive(true);
            SelectDeclineButton();
        }

        public void HideExitPanel()
        {
            SetPanelActive(false);
            ClearSelection();
        }

        public void QuitGame()
        {
            if (SceneManager.GetActiveScene().name != MainMenuSceneName)
            {
                LoadMainMenu();
                return;
            }

            Application.Quit();
        }

        public void QuitToMenuGame()
        {
            if (SceneManager.GetActiveScene().name == MainMenuSceneName)
            {
                QuitGame();
                return;
            }

            LoadMainMenu();
        }

        private void LoadMainMenu()
        {
            HideExitPanel();

            if (loadingManager != null)
                loadingManager.LoadScene(MainMenuSceneName);
            else
                SceneManager.LoadScene(MainMenuSceneName);
        }

        private void ResolvePanelRoot()
        {
            if (panelRoot != null)
                return;

            panelRoot = FindPanelRoot(confirmButton);

            if (panelRoot == null)
            {
                panelRoot = FindPanelRoot(declineButton);
            }
        }

        private GameObject FindPanelRoot(Button button)
        {
            if (button == null)
                return null;

            Transform current = button.transform;

            while (current != null)
            {
                if (current.name == "ExitConfirmationPanel")
                    return current.gameObject;

                current = current.parent;
            }

            return null;
        }

        private bool IsPanelVisible()
        {
            return panelRoot != null ? panelRoot.activeInHierarchy : gameObject.activeInHierarchy;
        }

        private void SetPanelActive(bool isActive)
        {
            if (panelRoot == null)
            {
                ResolvePanelRoot();
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(isActive);
            }
        }

        private void SelectDeclineButton()
        {
            if (EventSystem.current == null || declineButton == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            declineButton.Select();
        }

        private void ClearSelection()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}
