using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    [Serializable]
    public sealed class TutorialProgressData
    {
        public List<string> shownTutorialKeys = new();
    }

    internal static class TutorialSheetDefinitions
    {
        public const string MainRoomKey = "tutorial.mainroom";
        public const string MainRoomResourcePath = "Tutorials/MainRoomSheet";
        public const string MainRoomEditorAssetPath = "Assets/_Project/Art/UI/\u043c\u044b\u0448\u044c.png";

        public const string StrengthKey = "tutorial.minigame.strength";
        public const string StrengthResourcePath = "Tutorials/MiniGameStrengthSheet";
        public const string StrengthEditorAssetPath = "Assets/_Project/Art/UI/Tutorials/mini game 1/sheet.png";

        public const string AgilityKey = "tutorial.minigame.agility";
        public const string AgilityResourcePath = "Tutorials/MiniGameAgilitySheet";
        public const string AgilityEditorAssetPath = "Assets/_Project/Art/UI/Tutorials/mini game 2/sheet.png";

        public const string IntellectKey = "tutorial.minigame.intellect";
        public const string IntellectResourcePath = "Tutorials/MiniGameIntellectSheet";
        public const string IntellectEditorAssetPath = "Assets/_Project/Art/UI/Tutorials/mini game 3/sheet.png";

        public const string StorageKey = "tutorial.storage";
        public const string StorageResourcePath = "Tutorials/StorageSheet";
        public const string StorageEditorAssetPath = "Assets/Resources/Tutorials/StorageSheet.png";
    }

    public static class TutorialSheetService
    {
        private const string ProgressRepositoryKey = "TutorialProgressData";

        private static TutorialProgressData _progressData;
        private static bool _isProgressLoaded;
        private static RuntimeTutorialOverlay _activeOverlay;

        public static bool TryShowOnce(string tutorialKey, string resourcePath, string editorAssetPath, Action onClosed)
        {
            if (string.IsNullOrWhiteSpace(tutorialKey))
                return false;

            if (HasSeen(tutorialKey))
                return false;

            Sprite tutorialSprite = LoadTutorialSprite(resourcePath, editorAssetPath);
            if (tutorialSprite == null)
            {
                Debug.LogWarning($"Tutorial sprite was not found for key '{tutorialKey}'.");
                return false;
            }

            ShowOverlay(
                tutorialSprite,
                () =>
                {
                    MarkSeen(tutorialKey);
                    onClosed?.Invoke();
                });

            return true;
        }

        private static void ShowOverlay(Sprite tutorialSprite, Action onClosed)
        {
            if (_activeOverlay != null)
            {
                UnityEngine.Object.Destroy(_activeOverlay.gameObject);
                _activeOverlay = null;
            }

            EnsureEventSystem();

            GameObject overlayObject = new("TutorialOverlay");
            Canvas canvas = overlayObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = overlayObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            overlayObject.AddComponent<GraphicRaycaster>();

            _activeOverlay = overlayObject.AddComponent<RuntimeTutorialOverlay>();
            _activeOverlay.Initialize(tutorialSprite, onClosed, HandleOverlayDestroyed);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static void HandleOverlayDestroyed(RuntimeTutorialOverlay overlay)
        {
            if (_activeOverlay == overlay)
                _activeOverlay = null;
        }

        private static bool HasSeen(string tutorialKey)
        {
            EnsureProgressLoaded();
            return _progressData.shownTutorialKeys.Contains(tutorialKey);
        }

        private static void MarkSeen(string tutorialKey)
        {
            EnsureProgressLoaded();

            if (_progressData.shownTutorialKeys.Contains(tutorialKey))
                return;

            _progressData.shownTutorialKeys.Add(tutorialKey);
            Repository.SetData(ProgressRepositoryKey, _progressData);
            Repository.SaveState();
        }

        private static void EnsureProgressLoaded()
        {
            if (_isProgressLoaded)
                return;

            Repository.LoadState();

            if (!Repository.TryGetData(ProgressRepositoryKey, out _progressData) || _progressData == null)
                _progressData = new TutorialProgressData();

            _isProgressLoaded = true;
        }

        private static Sprite LoadTutorialSprite(string resourcePath, string editorAssetPath)
        {
            if (!string.IsNullOrWhiteSpace(resourcePath))
            {
                Sprite resourceSprite = Resources.Load<Sprite>(resourcePath);
                if (resourceSprite != null)
                    return resourceSprite;

                Texture2D resourceTexture = Resources.Load<Texture2D>(resourcePath);
                if (resourceTexture != null)
                    return CreateSpriteFromTexture(resourceTexture);
            }

#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(editorAssetPath))
            {
                Sprite editorSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(editorAssetPath);
                if (editorSprite != null)
                    return editorSprite;

                Texture2D editorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(editorAssetPath);
                if (editorTexture != null)
                    return CreateSpriteFromTexture(editorTexture);
            }
#endif

            return null;
        }

        private static Sprite CreateSpriteFromTexture(Texture2D texture)
        {
            if (texture == null)
                return null;

            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }

    internal sealed class RuntimeTutorialOverlay : MonoBehaviour
    {
        private const float BackgroundAlpha = 0.82f;
        private static readonly Vector2 MaxSheetSize = new(1600f, 920f);

        private float _previousTimeScale = 1f;
        private Action _onClosed;
        private Action<RuntimeTutorialOverlay> _onDestroyed;
        private bool _isClosed;

        public void Initialize(Sprite tutorialSprite, Action onClosed, Action<RuntimeTutorialOverlay> onDestroyed)
        {
            _onClosed = onClosed;
            _onDestroyed = onDestroyed;
            _previousTimeScale = Time.timeScale;

            BuildOverlay(tutorialSprite);
            Time.timeScale = 0f;
        }

        private void BuildOverlay(Sprite tutorialSprite)
        {
            GameObject clickCatcherObject = new("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
            clickCatcherObject.transform.SetParent(transform, false);

            RectTransform catcherRect = clickCatcherObject.GetComponent<RectTransform>();
            catcherRect.anchorMin = Vector2.zero;
            catcherRect.anchorMax = Vector2.one;
            catcherRect.offsetMin = Vector2.zero;
            catcherRect.offsetMax = Vector2.zero;

            Image catcherImage = clickCatcherObject.GetComponent<Image>();
            catcherImage.color = new Color(0f, 0f, 0f, BackgroundAlpha);

            Button button = clickCatcherObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(Close);

            GameObject sheetObject = new("Sheet", typeof(RectTransform), typeof(Image));
            sheetObject.transform.SetParent(clickCatcherObject.transform, false);

            RectTransform sheetRect = sheetObject.GetComponent<RectTransform>();
            sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
            sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
            sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.anchoredPosition = Vector2.zero;

            Image sheetImage = sheetObject.GetComponent<Image>();
            sheetImage.sprite = tutorialSprite;
            sheetImage.preserveAspect = true;
            sheetImage.raycastTarget = false;

            Vector2 spriteSize = new(tutorialSprite.rect.width, tutorialSprite.rect.height);
            float scale = Mathf.Min(MaxSheetSize.x / spriteSize.x, MaxSheetSize.y / spriteSize.y, 1f);
            sheetRect.sizeDelta = spriteSize * scale;
        }

        private void Close()
        {
            if (_isClosed)
                return;

            _isClosed = true;
            Time.timeScale = _previousTimeScale;
            _onClosed?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (!_isClosed)
                Time.timeScale = _previousTimeScale;

            _onDestroyed?.Invoke(this);
        }
    }
}
