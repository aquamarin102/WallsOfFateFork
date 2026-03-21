using Ink.Parsed;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Data
{
    public class QuestManager
    {
        public static QuestManager Instance { get; private set; }
        public event Action<bool> AllQuestsCompletedStateChanged;

        private sealed class MinigameContext
        {
            public int QuestId;
            public string WinDialoguePath;
            public string LoseDialoguePath;
        }

        private sealed class PendingMinigameDialogue
        {
            public int QuestId;
            public string DialoguePath;
            public bool PlayerWon;
        }

        private Dictionary<int, Quest> questsData;
        private readonly Dictionary<int, QuestStatus> questsStatusData = new();
        private static readonly Dictionary<int, bool> questsMinigameResults = new();
        private static MinigameContext pendingMinigameContext;
        private static PendingMinigameDialogue pendingMinigameDialogue;
        private bool areAllQuestsCompleted;

        public QuestManager()
        {
            Instance = this;
            LoadResourcesData();
            InitializeQuestsStatus();
            RefreshAllQuestsCompletedState();
        }

        private void LoadResourcesData()
        {
            TextAsset textAsset = Resources.Load<TextAsset>("Data/Quests");
            if (textAsset == null)
            {
                Debug.LogError("Default resources file not found!");
                return;
            }

            try
            {
                var settings = new JsonSerializerSettings
                {
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Error
                };

                var defaultData = JsonConvert.DeserializeObject<List<Quest>>(textAsset.text, settings);
                questsData = defaultData.ToDictionary(q => q.Id);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"JSON error: {ex.Message}");
            }
        }

        private void InitializeQuestsStatus()
        {
            foreach (var quest in questsData.Values)
            {
                questsStatusData.Add(quest.Id, new QuestStatus
                {
                    QuestId = quest.Id,
                    State = QuestState.NotStarted,
                    TasksStatusData = quest.Tasks.ToDictionary(t => t.Id, t => new TaskStatus
                    {
                        TaskId = t.Id,
                        State = QuestState.NotStarted
                    })
                });
            }
        }

        public void LoadSavedQuestsStatus()
        {
            if (Repository.TryGetData("QuestsStatus", out List<QuestStatus> savedStatus))
            {
                foreach (var status in savedStatus)
                {
                    questsStatusData[status.QuestId].State = status.State;
                    foreach (var taskStatus in status.TasksStatusData)
                    {
                        questsStatusData[status.QuestId].TasksStatusData[taskStatus.Key].State = taskStatus.Value.State;
                    }
                }

                Debug.Log("Loaded quests status data");
            }

            RefreshAllQuestsCompletedState();
        }

        public void SaveQuestsStatus()
        {
            Repository.SetData("QuestsStatus", questsStatusData.Values.ToList());
            Debug.Log("Saved quests status data");
        }

        public QuestState GetQuestState(int questId)
        {
            questsStatusData.TryGetValue(questId, out QuestStatus status);

            return status != null ? status.State : QuestState.NotStarted;
        }

        public QuestStatus GetQuestStatus(int questId)
        {
            return questsStatusData[questId];
        }

        public void UpdateQuest(int questId, QuestState state)
        {
            if (questsStatusData.TryGetValue(questId, out QuestStatus status))
            {
                status.State = state;
                RefreshAllQuestsCompletedState();
            }
        }

        public Quest GetQuest(int questId)
        {
            return questsData.TryGetValue(questId, out Quest quest) ? quest : null;
        }

        public bool AreAllQuestsCompleted()
        {
            return questsData != null &&
                   questsData.Count > 0 &&
                   questsStatusData.Count == questsData.Count &&
                   questsStatusData.Values.All(status => status.State == QuestState.Completed);
        }

        public List<Quest> GetCurrentQuests()
        {
            return questsStatusData.Where(q => q.Value.State == QuestState.InProgress)
                .Select(q => GetQuest(q.Key))
                .ToList();
        }

        public void UpdateQuestTask(int questId, int taskId, QuestState state)
        {
            if (questsStatusData.TryGetValue(questId, out QuestStatus status))
            {
                status.TasksStatusData[taskId].State = state;
            }
        }

        public QuestTask GetQuestTask(int questId, int taskId)
        {
            if (questsData.TryGetValue(questId, out Quest quest))
            {
                return quest.Tasks.FirstOrDefault(t => t.Id == taskId);
            }

            return null;
        }

        public void RegisterMinigameContext(MiniGameData gameData)
        {
            pendingMinigameContext = null;

            if (gameData?.customParameters == null)
            {
                return;
            }

            if (!TryGetIntParameter(gameData.customParameters, "QuestId", out int questId))
            {
                return;
            }

            pendingMinigameContext = new MinigameContext
            {
                QuestId = questId,
                WinDialoguePath = GetStringParameter(gameData.customParameters, "WinDialogue"),
                LoseDialoguePath = GetStringParameter(gameData.customParameters, "LoseDialogue")
            };
        }

        public void CompletePendingMinigame(bool playerWon)
        {
            if (pendingMinigameContext == null)
            {
                return;
            }

            questsMinigameResults[pendingMinigameContext.QuestId] = playerWon;

            string dialoguePath = playerWon
                ? pendingMinigameContext.WinDialoguePath
                : pendingMinigameContext.LoseDialoguePath;

            if (!string.IsNullOrWhiteSpace(dialoguePath))
            {
                pendingMinigameDialogue = new PendingMinigameDialogue
                {
                    QuestId = pendingMinigameContext.QuestId,
                    DialoguePath = dialoguePath,
                    PlayerWon = playerWon
                };
            }
            else
            {
                pendingMinigameDialogue = null;
            }

            pendingMinigameContext = null;
        }

        public bool TryGetQuestMinigameResult(int questId, out bool playerWon)
        {
            return questsMinigameResults.TryGetValue(questId, out playerWon);
        }

        public bool TryConsumePendingMinigameDialogue(int questId, out string dialoguePath, out bool playerWon)
        {
            if (pendingMinigameDialogue != null && pendingMinigameDialogue.QuestId == questId)
            {
                dialoguePath = pendingMinigameDialogue.DialoguePath;
                playerWon = pendingMinigameDialogue.PlayerWon;
                pendingMinigameDialogue = null;
                return !string.IsNullOrWhiteSpace(dialoguePath);
            }

            dialoguePath = null;
            playerWon = false;
            return false;
        }

        public void ClearMinigameRuntimeState()
        {
            questsMinigameResults.Clear();
            pendingMinigameContext = null;
            pendingMinigameDialogue = null;
        }

        private void RefreshAllQuestsCompletedState()
        {
            bool newValue = AreAllQuestsCompleted();
            if (newValue == areAllQuestsCompleted)
            {
                return;
            }

            areAllQuestsCompleted = newValue;
            AllQuestsCompletedStateChanged?.Invoke(areAllQuestsCompleted);
        }

        private static bool TryGetIntParameter(Dictionary<string, object> parameters, string key, out int value)
        {
            value = default;

            if (parameters == null || !parameters.TryGetValue(key, out object rawValue) || rawValue == null)
            {
                return false;
            }

            switch (rawValue)
            {
                case int intValue:
                    value = intValue;
                    return true;
                case long longValue when longValue >= int.MinValue && longValue <= int.MaxValue:
                    value = (int)longValue;
                    return true;
                case string stringValue when int.TryParse(stringValue, out int parsedValue):
                    value = parsedValue;
                    return true;
                case JValue jValue when jValue.Type == JTokenType.Integer:
                    value = jValue.ToObject<int>();
                    return true;
                case JValue jValue when jValue.Type == JTokenType.String &&
                                        int.TryParse(jValue.ToObject<string>(), out int parsedJValue):
                    value = parsedJValue;
                    return true;
                default:
                    return false;
            }
        }

        private static string GetStringParameter(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out object rawValue) || rawValue == null)
            {
                return null;
            }

            if (rawValue is string stringValue)
            {
                return stringValue;
            }

            if (rawValue is JValue jValue)
            {
                return jValue.ToObject<string>();
            }

            return rawValue.ToString();
        }
    }
}
