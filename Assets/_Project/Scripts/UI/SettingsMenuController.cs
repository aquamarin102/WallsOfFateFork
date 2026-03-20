using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public class SettingsMenuController : MonoBehaviour
    {
        public Slider sliderMusic;
        public Slider sliderSFX;
        public Slider sliderUI;
        public TMPro.TMP_Dropdown languageDropdown;
        public GameObject closeButton;

        private bool isLoading = false;

        private LocalizationManager lm;
        [Inject]
        public void Construct(LocalizationManager lm)
        {
            this.lm = lm;
        }

        private void Start()
        {
            sliderMusic.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            sliderSFX.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sliderUI.value = PlayerPrefs.GetFloat("UIVolume", 1f);
            BindCloseButton();

            InitializeLanguageDropdown();
            ApplySettings();
        }

        void Update()
        {
            if (gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                HideSettingsMenu();
            }
        }

        private void InitializeLanguageDropdown()
        {
            isLoading = true;
            languageDropdown.ClearOptions();

            List<TMPro.TMP_Dropdown.OptionData> options = new();

            List<Dictionary<string, string>> currentLanguageList = lm.LocalizationConfig[lm.CurrentLanguage];

            foreach (var dict in currentLanguageList)
            {
                foreach (var languageName in dict.Keys)
                {
                    options.Add(new TMPro.TMP_Dropdown.OptionData(languageName));
                }
            }

            languageDropdown.AddOptions(options);

            int selectedIndex = FindCurrentLanguageIndex(currentLanguageList);
            languageDropdown.value = selectedIndex;

            isLoading = false;

            languageDropdown.onValueChanged.RemoveAllListeners();
            languageDropdown.onValueChanged.AddListener(OnLanguageSelected);
        }

        private int FindCurrentLanguageIndex(List<Dictionary<string, string>> languageList)
        {
            for (int i = 0; i < languageList.Count; i++)
            {
                foreach (var kvp in languageList[i])
                {
                    if (kvp.Value == lm.CurrentLanguage)
                    {
                        return i;
                    }
                }
            }
            return 0;
        }

        private void OnLanguageSelected(int index)
        {
            if (isLoading) return;

            List<Dictionary<string, string>> currentLanguageList = lm.LocalizationConfig[lm.CurrentLanguage];

            if (index < currentLanguageList.Count)
            {
                var selectedDict = currentLanguageList[index];

                foreach (var kvp in selectedDict)
                {
                    lm.CurrentLanguage = kvp.Value;
                    break;
                }                    
            }
        }

        public void ApplySettings()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetVolume("Volume_Music", sliderMusic.value);
                AudioManager.Instance.SetVolume("Volume_SFX", sliderSFX.value);
                AudioManager.Instance.SetVolume("Volume_UI", sliderUI.value);
            }

            PlayerPrefs.SetFloat("MusicVolume", sliderMusic.value);
            PlayerPrefs.SetFloat("SFXVolume", sliderSFX.value);
            PlayerPrefs.SetFloat("UIVolume", sliderUI.value);
            PlayerPrefs.Save();
        }

        public void HideSettingsMenu()
        {
            gameObject.SetActive(false);
        }

        public void ShowSettingsMenu()
        {
            gameObject.SetActive(true);
        }

        private void BindCloseButton()
        {
            if (closeButton == null)
            {
                Button[] buttons = GetComponentsInChildren<Button>(true);

                foreach (Button button in buttons)
                {
                    if (button.gameObject.name == "CloseButton")
                    {
                        closeButton = button.gameObject;
                        break;
                    }
                }
            }

            if (closeButton != null && closeButton.TryGetComponent<Button>(out Button buttonComponent))
            {
                buttonComponent.onClick.RemoveListener(HideSettingsMenu);
                buttonComponent.onClick.AddListener(HideSettingsMenu);
            }
        }
    }
}