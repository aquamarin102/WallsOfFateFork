using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zenject;

namespace Game
{
    public class InputController : MonoBehaviour
    {
        public GameObject firstButton;
        // Интервал, в течение которого повторные нажатия Enter/Space/E игнорируются
        public float submitCooldown = 1f;

        private float lastSubmitTime = -Mathf.Infinity;
        private bool canAcceptInput = true;
        private float blockInputUntil = 0f;

        private LoadingManager loadingManager;
        private bool subscribedToLoadingEvents;

        [Inject]
        private void Construct([InjectOptional] LoadingManager loadingManager)
        {
            this.loadingManager = loadingManager;
        }

        private void OnEnable()
        {
            TrySubscribeToLoadingEvents();
        }

        private void OnDisable()
        {
            UnsubscribeFromLoadingEvents();
        }

        private void OnLoadingStarted()
        {
            canAcceptInput = false;
        }

        private void OnLoadingFinished()
        {
            StartCoroutine(EnableInputWithDelay(1f));
        }

        private IEnumerator EnableInputWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            canAcceptInput = true;
        }

        public void BlockInputFor(float seconds)
        {
            blockInputUntil = Time.unscaledTime + seconds;
        }

        private void Update()
        {
            TrySubscribeToLoadingEvents();

            // Блокируем ввод, пока идёт загрузка или не истёк начальный delay
            if (!canAcceptInput ||
                (loadingManager != null && loadingManager.IsLoading))
                return;

            if (Time.unscaledTime < blockInputUntil)
                return;

            EventSystem currentEventSystem = EventSystem.current;

            // Обнаружение мыши
            if (Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0)
            {
                if (InputModeTracker.UsingKeyboard)
                {
                    InputModeTracker.NotifyMouseInput();
                    if (currentEventSystem != null)
                    {
                        currentEventSystem.SetSelectedGameObject(null);
                    }
                }
            }

            // Обнаружение клавиатуры (стрелки или W/S)
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
            {
                if (!InputModeTracker.UsingKeyboard)
                    InputModeTracker.NotifyKeyboardInput();

                ClearMouseHoverEffect();

                if (currentEventSystem != null && currentEventSystem.currentSelectedGameObject == null)
                    SetSelected(firstButton);
            }

            // Enter, Space или E — с задержкой 1 секунда между нажатиями
            if (Input.GetKeyUp(KeyCode.Return)
                || Input.GetKeyUp(KeyCode.E))
            {
                if (Time.unscaledTime - lastSubmitTime < submitCooldown)
                    return;

                lastSubmitTime = Time.unscaledTime;

                var selected = currentEventSystem != null ? currentEventSystem.currentSelectedGameObject : null;
                if (selected != null)
                {
                    var btn = selected.GetComponent<Button>();
                    if (btn != null)
                        btn.onClick.Invoke();
                }
            }
        }

        private void ClearMouseHoverEffect()
        {
            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem == null)
            {
                return;
            }

            var pointerData = new PointerEventData(currentEventSystem)
            {
                position = Input.mousePosition
            };

            var results = new List<RaycastResult>();
            currentEventSystem.RaycastAll(pointerData, results);

            foreach (var res in results)
            {
                var effect = res.gameObject.GetComponent<UIButtonEffects>();
                if (effect != null)
                    effect.ForceExit();
            }
        }

        private void SetSelected(GameObject go)
        {
            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem == null)
            {
                return;
            }

            currentEventSystem.SetSelectedGameObject(null);
            currentEventSystem.SetSelectedGameObject(go);
        }

        private bool TrySubscribeToLoadingEvents()
        {
            if (subscribedToLoadingEvents)
            {
                return true;
            }

            loadingManager ??= LoadingManager.Instance;
            if (loadingManager == null)
            {
                return false;
            }

            loadingManager.LoadingStarted += OnLoadingStarted;
            loadingManager.LoadingFinished += OnLoadingFinished;
            subscribedToLoadingEvents = true;
            return true;
        }

        private void UnsubscribeFromLoadingEvents()
        {
            if (!subscribedToLoadingEvents || loadingManager == null)
            {
                return;
            }

            loadingManager.LoadingStarted -= OnLoadingStarted;
            loadingManager.LoadingFinished -= OnLoadingFinished;
            subscribedToLoadingEvents = false;
        }
    }

}
