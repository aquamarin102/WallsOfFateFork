using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class CommandQueue : MonoBehaviour
    {
        public int maxCommands = 7;
        public List<RouteCommand> Commands = new();

        [Header("Optional Limits")]
        [SerializeField, HideInInspector] private int maxTurnCommands = -1;
        [SerializeField] private int maxWaitCommands = -1;
        [SerializeField] private int maxMoveCommands = -1;
        [SerializeField] private RouteCommandType requiredCommand = RouteCommandType.None;
        [SerializeField] private int requiredCommandCount = 1;

        public event Action Changed;

        public int EffectiveMaxCommands => maxTurnCommands >= 0
            ? Mathf.Max(1, maxTurnCommands)
            : Mathf.Max(1, maxCommands);
        public int MaxWaitCommands => maxWaitCommands;
        public int MaxMoveCommands => maxMoveCommands;
        public RouteCommandType RequiredCommand => requiredCommand;
        public int RequiredCommandCount => requiredCommandCount;

        public bool TryAddCommand(RouteCommandType type, out string reason)
        {
            RouteCommand command = new(type);
            if (!CanAdd(command, out reason))
            {
                return false;
            }

            Commands.Add(command);
            NotifyChanged();
            reason = $"Добавлено: {command}";
            return true;
        }

        public bool CanAddCommand(RouteCommandType type, out string reason)
        {
            return CanAdd(new RouteCommand(type), out reason);
        }

        public bool RemoveLast(out string reason)
        {
            if (Commands.Count == 0)
            {
                reason = "Очередь уже пустая.";
                return false;
            }

            RouteCommand removedCommand = Commands[Commands.Count - 1];
            Commands.RemoveAt(Commands.Count - 1);
            NotifyChanged();
            reason = $"Убрано: {removedCommand}";
            return true;
        }

        public void Clear()
        {
            Commands.Clear();
            NotifyChanged();
        }

        public int CountOfType(RouteCommandType type)
        {
            int count = 0;
            for (int index = 0; index < Commands.Count; index++)
            {
                if (Commands[index].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountMoveCommands()
        {
            int count = 0;
            for (int index = 0; index < Commands.Count; index++)
            {
                if (RouteDirectionUtility.IsStepCommand(Commands[index].Type))
                {
                    count++;
                }
            }

            return count;
        }

        public bool ValidateForRun(out string reason)
        {
            if (Commands.Count == 0)
            {
                reason = "Сначала соберите маршрут.";
                return false;
            }

            int effectiveMaxCommands = EffectiveMaxCommands;
            if (Commands.Count > effectiveMaxCommands)
            {
                reason = $"Лимит команд превышен: {Commands.Count}/{effectiveMaxCommands}.";
                return false;
            }

            if (maxWaitCommands >= 0 && CountOfType(RouteCommandType.Wait) > maxWaitCommands)
            {
                reason = $"Лимит пауз превышен: {CountOfType(RouteCommandType.Wait)}/{maxWaitCommands}.";
                return false;
            }

            if (maxMoveCommands >= 0 && CountMoveCommands() > maxMoveCommands)
            {
                reason = $"Лимит шагов превышен: {CountMoveCommands()}/{maxMoveCommands}.";
                return false;
            }

            if (requiredCommand != RouteCommandType.None)
            {
                int minRequired = Mathf.Max(1, requiredCommandCount);
                if (CountOfType(requiredCommand) < minRequired)
                {
                    reason = $"Нужно использовать '{RouteDirectionUtility.CommandReadable(requiredCommand)}' минимум {minRequired} раз.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public string GetLimitSummary()
        {
            List<string> parts = new()
            {
                $"Команды: {Commands.Count}/{EffectiveMaxCommands}"
            };

            if (maxWaitCommands >= 0)
            {
                parts.Add($"Паузы: {CountOfType(RouteCommandType.Wait)}/{maxWaitCommands}");
            }

            if (maxMoveCommands >= 0)
            {
                parts.Add($"Шаги: {CountMoveCommands()}/{maxMoveCommands}");
            }

            if (requiredCommand != RouteCommandType.None)
            {
                int minRequired = Mathf.Max(1, requiredCommandCount);
                parts.Add($"Обяз.: {RouteDirectionUtility.CommandReadable(requiredCommand)} x{minRequired}");
            }

            return string.Join("\n", parts);
        }

        public void ApplyExternalLimits(
            int? newMaxCommands,
            int? newMaxTurns,
            int? newMaxWaits,
            int? newMaxMoves,
            RouteCommandType? required,
            int? requiredCountOverride)
        {
            if (newMaxCommands.HasValue)
            {
                maxCommands = Mathf.Max(1, newMaxCommands.Value);
            }

            if (newMaxTurns.HasValue)
            {
                maxTurnCommands = newMaxTurns.Value >= 0
                    ? Mathf.Max(1, newMaxTurns.Value)
                    : -1;
            }

            if (newMaxWaits.HasValue)
            {
                maxWaitCommands = newMaxWaits.Value;
            }

            if (newMaxMoves.HasValue)
            {
                maxMoveCommands = newMaxMoves.Value;
            }

            if (required.HasValue)
            {
                requiredCommand = required.Value;
            }

            if (requiredCountOverride.HasValue)
            {
                requiredCommandCount = Mathf.Max(1, requiredCountOverride.Value);
            }

            NotifyChanged();
        }

        private bool CanAdd(RouteCommand command, out string reason)
        {
            int effectiveMaxCommands = EffectiveMaxCommands;
            if (Commands.Count >= effectiveMaxCommands)
            {
                reason = $"Очередь заполнена: {effectiveMaxCommands} команд.";
                return false;
            }

            if (command.Type == RouteCommandType.Wait &&
                maxWaitCommands >= 0 &&
                CountOfType(RouteCommandType.Wait) >= maxWaitCommands)
            {
                reason = $"Лимит пауз достигнут ({maxWaitCommands}).";
                return false;
            }

            if (RouteDirectionUtility.IsStepCommand(command.Type) &&
                maxMoveCommands >= 0 &&
                CountMoveCommands() >= maxMoveCommands)
            {
                reason = $"Лимит шагов достигнут ({maxMoveCommands}).";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
