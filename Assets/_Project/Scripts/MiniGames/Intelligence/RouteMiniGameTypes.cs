using System;
using UnityEngine;

namespace Game
{
    public enum RouteDirection
    {
        Up,
        Right,
        Down,
        Left
    }

    public enum RouteCommandType
    {
        None,
        MoveUp,
        MoveRight,
        MoveDown,
        MoveLeft,
        Wait
    }

    public enum RouteCellType
    {
        Normal = 0,
        Wall = 1,
        Argument = 2,
        Exit = 3,
        Forbidden = 4,
        TimedBarrier = 5
    }

    public enum RouteBoardPlane
    {
        XY,
        XZ
    }

    public enum RouteControlAction
    {
        MoveUp,
        MoveRight,
        MoveDown,
        MoveLeft,
        Wait,
        Undo,
        Reset
    }

    [Serializable]
    public class RouteCommand
    {
        public RouteCommandType Type;
        [Min(1)] public int Value = 1;

        public RouteCommand(RouteCommandType type, int value = 1)
        {
            Type = type;
            Value = Mathf.Max(1, value);
        }

        public override string ToString()
        {
            return Type switch
            {
                RouteCommandType.MoveUp => Value > 1 ? $"Шаг вверх x{Value}" : "Шаг вверх",
                RouteCommandType.MoveRight => Value > 1 ? $"Шаг вправо x{Value}" : "Шаг вправо",
                RouteCommandType.MoveDown => Value > 1 ? $"Шаг вниз x{Value}" : "Шаг вниз",
                RouteCommandType.MoveLeft => Value > 1 ? $"Шаг влево x{Value}" : "Шаг влево",
                RouteCommandType.Wait => Value > 1 ? $"Пауза x{Value}" : "Пауза",
                _ => "Нет"
            };
        }
    }

    public static class RouteDirectionUtility
    {
        public static Vector2Int ToVector2Int(RouteDirection direction)
        {
            return direction switch
            {
                RouteDirection.Up => Vector2Int.up,
                RouteDirection.Right => Vector2Int.right,
                RouteDirection.Down => Vector2Int.down,
                RouteDirection.Left => Vector2Int.left,
                _ => Vector2Int.zero
            };
        }

        public static RouteDirection TurnLeft(RouteDirection direction)
        {
            return direction switch
            {
                RouteDirection.Up => RouteDirection.Left,
                RouteDirection.Left => RouteDirection.Down,
                RouteDirection.Down => RouteDirection.Right,
                RouteDirection.Right => RouteDirection.Up,
                _ => direction
            };
        }

        public static RouteDirection TurnRight(RouteDirection direction)
        {
            return direction switch
            {
                RouteDirection.Up => RouteDirection.Right,
                RouteDirection.Right => RouteDirection.Down,
                RouteDirection.Down => RouteDirection.Left,
                RouteDirection.Left => RouteDirection.Up,
                _ => direction
            };
        }

        public static RouteDirection Opposite(RouteDirection direction)
        {
            return direction switch
            {
                RouteDirection.Up => RouteDirection.Down,
                RouteDirection.Right => RouteDirection.Left,
                RouteDirection.Down => RouteDirection.Up,
                RouteDirection.Left => RouteDirection.Right,
                _ => direction
            };
        }

        public static string ToReadable(RouteDirection direction)
        {
            return direction switch
            {
                RouteDirection.Up => "Вверх",
                RouteDirection.Right => "Вправо",
                RouteDirection.Down => "Вниз",
                RouteDirection.Left => "Влево",
                _ => direction.ToString()
            };
        }

        public static bool IsStepCommand(RouteCommandType type)
        {
            return type == RouteCommandType.MoveUp ||
                   type == RouteCommandType.MoveRight ||
                   type == RouteCommandType.MoveDown ||
                   type == RouteCommandType.MoveLeft;
        }

        public static bool TryGetStepDirection(RouteCommandType type, out RouteDirection direction)
        {
            switch (type)
            {
                case RouteCommandType.MoveUp:
                    direction = RouteDirection.Up;
                    return true;

                case RouteCommandType.MoveRight:
                    direction = RouteDirection.Right;
                    return true;

                case RouteCommandType.MoveDown:
                    direction = RouteDirection.Down;
                    return true;

                case RouteCommandType.MoveLeft:
                    direction = RouteDirection.Left;
                    return true;

                default:
                    direction = RouteDirection.Up;
                    return false;
            }
        }

        public static string CommandReadable(RouteCommandType type)
        {
            return type switch
            {
                RouteCommandType.MoveUp => "Вверх",
                RouteCommandType.MoveRight => "Вправо",
                RouteCommandType.MoveDown => "Вниз",
                RouteCommandType.MoveLeft => "Влево",
                RouteCommandType.Wait => "Пауза",
                _ => "Нет"
            };
        }
    }

    public static class RouteMiniGameIcons
    {
        public static string Command(RouteCommandType type)
        {
            return type switch
            {
                RouteCommandType.MoveUp => "↑",
                RouteCommandType.MoveRight => "→",
                RouteCommandType.MoveDown => "↓",
                RouteCommandType.MoveLeft => "←",
                RouteCommandType.Wait => "⏸",
                _ => "·"
            };
        }

        public static string Action(RouteControlAction action)
        {
            return action switch
            {
                RouteControlAction.MoveUp => "↑",
                RouteControlAction.MoveRight => "→",
                RouteControlAction.MoveDown => "↓",
                RouteControlAction.MoveLeft => "←",
                RouteControlAction.Wait => "⏸",
                RouteControlAction.Undo => "↶",
                RouteControlAction.Reset => "⟲",
                _ => "?"
            };
        }

        public static string ActionLabel(RouteControlAction action)
        {
            return action switch
            {
                RouteControlAction.MoveUp => "Вверх",
                RouteControlAction.MoveRight => "Вправо",
                RouteControlAction.MoveDown => "Вниз",
                RouteControlAction.MoveLeft => "Влево",
                RouteControlAction.Wait => "Пауза",
                RouteControlAction.Undo => "Отмена",
                RouteControlAction.Reset => "Сброс",
                _ => action.ToString()
            };
        }

        public static string ActionKey(RouteControlAction action)
        {
            return action switch
            {
                RouteControlAction.MoveUp => "W / ↑",
                RouteControlAction.MoveRight => "D / →",
                RouteControlAction.MoveDown => "S / ↓",
                RouteControlAction.MoveLeft => "A / ←",
                RouteControlAction.Wait => "Space",
                RouteControlAction.Undo => "R",
                RouteControlAction.Reset => "Mouse",
                _ => string.Empty
            };
        }
    }
}
