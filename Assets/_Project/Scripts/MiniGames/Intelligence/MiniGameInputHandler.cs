using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Game
{
    public class MiniGameInputHandler : MonoBehaviour
    {
        public CommandQueue queue;
        public ExecutionManager executor;

        [Header("Input Mapping")]
        [SerializeField] private bool swapHorizontalControls;
        [SerializeField] private bool swapVerticalControls;

        [Header("Scene HUD")]
        [SerializeField] private RouteMiniGameHUD hud;

        [Header("Reset Hold")]
        [SerializeField, Min(0.15f)] private float resetHoldDuration = 0.45f;

        [Header("Mouse Input")]
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField, Min(0.1f)] private float doubleClickInterval = 0.28f;

        private bool _resetHoldActive;
        private bool _resetHoldTriggered;
        private float _resetHoldStartedAt;
        private bool _mouseResetHoldActive;
        private bool _mouseResetHoldTriggered;
        private float _mouseResetHoldStartedAt;
        private bool _hasPendingLeftClick;
        private Vector2Int _pendingLeftClickCell;
        private float _pendingLeftClickAt;
        private Camera _inputCamera;

        private void Awake()
        {
            RefreshRuntimeReferences();
            EnsureHud();
        }

        private void Start()
        {
            RefreshRuntimeReferences();
            EnsureHud();
        }

        private void Update()
        {
            RefreshRuntimeReferences();
            if (queue == null || executor == null)
            {
                return;
            }

            if (WasPressed(KeyCode.W, KeyCode.UpArrow))
            {
                HandleAction(swapVerticalControls ? RouteControlAction.MoveDown : RouteControlAction.MoveUp);
            }
            else if (WasPressed(KeyCode.D, KeyCode.RightArrow))
            {
                HandleAction(swapHorizontalControls ? RouteControlAction.MoveLeft : RouteControlAction.MoveRight);
            }
            else if (WasPressed(KeyCode.S, KeyCode.DownArrow))
            {
                HandleAction(swapVerticalControls ? RouteControlAction.MoveUp : RouteControlAction.MoveDown);
            }
            else if (WasPressed(KeyCode.A, KeyCode.LeftArrow))
            {
                HandleAction(swapHorizontalControls ? RouteControlAction.MoveRight : RouteControlAction.MoveLeft);
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                HandleAction(RouteControlAction.Wait);
            }

            HandleResetInput();
            HandleMouseInput();
        }

        public string GetActionKeyLabel(RouteControlAction action)
        {
            return action switch
            {
                RouteControlAction.MoveUp => swapVerticalControls ? "S / v" : "W / ^",
                RouteControlAction.MoveRight => swapHorizontalControls ? "A / >" : "D / >",
                RouteControlAction.MoveDown => swapVerticalControls ? "W / ^" : "S / v",
                RouteControlAction.MoveLeft => swapHorizontalControls ? "D / <" : "A / <",
                _ => RouteMiniGameIcons.ActionKey(action)
            };
        }

        public void HandleAction(RouteControlAction action)
        {
            if (executor == null)
            {
                return;
            }

            switch (action)
            {
                case RouteControlAction.MoveUp:
                    executor.TryExecuteImmediateCommand(RouteCommandType.MoveUp);
                    break;

                case RouteControlAction.MoveRight:
                    executor.TryExecuteImmediateCommand(RouteCommandType.MoveRight);
                    break;

                case RouteControlAction.MoveDown:
                    executor.TryExecuteImmediateCommand(RouteCommandType.MoveDown);
                    break;

                case RouteControlAction.MoveLeft:
                    executor.TryExecuteImmediateCommand(RouteCommandType.MoveLeft);
                    break;

                case RouteControlAction.Wait:
                    executor.TryExecuteImmediateCommand(RouteCommandType.Wait);
                    break;

                case RouteControlAction.Undo:
                    executor.TryUndoLastAction();
                    break;

                case RouteControlAction.Reset:
                    executor.ResetProgressKeepTimer();
                    break;
            }
        }

        private void HandleResetInput()
        {
            bool keyDown = Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Backspace);
            bool keyHeld = Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Backspace);
            bool keyUp = Input.GetKeyUp(KeyCode.R) || Input.GetKeyUp(KeyCode.Backspace);

            if (keyDown)
            {
                _resetHoldActive = true;
                _resetHoldTriggered = false;
                _resetHoldStartedAt = Time.unscaledTime;
            }

            if (_resetHoldActive &&
                !_resetHoldTriggered &&
                keyHeld &&
                Time.unscaledTime - _resetHoldStartedAt >= resetHoldDuration)
            {
                _resetHoldTriggered = true;
                HandleAction(RouteControlAction.Reset);
            }

            if (_resetHoldActive && keyUp)
            {
                if (!_resetHoldTriggered)
                {
                    HandleAction(RouteControlAction.Undo);
                }

                _resetHoldActive = false;
                _resetHoldTriggered = false;
                return;
            }

            if (_resetHoldActive && !keyHeld && !keyDown)
            {
                _resetHoldActive = false;
                _resetHoldTriggered = false;
            }
        }

        private void HandleMouseInput()
        {
            if (!enableMouseInput || executor == null || executor.player == null || executor.Grid == null)
            {
                return;
            }

            HandleMouseResetInput();

            if (!WasPrimaryMousePressedThisFrame())
            {
                return;
            }

            bool hoveredBoardCell = TryGetHoveredCell(out Vector2Int clickedCell);
            if (!hoveredBoardCell && IsPointerOverUi())
            {
                return;
            }

            if (!hoveredBoardCell)
            {
                return;
            }

            Vector2Int playerCell = executor.player.gridPosition;
            float clickTime = Time.unscaledTime;

            if (clickedCell == playerCell)
            {
                bool isDoubleClick =
                    _hasPendingLeftClick &&
                    _pendingLeftClickCell == clickedCell &&
                    clickTime - _pendingLeftClickAt <= doubleClickInterval;

                if (isDoubleClick)
                {
                    HandleAction(RouteControlAction.Wait);
                    _hasPendingLeftClick = false;
                    return;
                }

                _hasPendingLeftClick = true;
                _pendingLeftClickCell = clickedCell;
                _pendingLeftClickAt = clickTime;
                return;
            }

            _hasPendingLeftClick = false;

            if (TryResolveMouseMoveAction(clickedCell, playerCell, out RouteControlAction action))
            {
                HandleAction(action);
            }
        }

        private void HandleMouseResetInput()
        {
            bool mouseDown = WasSecondaryMousePressedThisFrame();
            bool mouseHeld = IsSecondaryMousePressed();
            bool mouseUp = WasSecondaryMouseReleasedThisFrame();

            if (mouseDown)
            {
                bool hoveredBoardCell = TryGetHoveredCell(out _);
                if (!hoveredBoardCell && IsPointerOverUi())
                {
                    _mouseResetHoldActive = false;
                    _mouseResetHoldTriggered = false;
                    return;
                }

                if (hoveredBoardCell)
                {
                    _mouseResetHoldActive = true;
                    _mouseResetHoldTriggered = false;
                    _mouseResetHoldStartedAt = Time.unscaledTime;
                }
                else
                {
                    _mouseResetHoldActive = false;
                    _mouseResetHoldTriggered = false;
                }
            }

            if (_mouseResetHoldActive &&
                !_mouseResetHoldTriggered &&
                mouseHeld &&
                Time.unscaledTime - _mouseResetHoldStartedAt >= resetHoldDuration)
            {
                _mouseResetHoldTriggered = true;
                HandleAction(RouteControlAction.Reset);
            }

            if (_mouseResetHoldActive && mouseUp)
            {
                if (!_mouseResetHoldTriggered)
                {
                    HandleAction(RouteControlAction.Undo);
                }

                _mouseResetHoldActive = false;
                _mouseResetHoldTriggered = false;
                return;
            }

            if (_mouseResetHoldActive && !mouseHeld && !mouseDown)
            {
                _mouseResetHoldActive = false;
                _mouseResetHoldTriggered = false;
            }
        }

        private void EnsureHud()
        {
            if (hud == null)
            {
                hud = GetComponentInChildren<RouteMiniGameHUD>(true);
            }

            if (hud == null)
            {
                hud = FindAnyObjectByType<RouteMiniGameHUD>(FindObjectsInactive.Include);
            }

            if (hud != null)
            {
                hud.Initialize(queue, executor);
            }
        }

        private bool TryGetHoveredCell(out Vector2Int cellPosition)
        {
            cellPosition = Vector2Int.zero;

            if (executor == null || executor.Grid == null)
            {
                return false;
            }

            Camera camera = ResolveInputCamera();
            if (camera == null || !TryReadPointerPosition(out Vector2 pointerPosition))
            {
                return false;
            }

            if (executor.Grid.TryGetGridPositionFromRay(camera.ScreenPointToRay(pointerPosition), out cellPosition))
            {
                return true;
            }

            return executor.Grid.TryGetGridPositionFromScreenPoint(camera, pointerPosition, out cellPosition);
        }

        private Camera ResolveInputCamera()
        {
            if (IsUsableCamera(_inputCamera))
            {
                return _inputCamera;
            }

            if (IsUsableCamera(Camera.main))
            {
                _inputCamera = Camera.main;
                return _inputCamera;
            }

            _inputCamera = FindAnyObjectByType<Camera>();
            return IsUsableCamera(_inputCamera) ? _inputCamera : null;
        }

        private static bool IsUsableCamera(Camera camera)
        {
            return camera != null && camera.isActiveAndEnabled;
        }

        private static bool TryResolveMouseMoveAction(
            Vector2Int clickedCell,
            Vector2Int playerCell,
            out RouteControlAction action)
        {
            action = default;
            Vector2Int delta = clickedCell - playerCell;

            if (delta == Vector2Int.zero)
            {
                return false;
            }

            if (delta.x == 0 && delta.y > 0)
            {
                action = RouteControlAction.MoveUp;
                return true;
            }

            if (delta.x == 0 && delta.y < 0)
            {
                action = RouteControlAction.MoveDown;
                return true;
            }

            if (delta.y == 0 && delta.x > 0)
            {
                action = RouteControlAction.MoveRight;
                return true;
            }

            if (delta.y == 0 && delta.x < 0)
            {
                action = RouteControlAction.MoveLeft;
                return true;
            }

            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            {
                action = delta.x > 0 ? RouteControlAction.MoveRight : RouteControlAction.MoveLeft;
                return true;
            }

            action = delta.y > 0 ? RouteControlAction.MoveUp : RouteControlAction.MoveDown;
            return true;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private static bool WasPrimaryMousePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.leftButton.wasPressedThisFrame;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                return pointer.press.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(0);
        }

        private static bool WasSecondaryMousePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.rightButton.wasPressedThisFrame;
            }
#endif
            return Input.GetMouseButtonDown(1);
        }

        private static bool WasSecondaryMouseReleasedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.rightButton.wasReleasedThisFrame;
            }
#endif
            return Input.GetMouseButtonUp(1);
        }

        private static bool IsSecondaryMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                return mouse.rightButton.isPressed;
            }
#endif
            return Input.GetMouseButton(1);
        }

        private static bool TryReadPointerPosition(out Vector2 position)
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                position = pointer.position.ReadValue();
                return true;
            }
#endif
            position = Input.mousePosition;
            return true;
        }

        private static bool WasPressed(KeyCode primary, KeyCode secondary)
        {
            return Input.GetKeyDown(primary) || Input.GetKeyDown(secondary);
        }

        private void RefreshRuntimeReferences()
        {
            if (queue == null)
            {
                queue = GetComponent<CommandQueue>();
            }

            if (executor == null)
            {
                executor = GetComponent<ExecutionManager>();
            }

            if (executor != null && executor.player == null)
            {
                executor.player = FindAnyObjectByType<PlayerController>();
            }
        }
    }
}
