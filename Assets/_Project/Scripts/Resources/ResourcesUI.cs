using Game.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using TMPro;
using UnityEngine;
using Zenject;

namespace Game
{
    public class ResourcesUI : MonoBehaviour
    {
        #region Inspector
        [Header("Text References")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text foodText;
        [SerializeField] private TMP_Text satisfactionText;
        [SerializeField] private TMP_Text strengthText;

        [Header("Animation Settings")]
        [Tooltip("Time (seconds) to fade from white to green/red")]
        [SerializeField] private float colourFadeDuration = 0.25f;
        [Tooltip("Time (seconds) to fade back from green/red to white")]
        [SerializeField] private float colourReturnDuration = 0.4f;
        [Tooltip("Delay (seconds) between each +1 / -1 increment")]
        [SerializeField] private float valueStepDelay = 0.15f;
        [SerializeField] private Color increaseColour = Color.green;
        [SerializeField] private Color decreaseColour = Color.red;
        [SerializeField] private Color normalColour = Color.white;
        #endregion

        #region Private fields
        
        private Dictionary<ResourceType, int> lastResourceValue = new() {
            { ResourceType.Gold, 0 },
            { ResourceType.Food, 0 },
            { ResourceType.PeopleSatisfaction, 0 },
            { ResourceType.CastleStrength, 0 }
        };

        private Dictionary<ResourceType, TMP_Text> resourceTextFields;

        // Stores the currently‑running coroutine for every TMP_Text so that it can be safely stopped when a new change arrives.
        private readonly Dictionary<TMP_Text, Coroutine> runningCoroutines = new();
        #endregion

        private PlayerManager playerManager;
        private bool subscribedToPlayerChanges;

        [Inject]
        private void Construct([InjectOptional] PlayerManager playerManager) {
            this.playerManager = playerManager;
        }

        private void Awake() {
            EnsureTextFieldsInitialized();
            TrySubscribeToPlayerChanges();
        }

        private void OnEnable()
        {
            EnsureTextFieldsInitialized();
            if (TrySubscribeToPlayerChanges())
            {
                UpdateAllResources(forceUpdate: true);
            }
        }

        private void Update()
        {
            if (subscribedToPlayerChanges)
            {
                return;
            }

            EnsureTextFieldsInitialized();
            if (TrySubscribeToPlayerChanges())
            {
                UpdateAllResources(forceUpdate: true);
            }
        }

        private void OnDisable() {
            UnsubscribeFromPlayerChanges();
        }

        private void OnDestroy() {
            UnsubscribeFromPlayerChanges();
        }

        private void EnsureTextFieldsInitialized()
        {
            if (resourceTextFields != null)
            {
                return;
            }

            resourceTextFields = new Dictionary<ResourceType, TMP_Text> {
                { ResourceType.Gold, goldText },
                { ResourceType.Food, foodText },
                { ResourceType.PeopleSatisfaction, satisfactionText },
                { ResourceType.CastleStrength, strengthText }
            };
        }

        #region Unity life-cycle
        private void Start()
        {
            if (TrySubscribeToPlayerChanges())
            {
                UpdateAllResources(forceUpdate: true);
            }
        }
        #endregion

        #region Event handlers
        private void OnResourceChanged(ResourceType resource, int value) {
            UpdateResource(resource, value, true);
        }
        #endregion

        #region Updating helpers
        private void UpdateAllResources(bool forceUpdate = false)
        {
            if (!TryResolvePlayerManager() || resourceTextFields == null)
            {
                return;
            }

            var resourceTypes = Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>();

            foreach (var type in resourceTypes)
            {
                UpdateResource(type, playerManager.PlayerData.GetResource(type), forceUpdate);
            }
        }

        private void UpdateResource(ResourceType resource, int currentValue, bool forceUpdate)
        {
            if (resourceTextFields == null || !resourceTextFields.TryGetValue(resource, out TMP_Text textField))
            {
                return;
            }

            var lastValue = lastResourceValue[resource];

            if (textField == null) return;

            // First frame or hard refresh – just set value without animation.
            if (forceUpdate)
            {
                textField.text = currentValue.ToString();
                textField.color = normalColour;
                lastResourceValue[resource] = currentValue;
                return;
            }

            if (lastValue == currentValue) return;

            // A change detected – cancel previous animation (if any) and start a new one
            if (runningCoroutines.TryGetValue(textField, out var previous) && previous != null)
                StopCoroutine(previous);

            runningCoroutines[textField] = StartCoroutine(AnimateResourceChange(textField, lastValue, currentValue));
            lastResourceValue[resource] = currentValue;
        }
        #endregion

        #region Animation coroutine
        private IEnumerator AnimateResourceChange(TMP_Text textField, int fromValue, int toValue)
        {
            int step = toValue > fromValue ? 1 : -1;
            Color targetTint = toValue > fromValue ? increaseColour : decreaseColour;

            // ▸ 1. Fade from white → green/red
            for (float t = 0f; t < colourFadeDuration; t += Time.unscaledDeltaTime)
            {
                textField.color = Color.Lerp(normalColour, targetTint, t / colourFadeDuration);
                yield return null;
            }
            textField.color = targetTint;

            // ▸ 2. Quickly tick value ±1 until we reach the target
            int shown = fromValue;
            while (shown != toValue)
            {
                shown += step;
                textField.text = shown.ToString();
                yield return new WaitForSecondsRealtime(valueStepDelay);
            }

            // ▸ 3. Fade back to white
            for (float t = 0f; t < colourReturnDuration; t += Time.unscaledDeltaTime)
            {
                textField.color = Color.Lerp(targetTint, normalColour, t / colourReturnDuration);
                yield return null;
            }
            textField.color = normalColour;

            // Mark coroutine as finished so future changes can start cleanly
            runningCoroutines[textField] = null;
        }
        #endregion

        private bool TryResolvePlayerManager()
        {
            playerManager ??= PlayerManager.Instance;
            return playerManager?.PlayerData != null;
        }

        private bool TrySubscribeToPlayerChanges()
        {
            if (subscribedToPlayerChanges)
            {
                return true;
            }

            if (!TryResolvePlayerManager())
            {
                return false;
            }

            playerManager.PlayerData.ResourceChanged += OnResourceChanged;
            subscribedToPlayerChanges = true;
            return true;
        }

        private void UnsubscribeFromPlayerChanges()
        {
            if (!subscribedToPlayerChanges || playerManager?.PlayerData == null)
            {
                return;
            }

            playerManager.PlayerData.ResourceChanged -= OnResourceChanged;
            subscribedToPlayerChanges = false;
        }

        #region Editor‑only validation
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!goldText) //Debug.LogWarning("Gold Text not assigned!", this);
            if (!foodText) //Debug.LogWarning("Food Text not assigned!", this);
            if (!satisfactionText) //Debug.LogWarning("Satisfaction Text not assigned!", this);
            if (!strengthText) //Debug.LogWarning("Strength Text not assigned!", this);

            colourFadeDuration = Mathf.Max(0.01f, colourFadeDuration);
            colourReturnDuration = Mathf.Max(0.01f, colourReturnDuration);
            valueStepDelay = Mathf.Max(0.001f, valueStepDelay);
        }
#endif
        #endregion
    }
}
