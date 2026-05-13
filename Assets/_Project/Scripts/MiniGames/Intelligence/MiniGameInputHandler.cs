using UnityEngine;

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

        private bool _resetHoldActive;
        private bool _resetHoldTriggered;
        private float _resetHoldStartedAt;

        private void Awake()
        {
            if (queue == null)
            {
                queue = GetComponent<CommandQueue>();
            }

            if (executor == null)
            {
                executor = GetComponent<ExecutionManager>();
            }
        }

        private void Start()
        {
            EnsureHud();
            HideLegacyBoardUi();
        }

        private void Update()
        {
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

                case RouteControlAction.Run:
                    executor.TryStartRun();
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

        private static void HideLegacyBoardUi()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int index = 0; index < canvases.Length; index++)
            {
                Canvas canvas = canvases[index];
                if (canvas == null)
                {
                    continue;
                }

                Transform buttonsRoot = FindNamedChildRecursive(canvas.transform, "Buttons");
                if (buttonsRoot != null)
                {
                    buttonsRoot.gameObject.SetActive(false);
                }

                Transform queueBox = FindNamedChildRecursive(canvas.transform, "box");
                if (queueBox != null)
                {
                    queueBox.gameObject.SetActive(false);
                }
            }
        }

        private static bool WasPressed(KeyCode primary, KeyCode secondary)
        {
            return Input.GetKeyDown(primary) || Input.GetKeyDown(secondary);
        }

        private static Transform FindNamedChildRecursive(Transform root, string objectName)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == objectName)
            {
                return root;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindNamedChildRecursive(root.GetChild(index), objectName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
