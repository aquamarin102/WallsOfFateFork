using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game.Data
{
    public class GameflowManager
    {
        public Day CurrentDay { get; private set; }

        private Dictionary<int, List<int>> QuestsByDay;

        private QuestManager questManager;

        [Inject]
        private void Construct(QuestManager questManager)
        {
            this.questManager = questManager;
        }

        public GameflowManager()
        {
            LoadQuestsByDayData();
            // Инициализируем первый день при старте игры
            AdvanceCurrentDay();
        }

        private void LoadQuestsByDayData()
        {
            TextAsset textAsset = Resources.Load<TextAsset>("Data/QuestsByDay");
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

                QuestsByDay = JsonConvert.DeserializeObject<Dictionary<int, List<int>>>(textAsset.text, settings);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"JSON error: {ex.Message}");
            }
        }

        public void AdvanceCurrentDay()
        {
            if (CurrentDay == null)
            {
                CurrentDay = new Day
                {
                    Id = 1,
                    CurrentQuestId = null
                };
            }
            else
            {
                CurrentDay = new Day
                {
                    Id = CurrentDay.Id + 1,
                    CurrentQuestId = null
                };
            }
        }

        public List<Quest> GetCurrentDayQuests()
        {
            var questIds = QuestsByDay[CurrentDay.Id];

            return questIds.ConvertAll(id => questManager.GetQuest(id));
        }

        public void LoadSavedGameflowData()
        {
            if (Repository.TryGetData("GameflowData", out Day data))
            {
                CurrentDay = data;
            }
        }

        public void SaveGameflowData()
        {
            Repository.SetData("GameflowData", CurrentDay);
        }

        public void StartRandomQuest()
        {
            if (CurrentDay.CurrentQuestId != null) return; // Уже есть активный квест
            var questsForCurrentDay = QuestsByDay[CurrentDay.Id].FindAll(questId => questManager.GetQuestState(questId) != QuestState.NotStarted);

            if (questsForCurrentDay.Count == 0)
            {
                Debug.LogWarning($"No quests available for day {CurrentDay.Id}");
                return;
            }

            int randomIndex = Random.Range(0, questsForCurrentDay.Count);
            CurrentDay.CurrentQuestId = questsForCurrentDay[randomIndex];

            questManager.UpdateQuest(CurrentDay.CurrentQuestId.Value, QuestState.InProgress);
        }
    }
}
