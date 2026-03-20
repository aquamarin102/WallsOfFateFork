using Ink.Parsed;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Data
{

    public class QuestManager
    {
        private Dictionary<int, Quest> questsData;
        private readonly Dictionary<int, QuestStatus> questsStatusData = new();

        public QuestManager()
        {
            LoadResourcesData();
            InitializeQuestsStatus();
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
            }
        }

        public Quest GetQuest(int questId)
        {
            return questsData.TryGetValue(questId, out Quest quest) ? quest : null;
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
            if(questsData.TryGetValue(questId, out Quest quest))
            {
                return quest.Tasks.FirstOrDefault(t => t.Id == taskId);
            }
            return null;
        }
    }
}
