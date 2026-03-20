using Assets._Project.Scripts.Managers;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;
using Game.UI;

namespace Game
{
    public class ToggleObjectOnButtonPress : MonoBehaviour
    {
        [System.Serializable]
        public class ToggleButton
        {
            public KeyCode keyCode;  // Клавиша, на которую реагируем
            public bool setActive;   // В какое состояние переводим TargetObject
        }

        [SerializeField]
        private List<ToggleButton> toggleButtons = new List<ToggleButton>();

        [field: SerializeField]
        public GameObject TargetObject { get; private set; }

        private LoadingManager _loadingManager;

        [Inject]
        private void Construct(LoadingManager loadingManager)
        {
            _loadingManager = loadingManager;
        }

        void Update()
        {
            //  Если загрузочный экран активен — выходим и не обрабатываем Esc/другие кнопки
            if (_loadingManager != null && _loadingManager.IsLoading)
                return;

            foreach (var button in toggleButtons)
            {
                if (Input.GetKeyDown(button.keyCode))
                {
                    if (button.setActive && TargetObject != null && TargetObject.activeSelf && TryCloseNestedPanel())
                    {
                        return;
                    }

                    Activate(button.setActive);
                }
            }
        }

        public void Activate(bool station)
        {
            // Здесь можно ещё добавить защиту от двойного срабатывания:
            // if (LoadingScreenManager.Instance != null && LoadingScreenManager.Instance.IsLoading) return;

            if (TargetObject == null)
                return;

            if (station)
            {
                SetTargetState(!TargetObject.activeSelf);
            }
            else
            {
                SetTargetState(false);
            }
        }

        private void SetTargetState(bool isActive)
        {
            if (!isActive)
            {
                CloseNestedPanels();
            }

            TargetObject.SetActive(isActive);
        }

        private bool TryCloseNestedPanel()
        {
            if (TryCloseSettingsPanel())
                return true;

            if (TryCloseChildPanel("ExitConfirmationPanel"))
                return true;

            return false;
        }

        private void CloseNestedPanels()
        {
            while (TryCloseNestedPanel())
            {
            }
        }

        private bool TryCloseSettingsPanel()
        {
            SettingsMenuController[] settingsPanels = TargetObject.GetComponentsInChildren<SettingsMenuController>(true);

            foreach (SettingsMenuController settingsPanel in settingsPanels)
            {
                if (settingsPanel != null && settingsPanel.gameObject.activeSelf)
                {
                    settingsPanel.HideSettingsMenu();
                    return true;
                }
            }

            return false;
        }

        private bool TryCloseChildPanel(string panelName)
        {
            Transform[] childTransforms = TargetObject.GetComponentsInChildren<Transform>(true);

            foreach (Transform childTransform in childTransforms)
            {
                if (childTransform == null || childTransform == TargetObject.transform)
                    continue;

                if (childTransform.name == panelName && childTransform.gameObject.activeSelf)
                {
                    childTransform.gameObject.SetActive(false);
                    return true;
                }
            }

            return false;
        }
    }
}
