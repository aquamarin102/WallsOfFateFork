using System;
using Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

namespace Game.UI
{
    public class QuestCompletionImageController : MonoBehaviour
    {
        [Header("Image")]
        [SerializeField] private Sprite _completionSprite;
        [SerializeField] private Vector2 _maxImageSize = new(1600f, 900f);

        [Header("Behavior")]
        [SerializeField, Range(0f, 1f)] private float _backgroundAlpha = 0.82f;
        [SerializeField] private bool _showOnStartIfAlreadyCompleted = true;
        [SerializeField] private bool _showOnlyOncePerSession = true;
        [SerializeField] private bool _closeOnClick = true;

        private static QuestCompletionImageController _instance;
        private QuestManager _questManager;
        private RuntimeQuestCompletionOverlay _activeOverlay;
        private bool _hasShownThisSession;

        [Inject]
        private void Construct(QuestManager questManager)
        {
            _questManager = questManager;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (!TryBindQuestManager())
            {
                Debug.LogWarning("QuestCompletionImageController could not resolve QuestManager on start.", this);
                return;
            }

            if (_showOnStartIfAlreadyCompleted && _questManager.AreAllQuestsCompleted())
            {
                TryShowCompletionImage();
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_instance == this)
            {
                _instance = null;
            }

            if (_questManager != null)
            {
                _questManager.AllQuestsCompletedStateChanged -= OnAllQuestsCompletedStateChanged;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            if (!TryBindQuestManager())
            {
                Debug.LogWarning($"QuestCompletionImageController could not resolve QuestManager in scene '{scene.name}'.", this);
                return;
            }

            if (_showOnStartIfAlreadyCompleted && _questManager.AreAllQuestsCompleted())
            {
                TryShowCompletionImage();
            }
        }

        private void OnAllQuestsCompletedStateChanged(bool areAllQuestsCompleted)
        {
            if (!areAllQuestsCompleted)
            {
                return;
            }

            TryShowCompletionImage();
        }

        private void TryShowCompletionImage()
        {
            if (_completionSprite == null)
            {
                Debug.LogWarning("Quest completion sprite is not assigned.", this);
                return;
            }

            if (_showOnlyOncePerSession && _hasShownThisSession)
            {
                return;
            }

            if (_activeOverlay != null)
            {
                return;
            }

            EnsureEventSystem();

            GameObject overlayObject = new("QuestCompletionOverlay");
            Canvas canvas = overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = overlayObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayObject.AddComponent<GraphicRaycaster>();

            _activeOverlay = overlayObject.AddComponent<RuntimeQuestCompletionOverlay>();
            _activeOverlay.Initialize(
                _completionSprite,
                _maxImageSize,
                _backgroundAlpha,
                _closeOnClick,
                HandleOverlayDestroyed);

            _hasShownThisSession = true;
        }

        private void HandleOverlayDestroyed(RuntimeQuestCompletionOverlay overlay)
        {
            if (_activeOverlay == overlay)
            {
                _activeOverlay = null;
            }
        }

        private bool TryBindQuestManager()
        {
            QuestManager currentQuestManager = QuestManager.Instance ?? _questManager;
            if (currentQuestManager == null)
            {
                return false;
            }

            if (ReferenceEquals(currentQuestManager, _questManager))
            {
                return true;
            }

            if (_questManager != null)
            {
                _questManager.AllQuestsCompletedStateChanged -= OnAllQuestsCompletedStateChanged;
            }

            _questManager = currentQuestManager;
            _questManager.AllQuestsCompletedStateChanged += OnAllQuestsCompletedStateChanged;
            return true;
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
            {
                return;
            }

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    internal sealed class RuntimeQuestCompletionOverlay : MonoBehaviour
    {
        private Action<RuntimeQuestCompletionOverlay> _onDestroyed;
        private bool _isClosed;

        public void Initialize(
            Sprite completionSprite,
            Vector2 maxImageSize,
            float backgroundAlpha,
            bool closeOnClick,
            Action<RuntimeQuestCompletionOverlay> onDestroyed)
        {
            _onDestroyed = onDestroyed;
            BuildOverlay(completionSprite, maxImageSize, backgroundAlpha, closeOnClick);
        }

        private void BuildOverlay(
            Sprite completionSprite,
            Vector2 maxImageSize,
            float backgroundAlpha,
            bool closeOnClick)
        {
            GameObject backgroundObject = new("Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(transform, false);

            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(backgroundAlpha));

            if (closeOnClick)
            {
                Button closeButton = backgroundObject.AddComponent<Button>();
                closeButton.transition = Selectable.Transition.None;
                closeButton.onClick.AddListener(Close);
            }
            else
            {
                backgroundImage.raycastTarget = false;
            }

            GameObject imageObject = new("CompletionImage", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(backgroundObject.transform, false);

            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            imageRect.pivot = new Vector2(0.5f, 0.5f);
            imageRect.anchoredPosition = Vector2.zero;

            Image completionImage = imageObject.GetComponent<Image>();
            completionImage.sprite = completionSprite;
            completionImage.preserveAspect = true;
            completionImage.raycastTarget = false;

            Vector2 safeMaxImageSize = new(Mathf.Max(1f, maxImageSize.x), Mathf.Max(1f, maxImageSize.y));
            Vector2 spriteSize = new(completionSprite.rect.width, completionSprite.rect.height);
            float scale = Mathf.Min(
                safeMaxImageSize.x / spriteSize.x,
                safeMaxImageSize.y / spriteSize.y,
                1f);

            imageRect.sizeDelta = spriteSize * scale;
        }

        private void Close()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _onDestroyed?.Invoke(this);
        }
    }
}
