using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public class RouteMiniGameHUD : MonoBehaviour
    {
        [Serializable]
        private sealed class TextBinding
        {
            [SerializeField] private Text text;
            [SerializeField] private TMP_Text tmpText;

            public bool HasTarget => text != null || tmpText != null;

            public void SetText(string value)
            {
                if (text != null)
                {
                    text.text = value;
                }

                if (tmpText != null)
                {
                    tmpText.text = value;
                }
            }

            public void SetColor(Color color)
            {
                if (text != null)
                {
                    text.color = color;
                }

                if (tmpText != null)
                {
                    tmpText.color = color;
                }
            }
        }

        [Header("Scene HUD")]
        [SerializeField] private TextBinding movesCounterText = new();
        [SerializeField] private TextBinding argumentsCounterText = new();
        [SerializeField] private TextBinding undoHintText = new();
        [SerializeField] private Slider candleSlider;
        [SerializeField] private Graphic candleFillGraphic;

        [Header("Colors")]
        [SerializeField] private Color defaultTextColor = Color.white;
        [SerializeField] private Color accentTextColor = new(0.58f, 0.95f, 0.62f, 1f);
        [SerializeField] private Color mutedTextColor = new(0.72f, 0.72f, 0.72f, 0.92f);
        [SerializeField] private Color warningTextColor = new(0.98f, 0.48f, 0.36f, 1f);
        [SerializeField] private Color candleNormalColor = new(1f, 0.82f, 0.36f, 1f);

        private CommandQueue _queue;
        private ExecutionManager _executor;

        public bool HasSceneBindings =>
            movesCounterText.HasTarget ||
            argumentsCounterText.HasTarget ||
            undoHintText.HasTarget ||
            candleSlider != null ||
            candleFillGraphic != null;

        public void Initialize(CommandQueue queue, ExecutionManager executor)
        {
            UnbindState();

            _queue = queue;
            _executor = executor;

            BindState();
            Refresh();
        }

        private void Update()
        {
            if (_executor == null)
            {
                return;
            }

            RefreshCandle();
        }

        private void OnDestroy()
        {
            UnbindState();
        }

        private void BindState()
        {
            if (_queue != null)
            {
                _queue.Changed -= Refresh;
                _queue.Changed += Refresh;
            }

            if (_executor != null)
            {
                _executor.StateChanged -= Refresh;
                _executor.StateChanged += Refresh;
            }
        }

        private void UnbindState()
        {
            if (_queue != null)
            {
                _queue.Changed -= Refresh;
            }

            if (_executor != null)
            {
                _executor.StateChanged -= Refresh;
            }
        }

        private void Refresh()
        {
            if (_queue == null || _executor == null)
            {
                return;
            }

            int commandsUsed = _queue.Commands.Count;
            int maxCommands = _queue.EffectiveMaxCommands;
            int collectedArguments = _executor.CollectedArguments;
            int totalArguments = Mathf.Max(_executor.TotalArguments, 0);
            bool canUndo =
                !_executor.IsRunning &&
                !_executor.IsResolved &&
                _executor.RemainingCandleSeconds > 0f &&
                commandsUsed > 0;

            movesCounterText.SetText($"Ходы: {commandsUsed}/{maxCommands}");
            movesCounterText.SetColor(commandsUsed >= maxCommands ? warningTextColor : defaultTextColor);

            argumentsCounterText.SetText($"Доводы: {collectedArguments}/{totalArguments}");
            argumentsCounterText.SetColor(
                totalArguments > 0 && collectedArguments >= totalArguments
                    ? accentTextColor
                    : defaultTextColor);

            undoHintText.SetText("R - отмена");
            undoHintText.SetColor(canUndo ? accentTextColor : mutedTextColor);

            RefreshCandle();
        }

        private void RefreshCandle()
        {
            float candleNormalized = _executor.CandleNormalized;

            if (candleSlider != null)
            {
                candleSlider.minValue = 0f;
                candleSlider.maxValue = 1f;
                candleSlider.wholeNumbers = false;
                candleSlider.SetValueWithoutNotify(candleNormalized);
            }

            if (candleFillGraphic != null)
            {
                candleFillGraphic.color = candleNormalized > 0.2f ? candleNormalColor : warningTextColor;
            }
        }
    }
}
