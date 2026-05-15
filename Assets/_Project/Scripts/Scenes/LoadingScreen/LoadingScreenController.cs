using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game
{
    public class LoadingScreenController : MonoBehaviour
    {
        private static bool suppressNextGameplayPointerSequence;

        public event Action<float> LoadingProgressUpdated;
        public event Action WaitingForInputStarted;
        public event Action WaitingForInputEnded;

        private Coroutine _fadeCoroutine;

        public float inputDelay = 0.05f;      // пауза перед тем, как выводим кнопку Continue
        public float freshInputGuardDelay = 0.1f;

        public void ActivateLoading(AsyncOperation operation)
        {
            AudioManager.Instance.ActivateLoadingSnapshot();
            AudioManager.Instance.PlayLoadingMusic();

            StartCoroutine(LoadingWait(operation));
        }

        private IEnumerator LoadingWait(AsyncOperation operation)
        {
            // Отслеживаем прогресс загрузки
            while (!operation.isDone)
            {
                LoadingProgressUpdated?.Invoke(operation.progress);

                if (operation.progress >= 0.9f)
                {
                    yield return new WaitForSecondsRealtime(inputDelay);

                    // Уведомляем о начале ожидания ввода
                    WaitingForInputStarted?.Invoke();

                    yield return StartCoroutine(WaitForUserInput(operation));
                    yield break;
                }
                yield return null;
            }
        }

        private IEnumerator WaitForUserInput(AsyncOperation op)
        {
            // Пропускаем кадр, в котором был инициирован переход, чтобы не съесть тот же ввод.
            yield return null;

            if (freshInputGuardDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(freshInputGuardDelay);
            }

            // Ждём, пока пользователь отпустит кнопку/мышь, которой открыл экран загрузки.
            while (Input.anyKey)
            {
                yield return null;
            }

            while (!Input.anyKeyDown)
                yield return null;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            SuppressGameplayPointerInputIfNeeded();
            WaitingForInputEnded?.Invoke();
        }

        public static bool ConsumeGameplayPointerSuppression()
        {
            if (!suppressNextGameplayPointerSequence)
                return false;

            if (IsPrimaryPointerPressed())
                return true;

            suppressNextGameplayPointerSequence = false;
            return true;
        }

        private static void SuppressGameplayPointerInputIfNeeded()
        {
            if (IsPrimaryPointerPressed())
                suppressNextGameplayPointerSequence = true;
        }

        private static bool IsPrimaryPointerPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                return mouse.leftButton.isPressed;

            Pointer pointer = Pointer.current;
            if (pointer != null)
                return pointer.press.isPressed;

            return Input.GetMouseButton(0);
        }
    }
}
