using System;
using System.Collections;
using Game.MiniGame;
using Game.MiniGame.PowerCheck;
using Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game
{
    public class ExecutionManager : MonoBehaviour, IMiniGameInstaller
    {
        public PlayerController player;
        public CommandQueue queue;

        [Header("Scene References")]
        [SerializeField] private GridManager grid;
        [SerializeField] private MiniGameInputHandler inputHandler;
        [SerializeField] private EndDayScreenManager endGameScreenManager;

        [Header("Game Rules")]
        [SerializeField] private int maxAttempts = 3;
        [SerializeField] private float betweenCommandsDelay = 0.12f;
        [SerializeField] private float postRunPause = 0.35f;
        [SerializeField] private bool requireExitToWin;
        [SerializeField] private RouteDirection startingDirection = RouteDirection.Up;

        public event Action StateChanged;
        public event Action<bool> OnEndGame;

        public bool IsRunning { get; private set; }
        public bool IsResolved { get; private set; }
        public int AttemptsRemaining { get; private set; }
        public int MaxAttempts => maxAttempts;
        public string StatusMessage { get; private set; }
        public GridManager Grid => grid;
        public int LastFailedCommandIndex => _lastFailedCommandIndex;

        private MiniGameData _gameData;
        private bool _initialized;
        private int _lastFailedCommandIndex = -1;
        private bool _hasBootstrappedScene;
        private bool _isTutorialPending;
        private bool _useEndGameScreen;

        public void InitializeWithData(MiniGameData gameData)
        {
            _gameData = gameData;
            BeginSceneBootstrap();
        }

        public void OnMiniGameEnded(bool playerWin)
        {
            UnbindEndGameScreen();

            if (MinigameManager.Instance != null)
            {
                MinigameManager.Instance.EndMinigame(playerWin);
            }
        }

        public bool TryStartRun()
        {
            if (IsRunning)
            {
                return false;
            }

            if (AttemptsRemaining <= 0)
            {
                SetStatus("Попытки закончились. Нажмите Reset, чтобы начать заново.", true);
                return false;
            }

            if (!queue.ValidateForRun(out string reason))
            {
                SetStatus(reason, true);
                return false;
            }

            StartCoroutine(RunRoutine());
            return true;
        }

        public void ResetSession(bool restoreAttempts, bool clearQueue)
        {
            if (IsRunning)
            {
                return;
            }

            InitializeSession();

            if (restoreAttempts)
            {
                AttemptsRemaining = maxAttempts;
                IsResolved = false;
            }

            if (clearQueue)
            {
                queue.Clear();
            }

            grid.ResetBoardState();
            player.ResetToStart();
            SetStatus("Маршрут сброшен. Составьте новый прогон.", false);
        }

        public void SetStatus(string message, bool isWarning)
        {
            StatusMessage = message;
            StateChanged?.Invoke();

            if (isWarning)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }

        public void ClearFailureMarkers(bool notify = true)
        {
            if (_lastFailedCommandIndex < 0)
            {
                return;
            }

            _lastFailedCommandIndex = -1;
            if (notify)
            {
                StateChanged?.Invoke();
            }
        }

        public void TrimFailureMarkersToCommandCount(int commandCount, bool notify = true)
        {
            if (_lastFailedCommandIndex < 0)
            {
                return;
            }

            if (commandCount > _lastFailedCommandIndex)
            {
                return;
            }

            ClearFailureMarkers(notify);
        }

        private void Awake()
        {
            EnsureReferences();
            BindEndGameScreen();
        }

        private void Start()
        {
            BeginSceneBootstrap();
        }

        private void BeginSceneBootstrap()
        {
            if (_hasBootstrappedScene || _isTutorialPending)
            {
                return;
            }

            if (TutorialSheetService.TryShowOnce(
                TutorialSheetDefinitions.IntellectKey,
                TutorialSheetDefinitions.IntellectResourcePath,
                TutorialSheetDefinitions.IntellectEditorAssetPath,
                CompleteSceneBootstrap))
            {
                _isTutorialPending = true;
                return;
            }

            CompleteSceneBootstrap();
        }

        private void CompleteSceneBootstrap()
        {
            if (_hasBootstrappedScene)
            {
                return;
            }

            _isTutorialPending = false;
            _hasBootstrappedScene = true;

            EnsureReferences();
            BindEndGameScreen();
            ApplyGameData();
            InitializeSession();
        }

        private IEnumerator RunRoutine()
        {
            IsRunning = true;
            ClearFailureMarkers(false);
            SetStatus("Прогон запущен. Магнат выполняет маршрут...", false);

            grid.ResetBoardState();
            player.ResetToStart();

            if (!ValidateStartCell(out string startFailure))
            {
                yield return StartCoroutine(HandleFailure(startFailure));
                yield break;
            }

            for (int commandIndex = 0; commandIndex < queue.Commands.Count; commandIndex++)
            {
                RouteCommand command = queue.Commands[commandIndex];

                if (RouteDirectionUtility.TryGetStepDirection(command.Type, out RouteDirection stepDirection))
                {
                    for (int step = 0; step < command.Value; step++)
                    {
                        if (player.FacingDirection != stepDirection)
                        {
                            yield return player.AnimateTurn(stepDirection);
                        }

                        Vector2Int nextPosition = player.PeekPosition(stepDirection);

                        if (!grid.IsInside(nextPosition))
                        {
                            yield return StartCoroutine(HandleFailure("Магнат вышел за границы поля.", commandIndex));
                            yield break;
                        }

                        if (grid.IsBlocked(nextPosition))
                        {
                            yield return StartCoroutine(HandleFailure("Столкновение со стеной или блокирующим объектом.", commandIndex));
                            yield break;
                        }

                        yield return player.AnimateMoveTo(nextPosition);

                        int collected = grid.CollectArguments(nextPosition);
                        if (collected > 0)
                        {
                            SetStatus(collected > 1
                                ? $"Собрано доводов: +{collected}. Осталось {grid.RemainingArguments}."
                                : $"Довод собран. Осталось {grid.RemainingArguments}.", false);
                        }

                        if (grid.IsForbidden(nextPosition))
                        {
                            yield return StartCoroutine(HandleFailure("Вход в запрещённую клетку.", commandIndex));
                            yield break;
                        }

                        yield return new WaitForSeconds(betweenCommandsDelay);

                        if (!TryAdvanceTurnState(true, out string dynamicFailureMessage))
                        {
                            yield return StartCoroutine(HandleFailure(dynamicFailureMessage, commandIndex));
                            yield break;
                        }
                    }

                    continue;
                }

                if (command.Type == RouteCommandType.Wait)
                {
                    SetStatus("Пауза.", false);
                    for (int waitStep = 0; waitStep < command.Value; waitStep++)
                    {
                        yield return new WaitForSeconds(Mathf.Max(betweenCommandsDelay, 0.18f));

                        if (!TryAdvanceTurnState(false, out string dynamicFailureMessage))
                        {
                            yield return StartCoroutine(HandleFailure(dynamicFailureMessage, commandIndex));
                            yield break;
                        }
                    }
                }
            }

            if (grid.RemainingArguments > 0)
            {
                yield return StartCoroutine(HandleFailure($"Не все доводы собраны. Осталось {grid.RemainingArguments}."));
                yield break;
            }

            if (requireExitToWin && grid.HasExitCell && !grid.IsExit(player.gridPosition))
            {
                yield return StartCoroutine(HandleFailure("Все доводы собраны, но Магнат не завершил маршрут в выходной клетке."));
                yield break;
            }

            yield return new WaitForSeconds(postRunPause);
            HandleVictory();
        }

        private IEnumerator HandleFailure(string message, int failedCommandIndex = -1)
        {
            _lastFailedCommandIndex = failedCommandIndex;
            AttemptsRemaining = Mathf.Max(0, AttemptsRemaining - 1);
            IsRunning = false;

            if (AttemptsRemaining > 0)
            {
                SetStatus($"{message} Осталось попыток: {AttemptsRemaining}/{maxAttempts}.", true);
                yield return new WaitForSeconds(postRunPause);
                grid.ResetBoardState();
                player.ResetToStart();
                StateChanged?.Invoke();
                yield break;
            }

            IsResolved = true;
            SetStatus($"{message} Попытки закончились.", true);
            yield return new WaitForSeconds(postRunPause);

            FinishMiniGame(false);
        }

        private void HandleVictory()
        {
            IsRunning = false;
            IsResolved = true;
            ClearFailureMarkers(false);
            SetStatus("Все доводы собраны. Прогон успешен.", false);

            FinishMiniGame(true);
        }

        private void InitializeSession()
        {
            EnsureReferences();

            if (grid == null || player == null || queue == null)
            {
                SetStatus("Сцена мини-игры настроена не полностью: не хватает ссылок на grid, player или queue.", true);
                return;
            }

            grid.RefreshLayout();
            player.Initialize(grid);
            player.SetStartingDirection(startingDirection, true);

            if (!_initialized)
            {
                AttemptsRemaining = maxAttempts;
                _initialized = true;
            }
            else if (AttemptsRemaining <= 0 && !IsResolved)
            {
                AttemptsRemaining = maxAttempts;
            }

            grid.ResetBoardState();
            player.ResetToStart();
            IsRunning = false;
            ClearFailureMarkers(false);

            if (!IsResolved || AttemptsRemaining > 0)
            {
                SetStatus("Соберите маршрут и нажмите Enter, чтобы запустить прогон.", false);
            }
        }

        private void EnsureReferences()
        {
            if (grid == null)
            {
                grid = FindObjectOfType<GridManager>();
            }

            if (player == null)
            {
                player = FindObjectOfType<PlayerController>();
            }

            if (queue == null)
            {
                queue = FindObjectOfType<CommandQueue>();
            }

            if (inputHandler == null)
            {
                inputHandler = FindObjectOfType<MiniGameInputHandler>();
            }

            if (endGameScreenManager == null)
            {
                endGameScreenManager = FindObjectOfType<EndDayScreenManager>();
            }
        }

        private void BindEndGameScreen()
        {
            if (endGameScreenManager == null)
            {
                endGameScreenManager = FindObjectOfType<EndDayScreenManager>();
            }

            _useEndGameScreen = endGameScreenManager != null;
            if (endGameScreenManager != null)
            {
                endGameScreenManager.OnEndGame -= OnMiniGameEnded;
                endGameScreenManager.OnEndGame += OnMiniGameEnded;
            }
        }

        private void UnbindEndGameScreen()
        {
            if (endGameScreenManager != null)
            {
                endGameScreenManager.OnEndGame -= OnMiniGameEnded;
            }
        }

        private void FinishMiniGame(bool playerWin)
        {
            if (_useEndGameScreen)
            {
                OnEndGame?.Invoke(playerWin);
                return;
            }

            OnMiniGameEnded(playerWin);
        }

        private void OnDestroy()
        {
            UnbindEndGameScreen();
        }

        private bool ValidateStartCell(out string reason)
        {
            if (!grid.IsInside(player.gridPosition))
            {
                reason = "Стартовая позиция находится вне поля.";
                return false;
            }

            if (grid.IsBlocked(player.gridPosition))
            {
                reason = "Стартовая позиция занята стеной.";
                return false;
            }

            if (grid.IsForbidden(player.gridPosition))
            {
                reason = "Стартовая позиция находится в запрещённой клетке.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryAdvanceTurnState(bool allowCurrentCellToBecomeBlocked, out string failureMessage)
        {
            failureMessage = string.Empty;
            grid.AdvanceTurnState();

            if (!grid.IsInside(player.gridPosition))
            {
                failureMessage = "\u041F\u043E\u0441\u043B\u0435 \u043F\u0435\u0440\u0435\u043A\u043B\u044E\u0447\u0435\u043D\u0438\u044F \u043F\u043E\u043B\u044F \u041C\u0430\u0433\u043D\u0430\u0442 \u043E\u043A\u0430\u0437\u0430\u043B\u0441\u044F \u0432\u043D\u0435 \u0433\u0440\u0430\u043D\u0438\u0446.";
                return false;
            }

            if (!allowCurrentCellToBecomeBlocked && grid.IsBlocked(player.gridPosition))
            {
                failureMessage = "\u041F\u0435\u0440\u0435\u043A\u043B\u044E\u0447\u0430\u0435\u043C\u0430\u044F \u043F\u0440\u0435\u0433\u0440\u0430\u0434\u0430 \u0437\u0430\u043A\u0440\u044B\u043B\u0430\u0441\u044C \u043F\u043E\u0434 \u041C\u0430\u0433\u043D\u0430\u0442\u043E\u043C.";
                return false;
            }

            if (grid.IsForbidden(player.gridPosition))
            {
                failureMessage = "\u041F\u043E\u0441\u043B\u0435 \u043F\u0435\u0440\u0435\u043A\u043B\u044E\u0447\u0435\u043D\u0438\u044F \u043F\u043E\u043B\u044F \u041C\u0430\u0433\u043D\u0430\u0442 \u043E\u043A\u0430\u0437\u0430\u043B\u0441\u044F \u0432 \u0437\u0430\u043F\u0440\u0435\u0449\u0451\u043D\u043D\u043E\u0439 \u043A\u043B\u0435\u0442\u043A\u0435.";
                return false;
            }

            return true;
        }

        private void ApplyGameData()
        {
            if (_gameData == null || _gameData.customParameters == null)
            {
                return;
            }

            if (TryGetInt("attempts", out int attempts))
            {
                maxAttempts = Mathf.Max(1, attempts);
                AttemptsRemaining = maxAttempts;
            }

            int? queueLimit = TryGetInt("maxCommands", out int maxCommandsValue) ? maxCommandsValue : null;
            int? turnLimit = TryGetInt("maxTurns", out int maxTurnsValue) ? maxTurnsValue : null;
            int? waitLimit = TryGetInt("maxWaits", out int maxWaitsValue) ? maxWaitsValue : null;
            int? moveLimit = TryGetInt("maxMoves", out int maxMovesValue) ? maxMovesValue : null;
            int? requiredCount = TryGetInt("requiredCommandCount", out int requiredCommandCountValue) ? requiredCommandCountValue : null;

            RouteCommandType? requiredCommand = null;
            if (TryGetString("requiredCommand", out string requiredCommandText) &&
                Enum.TryParse(requiredCommandText, true, out RouteCommandType parsedCommand))
            {
                requiredCommand = parsedCommand;
            }

            queue.ApplyExternalLimits(queueLimit, turnLimit, waitLimit, moveLimit, requiredCommand, requiredCount);

            if (TryGetFloat("stepDelay", out float commandDelay))
            {
                betweenCommandsDelay = Mathf.Max(0f, commandDelay);
            }

            if (TryGetBool("requireExit", out bool requireExit))
            {
                requireExitToWin = requireExit;
            }

            if (TryGetString("startingDirection", out string directionText) &&
                Enum.TryParse(directionText, true, out RouteDirection parsedDirection))
            {
                startingDirection = parsedDirection;
            }
        }

        private bool TryGetInt(string key, out int value)
        {
            value = 0;
            if (!TryGetObject(key, out object rawValue))
            {
                return false;
            }

            if (rawValue is int intValue)
            {
                value = intValue;
                return true;
            }

            if (rawValue is long longValue)
            {
                value = (int)longValue;
                return true;
            }

            if (rawValue is float floatValue)
            {
                value = Mathf.RoundToInt(floatValue);
                return true;
            }

            if (rawValue is double doubleValue)
            {
                value = (int)Math.Round(doubleValue);
                return true;
            }

            if (rawValue is string stringValue && int.TryParse(stringValue, out int parsedValue))
            {
                value = parsedValue;
                return true;
            }

            if (rawValue is JValue jValue)
            {
                value = jValue.Value<int>();
                return true;
            }

            return false;
        }

        private bool TryGetFloat(string key, out float value)
        {
            value = 0f;
            if (!TryGetObject(key, out object rawValue))
            {
                return false;
            }

            if (rawValue is float floatValue)
            {
                value = floatValue;
                return true;
            }

            if (rawValue is double doubleValue)
            {
                value = (float)doubleValue;
                return true;
            }

            if (rawValue is int intValue)
            {
                value = intValue;
                return true;
            }

            if (rawValue is long longValue)
            {
                value = longValue;
                return true;
            }

            if (rawValue is string stringValue && float.TryParse(stringValue, out float parsedValue))
            {
                value = parsedValue;
                return true;
            }

            if (rawValue is JValue jValue)
            {
                value = jValue.Value<float>();
                return true;
            }

            return false;
        }

        private bool TryGetBool(string key, out bool value)
        {
            value = false;
            if (!TryGetObject(key, out object rawValue))
            {
                return false;
            }

            if (rawValue is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            if (rawValue is string stringValue && bool.TryParse(stringValue, out bool parsedValue))
            {
                value = parsedValue;
                return true;
            }

            if (rawValue is JValue jValue)
            {
                value = jValue.Value<bool>();
                return true;
            }

            return false;
        }

        private bool TryGetString(string key, out string value)
        {
            value = string.Empty;
            if (!TryGetObject(key, out object rawValue) || rawValue == null)
            {
                return false;
            }

            if (rawValue is string stringValue)
            {
                value = stringValue;
                return true;
            }

            if (rawValue is JValue jValue)
            {
                value = jValue.Value<string>();
                return true;
            }

            value = rawValue.ToString();
            return !string.IsNullOrEmpty(value);
        }

        private bool TryGetObject(string key, out object rawValue)
        {
            rawValue = null;
            return _gameData != null &&
                   _gameData.customParameters != null &&
                   _gameData.customParameters.TryGetValue(key, out rawValue);
        }
    }
}

