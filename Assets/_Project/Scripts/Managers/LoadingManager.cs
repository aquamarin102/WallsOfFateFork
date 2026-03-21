using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public class LoadingManager
    {
        public static LoadingManager Instance { get; private set; }

        public bool StartupIntroShown { get; set; }

        public event Action LoadingStarted;
        public event Action LoadingFinished;
        public event Action<float> LoadingProgressUpdated;
        public event Action WaitingForInputStarted;

        private readonly string loadingSceneName = "LoadingScreen";
        private LoadingRoutineRunner runner;
        private Coroutine activeLoadCoroutine;

        public bool IsLoading { get; private set; }

        public LoadingManager()
        {
            Instance = this;
        }

        public void LoadSceneAsync(string sceneName)
        {
            LoadScene(sceneName);
        }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("LoadingManager: scene name is null or empty.");
                return;
            }

            if (sceneName == loadingSceneName)
            {
                LoadSceneDirect(sceneName);
                return;
            }

            if (IsLoading)
            {
                Debug.LogWarning($"LoadingManager: scene load '{sceneName}' ignored because another load is already in progress.");
                return;
            }

            EnsureRunner();

            IsLoading = true;
            activeLoadCoroutine = runner.StartCoroutine(LoadSceneRoutine(sceneName));
        }

        public void LoadSceneDirect(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogError("LoadingManager: scene name is null or empty.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        private void EnsureRunner()
        {
            if (runner != null)
            {
                return;
            }

            var runnerObject = new GameObject("[LoadingManagerRunner]");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<LoadingRoutineRunner>();
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            var loadingSceneOperation = SceneManager.LoadSceneAsync(loadingSceneName);
            if (loadingSceneOperation == null)
            {
                Debug.LogError($"LoadingManager: failed to load loading scene '{loadingSceneName}'.");
                IsLoading = false;
                activeLoadCoroutine = null;
                LoadSceneDirect(sceneName);
                yield break;
            }

            yield return loadingSceneOperation;

            // Даём сцене загрузки один кадр на инициализацию и отображение.
            yield return null;

            var loadingScene = SceneManager.GetSceneByName(loadingSceneName);
            if (loadingScene.IsValid())
            {
                SceneManager.SetActiveScene(loadingScene);
            }

            var controller = loadingScene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<LoadingScreenController>(true))
                .FirstOrDefault();

            if (controller == null)
            {
                Debug.LogError("LoadingManager: LoadingScreenController not found in LoadingScreen scene.");
                IsLoading = false;
                activeLoadCoroutine = null;
                LoadSceneDirect(sceneName);
                yield break;
            }

            bool waitingForInputEnded = false;

            void OnLoadingProgressUpdated(float progress)
            {
                LoadingProgressUpdated?.Invoke(progress);
            }

            void OnWaitingForInputStarted()
            {
                WaitingForInputStarted?.Invoke();
            }

            void OnWaitingForInputEnded()
            {
                waitingForInputEnded = true;
            }

            controller.LoadingProgressUpdated += OnLoadingProgressUpdated;
            controller.WaitingForInputStarted += OnWaitingForInputStarted;
            controller.WaitingForInputEnded += OnWaitingForInputEnded;

            LoadingStarted?.Invoke();

            var targetSceneOperation = SceneManager.LoadSceneAsync(sceneName);
            if (targetSceneOperation == null)
            {
                controller.LoadingProgressUpdated -= OnLoadingProgressUpdated;
                controller.WaitingForInputStarted -= OnWaitingForInputStarted;
                controller.WaitingForInputEnded -= OnWaitingForInputEnded;

                Debug.LogError($"LoadingManager: failed to start loading scene '{sceneName}'.");
                IsLoading = false;
                activeLoadCoroutine = null;
                LoadSceneDirect(sceneName);
                yield break;
            }

            targetSceneOperation.allowSceneActivation = false;
            controller.ActivateLoading(targetSceneOperation);

            while (!waitingForInputEnded)
            {
                yield return null;
            }

            targetSceneOperation.allowSceneActivation = true;

            while (!targetSceneOperation.isDone)
            {
                yield return null;
            }

            var targetScene = SceneManager.GetSceneByName(sceneName);
            if (targetScene.IsValid())
            {
                SceneManager.SetActiveScene(targetScene);
            }

            controller.LoadingProgressUpdated -= OnLoadingProgressUpdated;
            controller.WaitingForInputStarted -= OnWaitingForInputStarted;
            controller.WaitingForInputEnded -= OnWaitingForInputEnded;

            IsLoading = false;
            activeLoadCoroutine = null;
            LoadingFinished?.Invoke();
        }

        private class LoadingRoutineRunner : MonoBehaviour
        {
            private void Awake()
            {
                hideFlags = HideFlags.HideInHierarchy;
            }
        }
    }
}
