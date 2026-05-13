using System;
using System.Collections;
using System.Collections.Generic;
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
        [SerializeField, Min(5f)] private float candleDurationSeconds = 60f;
        [SerializeField] private float betweenCommandsDelay = 0.12f;
        [SerializeField] private float postRunPause = 0.35f;
        [SerializeField] private bool requireExitToWin;
        [SerializeField] private bool requireOrderedArgumentCollection;
        [SerializeField] private RouteDirection startingDirection = RouteDirection.Up;

        [Header("Warnings")]
        [SerializeField, Min(1)] private int lowMovesWarningThreshold = 3;

        [Header("Live Feel")]
        [SerializeField, Min(0f)] private float liveInputCooldown = 0.015f;
        [SerializeField, Min(0.01f)] private float liveWaitDuration = 0.05f;
        [SerializeField, Min(0.01f)] private float liveMoveDuration = 0.11f;
        [SerializeField, Min(0.01f)] private float liveTurnDuration = 0.06f;

        public event Action StateChanged;
        public event Action<bool> OnEndGame;

        public bool IsRunning { get; private set; }
        public bool IsResolved { get; private set; }
        public string StatusMessage { get; private set; }
        public GridManager Grid => grid;
        public RoutePlanPreview CurrentPreview { get; private set; } = new();
        public float RemainingCandleSeconds => Mathf.Max(0f, _remainingCandleSeconds);
        public float CandleNormalized => _resolvedCandleDuration > 0.0001f
            ? Mathf.Clamp01(RemainingCandleSeconds / _resolvedCandleDuration)
            : 0f;
        public bool UsesOrderedArgumentCollection => requireOrderedArgumentCollection || (grid != null && grid.HasSequencedArguments);
        public int CollectedArguments => grid != null ? Mathf.Max(0, grid.TotalArguments - grid.RemainingArguments) : 0;
        public int PlannedCollectedArguments => CollectedArguments;
        public int TotalArguments => grid != null ? grid.TotalArguments : 0;

        private MiniGameData _gameData;
        private bool _initialized;
        private int _lastFailedCommandIndex = -1;
        private bool _hasBootstrappedScene;
        private bool _isTutorialPending;
        private bool _useEndGameScreen;
        private float _remainingCandleSeconds;
        private float _resolvedCandleDuration;
        private bool _timerExpiryHandled;
        private CommandQueue _boundQueue;
        private Coroutine _activeRunRoutine;
        private int _nextRequiredSequence = 1;
        private readonly Queue<RouteCommandType> _pendingImmediateCommands = new();

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
            SetStatus("Enter больше не используется: ходы выполняются сразу при нажатии.", true);
            return true;
        }

        public void ResetSession(bool restoreAttempts, bool clearQueue)
        {
            if (IsRunning || IsResolved)
            {
                return;
            }

            EnsureReferences();
            BindQueue();

            ResetProgressInternal(clearQueue);
            SetStatus("Прогресс по полю сброшен. Свеча продолжает гореть.", false);
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
            if (_lastFailedCommandIndex < 0 || commandCount > _lastFailedCommandIndex)
            {
                return;
            }

            ClearFailureMarkers(notify);
        }

        public void RefreshRoutePreview(bool notify = true)
        {
            EnsureReferences();

            if (grid == null || player == null || queue == null)
            {
                CurrentPreview = new RoutePlanPreview
                {
                    IsPathValid = false,
                    ValidationMessage = "Маршрут недоступен: сцена мини-игры настроена не полностью."
                };

                if (notify)
                {
                    StateChanged?.Invoke();
                }

                return;
            }

            CurrentPreview = new RoutePlanPreview
            {
                IsPathValid = true,
                UsesOrderedArguments = UsesOrderedArgumentCollection,
                PlannedCollectedArguments = CollectedArguments,
                TotalArguments = TotalArguments,
                ValidationMessage = string.Empty
            };
            grid.ApplyRoutePreviewHighlights(null);

            if (notify)
            {
                StateChanged?.Invoke();
            }
        }

        public bool TryExecuteImmediateCommand(RouteCommandType type)
        {
            EnsureReferences();
            BindQueue();

            if (IsRunning)
            {
                return TryBufferImmediateCommand(type);
            }

            if (!CanProcessImmediateCommand(type, out RouteDirection? stepDirection, out string reason))
            {
                SetStatus(reason, true);
                return false;
            }

            if (_activeRunRoutine != null)
            {
                StopCoroutine(_activeRunRoutine);
            }

            _activeRunRoutine = StartCoroutine(ExecuteImmediateCommandRoutine(type, stepDirection));
            return true;
        }

        public bool TryUndoLastAction()
        {
            if (!CanProcessHistoryEdit(out string reason))
            {
                SetStatus(reason, true);
                return false;
            }

            _pendingImmediateCommands.Clear();

            List<RouteCommandType> historySnapshot = CaptureHistoryTypes();
            if (historySnapshot.Count == 0)
            {
                SetStatus("История действий уже пуста.", true);
                return false;
            }

            int targetCommandCount = historySnapshot.Count - 1;

            if (_activeRunRoutine != null)
            {
                StopCoroutine(_activeRunRoutine);
            }

            _activeRunRoutine = StartCoroutine(RollbackHistoryRoutine(
                targetCommandCount,
                historySnapshot,
                targetCommandCount > 0
                    ? "Последнее действие отменено."
                    : "Все действия очищены. Магнат снова на старте."));
            return true;
        }

        public bool ResetProgressKeepTimer()
        {
            if (!CanProcessHistoryEdit(out string reason))
            {
                SetStatus(reason, true);
                return false;
            }

            _pendingImmediateCommands.Clear();

            List<RouteCommandType> historySnapshot = CaptureHistoryTypes();

            if (_activeRunRoutine != null)
            {
                StopCoroutine(_activeRunRoutine);
            }

            _activeRunRoutine = StartCoroutine(RollbackHistoryRoutine(
                0,
                historySnapshot,
                "Прогресс по полю сброшен. Свеча продолжает гореть."));
            return true;
        }

        private IEnumerator ExecuteImmediateCommandRoutine(RouteCommandType type, RouteDirection? stepDirection)
        {
            IsRunning = true;
            ClearFailureMarkers(false);
            StateChanged?.Invoke();

            if (!queue.TryAddCommand(type, out _))
            {
                IsRunning = false;
                _activeRunRoutine = null;
                yield break;
            }

            int collectedArguments = 0;
            string warningMessage = string.Empty;

            if (stepDirection.HasValue)
            {
                RouteDirection direction = stepDirection.Value;
                if (player.FacingDirection != direction)
                {
                    yield return player.AnimateTurn(direction);
                }

                Vector2Int nextPosition = player.PeekPosition(direction);
                yield return player.AnimateMoveTo(nextPosition);

                collectedArguments = grid.CollectArguments(nextPosition, UsesOrderedArgumentCollection, ref _nextRequiredSequence);

                if (betweenCommandsDelay > 0f)
                {
                    yield return new WaitForSeconds(betweenCommandsDelay);
                }

                if (!TryAdvanceTurnState(true, out warningMessage))
                {
                    queue.RemoveLast(out _);
                    RebuildStateFromHistory(out string rebuildFailure);
                    _pendingImmediateCommands.Clear();
                    IsRunning = false;
                    _activeRunRoutine = null;
                    SetStatus(string.IsNullOrWhiteSpace(rebuildFailure) ? warningMessage : rebuildFailure, true);
                    yield break;
                }
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(betweenCommandsDelay, liveWaitDuration));

                if (!TryAdvanceTurnState(false, out warningMessage))
                {
                    queue.RemoveLast(out _);
                    RebuildStateFromHistory(out string rebuildFailure);
                    _pendingImmediateCommands.Clear();
                    IsRunning = false;
                    _activeRunRoutine = null;
                    SetStatus(string.IsNullOrWhiteSpace(rebuildFailure) ? warningMessage : rebuildFailure, true);
                    yield break;
                }
            }

            RefreshRoutePreview(false);
            IsRunning = false;
            _activeRunRoutine = null;

            if (TryCompleteIfSolved(out string completionHint))
            {
                yield break;
            }

            if (TryStartNextBufferedCommand())
            {
                yield break;
            }

            SetStatus(
                string.IsNullOrWhiteSpace(completionHint)
                    ? BuildActionSuccessMessage(type, collectedArguments)
                    : completionHint,
                false);
        }

        private IEnumerator RollbackHistoryRoutine(
            int targetCommandCount,
            List<RouteCommandType> historySnapshot,
            string successMessage)
        {
            IsRunning = true;
            ClearFailureMarkers(false);
            StateChanged?.Invoke();

            yield return AnimateRollbackToHistoryCount(targetCommandCount, historySnapshot);
            RouteDirection rollbackFacing = player != null ? player.FacingDirection : startingDirection;

            while (queue.Commands.Count > targetCommandCount)
            {
                queue.RemoveLast(out _);
            }

            if (!RebuildStateFromHistory(true, out string rebuildFailure))
            {
                ResetProgressInternal(true);
                IsRunning = false;
                _activeRunRoutine = null;
                SetStatus(rebuildFailure, true);
                yield break;
            }

            if (player != null)
            {
                player.SetState(player.gridPosition, rollbackFacing, false);
                StateChanged?.Invoke();
            }

            IsRunning = false;
            _activeRunRoutine = null;

            if (TryStartNextBufferedCommand())
            {
                yield break;
            }

            SetStatus(successMessage, false);
        }

        private IEnumerator AnimateRollbackToHistoryCount(
            int targetCommandCount,
            List<RouteCommandType> historySnapshot)
        {
            if (player == null || historySnapshot == null || historySnapshot.Count <= targetCommandCount)
            {
                yield break;
            }

            for (int index = historySnapshot.Count - 1; index >= targetCommandCount; index--)
            {
                RouteCommandType commandType = historySnapshot[index];
                if (!RouteDirectionUtility.TryGetStepDirection(commandType, out RouteDirection stepDirection))
                {
                    if (commandType == RouteCommandType.Wait)
                    {
                        yield return new WaitForSeconds(Mathf.Max(0.05f, betweenCommandsDelay * 0.5f));
                    }

                    continue;
                }

                RouteDirection rollbackDirection = RouteDirectionUtility.Opposite(stepDirection);
                if (player.FacingDirection != rollbackDirection)
                {
                    yield return player.AnimateTurn(rollbackDirection);
                }

                Vector2Int previousPosition = player.gridPosition - RouteDirectionUtility.ToVector2Int(stepDirection);
                yield return player.AnimateMoveTo(previousPosition);
            }
        }

        private bool CanProcessPlayerAction(out string reason)
        {
            EnsureReferences();
            BindQueue();

            if (!_initialized)
            {
                reason = "Мини-игра ещё не инициализирована.";
                return false;
            }

            if (grid == null || player == null || queue == null)
            {
                reason = "Сцена мини-игры настроена не полностью.";
                return false;
            }

            if (IsResolved)
            {
                reason = RemainingCandleSeconds <= 0f
                    ? "Свеча уже догорела."
                    : "Мини-игра уже завершена.";
                return false;
            }

            if (RemainingCandleSeconds <= 0f)
            {
                HandleTimerExpiry();
                reason = "Свеча уже догорела.";
                return false;
            }

            if (IsRunning)
            {
                reason = "Дождитесь завершения текущего действия.";
                return false;
            }

            if (!ValidateStartCell(out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool CanProcessImmediateCommand(RouteCommandType type, out RouteDirection? stepDirection, out string reason)
        {
            stepDirection = null;

            if (!CanProcessPlayerAction(out reason))
            {
                return false;
            }

            if (!queue.CanAddCommand(type, out reason))
            {
                return false;
            }

            if (RouteDirectionUtility.TryGetStepDirection(type, out RouteDirection resolvedDirection))
            {
                if (!TryValidateImmediateMove(resolvedDirection, out _, out reason))
                {
                    return false;
                }

                stepDirection = resolvedDirection;
            }

            reason = string.Empty;
            return true;
        }

        private bool TryBufferImmediateCommand(RouteCommandType type)
        {
            if (!_initialized)
            {
                SetStatus("Мини-игра ещё не инициализирована.", true);
                return false;
            }

            if (queue == null || player == null || grid == null)
            {
                SetStatus("Сцена мини-игры настроена не полностью.", true);
                return false;
            }

            if (IsResolved)
            {
                SetStatus(RemainingCandleSeconds <= 0f ? "Свеча уже догорела." : "Мини-игра уже завершена.", true);
                return false;
            }

            if (RemainingCandleSeconds <= 0f)
            {
                HandleTimerExpiry();
                return false;
            }

            if (!CanReserveBufferedCommand(type, out string reason))
            {
                SetStatus(reason, true);
                return false;
            }

            _pendingImmediateCommands.Enqueue(type);
            return true;
        }

        private bool CanReserveBufferedCommand(RouteCommandType type, out string reason)
        {
            int reservedCommands = queue.Commands.Count + _pendingImmediateCommands.Count;
            if (reservedCommands >= Mathf.Max(queue.maxCommands, 1))
            {
                reason = $"Очередь действий заполнена: {queue.maxCommands}.";
                return false;
            }

            if (type == RouteCommandType.Wait && queue.MaxWaitCommands >= 0)
            {
                int reservedWaits = queue.CountOfType(RouteCommandType.Wait) + CountBufferedCommandsOfType(RouteCommandType.Wait);
                if (reservedWaits >= queue.MaxWaitCommands)
                {
                    reason = $"Лимит пауз достигнут ({queue.MaxWaitCommands}).";
                    return false;
                }
            }

            if (RouteDirectionUtility.IsStepCommand(type) && queue.MaxMoveCommands >= 0)
            {
                int reservedMoves = queue.CountMoveCommands() + CountBufferedMoveCommands();
                if (reservedMoves >= queue.MaxMoveCommands)
                {
                    reason = $"Лимит шагов достигнут ({queue.MaxMoveCommands}).";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private bool TryStartNextBufferedCommand()
        {
            while (!IsRunning && !IsResolved && _pendingImmediateCommands.Count > 0)
            {
                RouteCommandType nextType = _pendingImmediateCommands.Dequeue();
                if (CanProcessImmediateCommand(nextType, out RouteDirection? stepDirection, out string reason))
                {
                    if (_activeRunRoutine != null)
                    {
                        StopCoroutine(_activeRunRoutine);
                    }

                    _activeRunRoutine = StartCoroutine(ExecuteImmediateCommandRoutine(nextType, stepDirection));
                    return true;
                }

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    SetStatus(reason, true);
                }
            }

            return false;
        }

        private bool CanProcessHistoryEdit(out string reason)
        {
            if (!CanProcessPlayerAction(out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private List<RouteCommandType> CaptureHistoryTypes()
        {
            List<RouteCommandType> result = new();
            if (queue == null || queue.Commands == null)
            {
                return result;
            }

            for (int index = 0; index < queue.Commands.Count; index++)
            {
                result.Add(queue.Commands[index].Type);
            }

            return result;
        }

        private int CountBufferedCommandsOfType(RouteCommandType type)
        {
            int count = 0;
            foreach (RouteCommandType bufferedType in _pendingImmediateCommands)
            {
                if (bufferedType == type)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountBufferedMoveCommands()
        {
            int count = 0;
            foreach (RouteCommandType bufferedType in _pendingImmediateCommands)
            {
                if (RouteDirectionUtility.IsStepCommand(bufferedType))
                {
                    count++;
                }
            }

            return count;
        }

        private bool TryValidateImmediateMove(RouteDirection direction, out Vector2Int nextPosition, out string reason)
        {
            nextPosition = player.PeekPosition(direction);

            if (!grid.IsInside(nextPosition))
            {
                reason = "Нельзя выйти за границы поля.";
                return false;
            }

            if (grid.IsBlocked(nextPosition))
            {
                reason = "Путь перекрыт препятствием.";
                return false;
            }

            if (grid.IsForbidden(nextPosition))
            {
                reason = "Нельзя войти в запрещённую клетку.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool RebuildStateFromHistory(out string failureMessage)
        {
            return RebuildStateFromHistory(false, out failureMessage);
        }

        private bool RebuildStateFromHistory(bool preserveVisualPose, out string failureMessage)
        {
            EnsureReferences();
            BindQueue();

            if (grid == null || player == null || queue == null)
            {
                failureMessage = "Сцена мини-игры настроена не полностью.";
                return false;
            }

            grid.ResetBoardState();
            grid.ApplyRoutePreviewHighlights(null);
            if (preserveVisualPose)
            {
                player.SetState(player.StartGridPosition, player.StartDirection, false);
            }
            else
            {
                player.ResetToStart();
            }

            _nextRequiredSequence = 1;
            ClearFailureMarkers(false);

            if (!ValidateStartCell(out failureMessage))
            {
                return false;
            }

            for (int index = 0; index < queue.Commands.Count; index++)
            {
                if (!ApplyHistoricalCommand(queue.Commands[index].Type, preserveVisualPose, out failureMessage))
                {
                    return false;
                }
            }

            RefreshRoutePreview(false);
            StateChanged?.Invoke();
            failureMessage = string.Empty;
            return true;
        }

        private bool ApplyHistoricalCommand(RouteCommandType type, out string failureMessage)
        {
            return ApplyHistoricalCommand(type, false, out failureMessage);
        }

        private bool ApplyHistoricalCommand(RouteCommandType type, bool preserveVisualPose, out string failureMessage)
        {
            if (RouteDirectionUtility.TryGetStepDirection(type, out RouteDirection direction))
            {
                if (!TryValidateImmediateMove(direction, out Vector2Int nextPosition, out failureMessage))
                {
                    return false;
                }

                if (preserveVisualPose)
                {
                    player.SetState(nextPosition, direction, false);
                }
                else
                {
                    player.SnapTo(nextPosition, direction);
                }

                grid.CollectArguments(nextPosition, UsesOrderedArgumentCollection, ref _nextRequiredSequence);
                return TryAdvanceTurnState(true, out failureMessage);
            }

            if (type == RouteCommandType.Wait)
            {
                return TryAdvanceTurnState(false, out failureMessage);
            }

            failureMessage = string.Empty;
            return true;
        }

        private bool TryCompleteIfSolved(out string completionHint)
        {
            completionHint = string.Empty;

            if (grid == null || grid.RemainingArguments > 0)
            {
                return false;
            }

            if (requireExitToWin && grid.HasExitCell && !grid.IsExit(player.gridPosition))
            {
                completionHint = "Все доводы собраны. Доберитесь до выхода, пока горит свеча.";
                return false;
            }

            HandleVictory();
            return true;
        }

        private string BuildActionSuccessMessage(RouteCommandType type, int collectedArguments)
        {
            if (collectedArguments > 1)
            {
                return $"Собрано доводов: +{collectedArguments}. Осталось {grid.RemainingArguments}.";
            }

            if (collectedArguments == 1)
            {
                return $"Довод собран. Осталось {grid.RemainingArguments}.";
            }

            return type == RouteCommandType.Wait
                ? "Пауза выполнена."
                : $"Ход: {RouteDirectionUtility.CommandReadable(type)}.";
        }

        private void ResetProgressInternal(bool clearQueue)
        {
            if (_activeRunRoutine != null)
            {
                StopCoroutine(_activeRunRoutine);
                _activeRunRoutine = null;
            }

            _pendingImmediateCommands.Clear();

            if (clearQueue && queue != null)
            {
                queue.Clear();
            }

            if (grid != null)
            {
                grid.ResetBoardState();
                grid.ApplyRoutePreviewHighlights(null);
            }

            if (player != null)
            {
                player.ResetToStart();
            }

            _nextRequiredSequence = 1;
            IsRunning = false;
            ClearFailureMarkers(false);
            RefreshRoutePreview(false);
            StateChanged?.Invoke();
        }

        private void Awake()
        {
            EnsureReferences();
            BindQueue();
            EnsureBoardPreview();
            EnsureMinimalHud();
            EnsureLegacyUiHider();
            BindEndGameScreen();
        }

        private void Start()
        {
            BeginSceneBootstrap();
        }

        private void Update()
        {
            RefreshRemainingMovesIndicator();

            if (!_initialized || IsResolved || _isTutorialPending)
            {
                return;
            }

            if (_remainingCandleSeconds > 0f)
            {
                _remainingCandleSeconds = Mathf.Max(0f, _remainingCandleSeconds - Time.deltaTime);
            }

            if (_remainingCandleSeconds <= 0f)
            {
                HandleTimerExpiry();
            }
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
            BindQueue();
            EnsureBoardPreview();
            EnsureMinimalHud();
            EnsureLegacyUiHider();
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
                yield return StartCoroutine(HandleUnexpectedRuntimeFailure(startFailure));
                yield break;
            }

            int nextRequiredSequence = 1;

            for (int commandIndex = 0; commandIndex < queue.Commands.Count; commandIndex++)
            {
                if (IsResolved)
                {
                    yield break;
                }

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
                            yield return StartCoroutine(HandleUnexpectedRuntimeFailure("Магнат вышел за границы поля.", commandIndex));
                            yield break;
                        }

                        if (grid.IsBlocked(nextPosition))
                        {
                            yield return StartCoroutine(HandleUnexpectedRuntimeFailure("Маршрут упирается в препятствие.", commandIndex));
                            yield break;
                        }

                        yield return player.AnimateMoveTo(nextPosition);

                        int collected = grid.CollectArguments(nextPosition, UsesOrderedArgumentCollection, ref nextRequiredSequence);
                        if (collected > 0)
                        {
                            SetStatus(collected > 1
                                ? $"Собрано доводов: +{collected}. Осталось {grid.RemainingArguments}."
                                : $"Довод собран. Осталось {grid.RemainingArguments}.", false);
                        }

                        if (grid.IsForbidden(nextPosition))
                        {
                            yield return StartCoroutine(HandleUnexpectedRuntimeFailure("Маршрут входит в запрещённую клетку.", commandIndex));
                            yield break;
                        }

                        yield return new WaitForSeconds(betweenCommandsDelay);

                        if (IsResolved)
                        {
                            yield break;
                        }

                        if (!TryAdvanceTurnState(true, out string dynamicFailureMessage))
                        {
                            yield return StartCoroutine(HandleUnexpectedRuntimeFailure(dynamicFailureMessage, commandIndex));
                            yield break;
                        }
                    }

                    continue;
                }

                if (command.Type != RouteCommandType.Wait)
                {
                    continue;
                }

                SetStatus("Пауза.", false);
                for (int waitStep = 0; waitStep < command.Value; waitStep++)
                {
                    yield return new WaitForSeconds(Mathf.Max(betweenCommandsDelay, 0.18f));

                    if (IsResolved)
                    {
                        yield break;
                    }

                    if (!TryAdvanceTurnState(false, out string dynamicFailureMessage))
                    {
                        yield return StartCoroutine(HandleUnexpectedRuntimeFailure(dynamicFailureMessage, commandIndex));
                        yield break;
                    }
                }
            }

            if (grid.RemainingArguments > 0)
            {
                yield return StartCoroutine(HandleUnsuccessfulRun($"Не все доводы собраны. Осталось {grid.RemainingArguments}."));
                yield break;
            }

            if (requireExitToWin && grid.HasExitCell && !grid.IsExit(player.gridPosition))
            {
                yield return StartCoroutine(HandleUnsuccessfulRun("Все доводы собраны, но Магнат не завершил маршрут в выходной клетке."));
                yield break;
            }

            yield return new WaitForSeconds(postRunPause);

            if (!IsResolved)
            {
                HandleVictory();
            }
        }

        private IEnumerator HandleUnsuccessfulRun(string message)
        {
            IsRunning = false;
            _activeRunRoutine = null;

            SetStatus($"{message} Магнат возвращается в начало, свеча продолжает гореть.", true);
            yield return new WaitForSeconds(postRunPause);

            if (IsResolved)
            {
                yield break;
            }

            grid.ResetBoardState();
            player.ResetToStart();
            RefreshRoutePreview(false);
            StateChanged?.Invoke();
        }

        private IEnumerator HandleUnexpectedRuntimeFailure(string message, int failedCommandIndex = -1)
        {
            _lastFailedCommandIndex = failedCommandIndex;
            IsRunning = false;
            _activeRunRoutine = null;

            SetStatus($"{message} Исправьте маршрут и попробуйте снова, пока не догорела свеча.", true);
            yield return new WaitForSeconds(postRunPause);

            if (IsResolved)
            {
                yield break;
            }

            grid.ResetBoardState();
            player.ResetToStart();
            RefreshRoutePreview(false);
            StateChanged?.Invoke();
        }

        private void HandleVictory()
        {
            IsRunning = false;
            IsResolved = true;
            _activeRunRoutine = null;
            ClearFailureMarkers(false);
            SetStatus("Все доводы собраны. Прогон успешен.", false);

            FinishMiniGame(true);
        }

        private void InitializeSession()
        {
            EnsureReferences();
            BindQueue();

            if (grid == null || player == null || queue == null)
            {
                CurrentPreview = new RoutePlanPreview
                {
                    IsPathValid = false,
                    ValidationMessage = "Сцена мини-игры настроена не полностью: не хватает ссылок на grid, player или queue."
                };
                SetStatus(CurrentPreview.ValidationMessage, true);
                return;
            }

            grid.RefreshLayout();
            player.Initialize(grid);
            player.SetStartingDirection(startingDirection, true);
            player.ClampMotionTimings(liveMoveDuration, liveTurnDuration);
            betweenCommandsDelay = Mathf.Min(betweenCommandsDelay, liveInputCooldown);

            _resolvedCandleDuration = Mathf.Max(5f, candleDurationSeconds);
            _remainingCandleSeconds = _resolvedCandleDuration;
            _timerExpiryHandled = false;
            _initialized = true;
            IsRunning = false;
            IsResolved = false;
            StatusMessage = string.Empty;
            _nextRequiredSequence = 1;
            _pendingImmediateCommands.Clear();

            grid.ResetBoardState();
            grid.ApplyRoutePreviewHighlights(null);
            player.ResetToStart();
            ClearFailureMarkers(false);
            RefreshRoutePreview(false);
            RefreshRemainingMovesIndicator();
            SetStatus("Ходите по полю сразу, собирайте доводы и следите за свечой.", false);
        }

        private void RefreshRemainingMovesIndicator()
        {
            if (player == null || queue == null)
            {
                return;
            }

            int maxCommands = Mathf.Max(queue.maxCommands, 1);
            int remainingMoves = Mathf.Max(0, maxCommands - queue.Commands.Count);
            bool shouldShow =
                _initialized &&
                !IsResolved &&
                RemainingCandleSeconds > 0f &&
                remainingMoves > 0 &&
                remainingMoves <= Mathf.Max(1, lowMovesWarningThreshold);

            player.SetRemainingMovesIndicator(remainingMoves, shouldShow);
        }

        private void EnsureReferences()
        {
            if (grid == null)
            {
                grid = FindAnyObjectByType<GridManager>();
            }

            if (player == null)
            {
                player = FindAnyObjectByType<PlayerController>();
            }

            if (queue == null)
            {
                queue = FindAnyObjectByType<CommandQueue>();
            }

            if (inputHandler == null)
            {
                inputHandler = FindAnyObjectByType<MiniGameInputHandler>();
            }

            if (endGameScreenManager == null)
            {
                endGameScreenManager = FindAnyObjectByType<EndDayScreenManager>();
            }
        }

        private void BindQueue()
        {
            if (_boundQueue == queue)
            {
                return;
            }

            if (_boundQueue != null)
            {
                _boundQueue.Changed -= HandleQueueChanged;
            }

            _boundQueue = queue;

            if (_boundQueue != null)
            {
                _boundQueue.Changed -= HandleQueueChanged;
                _boundQueue.Changed += HandleQueueChanged;
            }
        }

        private void HandleQueueChanged()
        {
            if (IsResolved)
            {
                return;
            }

            RefreshRoutePreview();
        }

        private void EnsureBoardPreview()
        {
            RouteBoardPreview preview = GetComponent<RouteBoardPreview>();
            if (preview != null)
            {
                preview.enabled = false;
            }
        }

        private void EnsureMinimalHud()
        {
            RouteMiniGameHUD existingHud = FindAnyObjectByType<RouteMiniGameHUD>(FindObjectsInactive.Include);
            if (existingHud != null)
            {
                existingHud.Initialize(queue, this);
            }
        }

        private void EnsureLegacyUiHider()
        {
            RouteLegacyUiHider legacyUiHider = GetComponent<RouteLegacyUiHider>();
            if (legacyUiHider != null)
            {
                legacyUiHider.enabled = false;
            }
        }

        private void BindEndGameScreen()
        {
            if (endGameScreenManager == null)
            {
                endGameScreenManager = FindAnyObjectByType<EndDayScreenManager>();
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

        private bool ValidateStartCell(out string reason)
        {
            if (!grid.IsInside(player.gridPosition))
            {
                reason = "Стартовая позиция находится вне поля.";
                return false;
            }

            if (grid.IsBlocked(player.gridPosition))
            {
                reason = "Стартовая позиция занята препятствием.";
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
            grid.AdvanceTurnState(player != null ? player.gridPosition : (Vector2Int?)null);

            if (!grid.IsInside(player.gridPosition))
            {
                failureMessage = "После переключения поля Магнат оказался вне границ.";
                return false;
            }

            if (!allowCurrentCellToBecomeBlocked && grid.IsBlocked(player.gridPosition))
            {
                failureMessage = "Переключаемая преграда закрылась под Магнатом.";
                return false;
            }

            if (grid.IsForbidden(player.gridPosition))
            {
                failureMessage = "После переключения поля Магнат оказался в запрещённой клетке.";
                return false;
            }

            return true;
        }

        private void HandleTimerExpiry()
        {
            if (_timerExpiryHandled || IsResolved)
            {
                return;
            }

            _timerExpiryHandled = true;
            _remainingCandleSeconds = 0f;
            IsRunning = false;
            IsResolved = true;
            _pendingImmediateCommands.Clear();

            if (_activeRunRoutine != null)
            {
                StopCoroutine(_activeRunRoutine);
                _activeRunRoutine = null;
            }

            SetStatus("Свеча догорела. Мини-игра провалена.", true);
            FinishMiniGame(false);
        }

        private void ApplyGameData()
        {
            if (_gameData == null || _gameData.customParameters == null)
            {
                return;
            }

            bool hasExplicitCandleDuration = TryGetFloatFromKeys(
                out float resolvedCandleDuration,
                "candleDuration",
                "candleDurationSeconds",
                "planningTimeSeconds",
                "timeLimitSeconds",
                "timerSeconds");

            if (hasExplicitCandleDuration)
            {
                candleDurationSeconds = Mathf.Max(5f, resolvedCandleDuration);
            }
            else if (TryGetInt("attempts", out int attempts))
            {
                candleDurationSeconds = Mathf.Max(30f, attempts * 20f);
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

            if (TryGetBoolFromKeys(out bool orderedArguments, "requireOrderedArguments", "strictArgumentOrder", "orderedArguments"))
            {
                requireOrderedArgumentCollection = orderedArguments;
            }

            if (TryGetString("startingDirection", out string directionText) &&
                Enum.TryParse(directionText, true, out RouteDirection parsedDirection))
            {
                startingDirection = parsedDirection;
            }
        }

        private bool TryGetFloatFromKeys(out float value, params string[] keys)
        {
            value = 0f;

            for (int index = 0; index < keys.Length; index++)
            {
                if (!TryGetFloat(keys[index], out float resolvedValue))
                {
                    continue;
                }

                value = resolvedValue;
                return true;
            }

            return false;
        }

        private bool TryGetBoolFromKeys(out bool value, params string[] keys)
        {
            value = false;

            for (int index = 0; index < keys.Length; index++)
            {
                if (!TryGetBool(keys[index], out bool resolvedValue))
                {
                    continue;
                }

                value = resolvedValue;
                return true;
            }

            return false;
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

        private void OnDestroy()
        {
            if (_boundQueue != null)
            {
                _boundQueue.Changed -= HandleQueueChanged;
            }

            UnbindEndGameScreen();
        }
    }

    [DisallowMultipleComponent]
    public class RouteBoardPreview : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }
    }

    [DisallowMultipleComponent]
    public class RouteLegacyUiHider : MonoBehaviour
    {
        private void Awake()
        {
            enabled = false;
        }
    }
}
