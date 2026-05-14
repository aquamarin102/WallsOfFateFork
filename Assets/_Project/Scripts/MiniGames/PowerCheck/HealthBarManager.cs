using UnityEngine;
using UnityEngine.UI;

namespace Game.MiniGame.PowerCheck
{
    public class HealthBarManager : MonoBehaviour
    {
        private static readonly string[] PortraitResourceFolders =
        {
            "MiniGames/PowerCheck/old/PowerCheckPortraits",
            "MiniGames/PowerCheck/PowerCheckPortraits"
        };

        private Slider _healthBar;
        private MiniGamePlayer _player;
        private Text _healthBarText;
        private Image _portraitImage;
        private bool _isSubscribed;

        /// <summary>
        /// Назначает полоску здоровья (вызывается из MiniGameInstaller).
        /// </summary>
        /// <param name="healthBar">Slider для отображения здоровья.</param>
        public void SetHealthBar(Slider healthBar)
        {
            _healthBar = healthBar;
            TryInitialize();
        }

        private void Start()
        {
            TryInitialize();
        }

        private void OnDestroy()
        {
            if (_player != null && _isSubscribed)
            {
                _player.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void TryInitialize()
        {
            _player ??= GetComponent<MiniGamePlayer>();
            if (_player == null)
            {
                Debug.LogError("Компонент MiniGamePlayer не найден!", this);
                return;
            }

            if (_healthBar == null)
            {
                return;
            }

            if (gameObject.activeSelf)
            {
                _healthBar.gameObject.SetActive(true);
            }

            CacheHealthBarReferences();

            if (!_isSubscribed)
            {
                _player.OnHealthChanged += HandleHealthChanged;
                _isSubscribed = true;
            }

            UpdateHealthBar(_player.Health, _player.MaxHealth);
            UpdatePortrait();
        }

        private void CacheHealthBarReferences()
        {
            if (_healthBar == null)
            {
                return;
            }

            _healthBarText ??= _healthBar.GetComponentInChildren<Text>(true);
            if (_portraitImage != null)
            {
                return;
            }

            Transform imageTransform = _healthBar.transform.Find("Image");
            if (imageTransform == null)
            {
                Debug.LogWarning("Объект с именем 'Image' не найден под полоской здоровья!", _healthBar);
                return;
            }

            _portraitImage = imageTransform.GetComponent<Image>();
            if (_portraitImage == null)
            {
                Debug.LogWarning("Компонент Image не найден на объекте 'Image' под полоской здоровья!", imageTransform);
            }
        }

        private void HandleHealthChanged(uint currentHealth, uint maxHealth)
        {
            UpdateHealthBar(currentHealth, maxHealth);
        }

        /// <summary>
        /// Обновляет значение полоски здоровья и текст.
        /// </summary>
        private void UpdateHealthBar(uint currentHealth, uint maxHealth)
        {
            if (_healthBar == null)
            {
                return;
            }

            _healthBar.value = maxHealth == 0 ? 0f : (float)currentHealth / maxHealth;
            if (_healthBarText != null)
            {
                _healthBarText.text = $"{currentHealth} / {maxHealth}";
            }
        }

        /// <summary>
        /// Проверяет и обновляет спрайт портрета в дочернем Image с именем "Image".
        /// </summary>
        private void UpdatePortrait()
        {
            if (_player == null || _healthBar == null || _portraitImage == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_player.Portrait))
            {
                Debug.LogWarning("player.Portrait пуст или null!", this);
                return;
            }

            Sprite portraitSprite = LoadPortraitSprite(_player.Portrait);
            if (portraitSprite == null)
            {
                Debug.LogWarning(
                    $"Не удалось загрузить портрет '{_player.Portrait}'. Проверены пути: {string.Join(", ", PortraitResourceFolders)}",
                    this);
                return;
            }

            if (_portraitImage.sprite != portraitSprite)
            {
                _portraitImage.sprite = portraitSprite;
            }
        }

        private static Sprite LoadPortraitSprite(string portraitName)
        {
            string normalizedPortraitName = NormalizePortraitName(portraitName);
            if (string.IsNullOrEmpty(normalizedPortraitName))
            {
                return null;
            }

            foreach (string resourceFolder in PortraitResourceFolders)
            {
                Sprite portraitSprite = Resources.Load<Sprite>($"{resourceFolder}/{normalizedPortraitName}");
                if (portraitSprite != null)
                {
                    return portraitSprite;
                }
            }

            return null;
        }

        private static string NormalizePortraitName(string portraitName)
        {
            if (string.IsNullOrWhiteSpace(portraitName))
            {
                return string.Empty;
            }

            string normalizedPortraitName = portraitName.Trim().Replace('\\', '/');
            int lastSlashIndex = normalizedPortraitName.LastIndexOf('/');
            if (lastSlashIndex >= 0 && lastSlashIndex < normalizedPortraitName.Length - 1)
            {
                normalizedPortraitName = normalizedPortraitName.Substring(lastSlashIndex + 1);
            }

            int extensionSeparatorIndex = normalizedPortraitName.LastIndexOf('.');
            if (extensionSeparatorIndex > 0)
            {
                normalizedPortraitName = normalizedPortraitName.Substring(0, extensionSeparatorIndex);
            }

            return normalizedPortraitName;
        }
    }
}
