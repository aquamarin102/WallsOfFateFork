using System;
using System.Collections;
using UnityEngine;

namespace Game
{
    public class LoadingScreenController : MonoBehaviour
    {
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

            while (!Input.anyKeyDown) yield return null;

            if (_fadeCoroutine != null)
                StopCoroutine(_fadeCoroutine);

            WaitingForInputEnded?.Invoke();
        }
    }

}

