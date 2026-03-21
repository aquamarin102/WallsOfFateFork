using Newtonsoft.Json;
using System;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public class ResourcesData
    {
        public int Gold;
        public int Food;
        public int PeopleSatisfaction;
        public int CastleStrength;
    }

    public class PlayerManager
    {
        public static PlayerManager Instance { get; private set; }

        public Player PlayerData { get; private set; } = new Player();

        public PlayerManager() { 
            Instance = this;
            PlayerData.FreePoints = 5; // Начальное количество очков для распределения

            LoadDefaultPlayerData();
        }

        public void LoadSavedPlayerData()
        {
            if (Repository.TryGetData("GameResources", out ResourcesData data))
            {
                PlayerData.SetResource(ResourceType.Gold, data.Gold);
                PlayerData.SetResource(ResourceType.Food, data.Food);
                PlayerData.SetResource(ResourceType.PeopleSatisfaction, data.PeopleSatisfaction);
                PlayerData.SetResource(ResourceType.CastleStrength, data.CastleStrength);

                Debug.Log("Loaded resources data");
            }
        }

        public void SavePlayerData()
        {
            var data = new ResourcesData
            {
                Gold = PlayerData.GetResource(ResourceType.Gold),
                Food = PlayerData.GetResource(ResourceType.Food),
                PeopleSatisfaction = PlayerData.GetResource(ResourceType.PeopleSatisfaction),
                CastleStrength = PlayerData.GetResource(ResourceType.CastleStrength)
            };
            Repository.SetData("GameResources", data);
            Debug.Log("Saved resources data");
        }

        private void LoadDefaultPlayerData()
        {
            TextAsset textAsset = Resources.Load<TextAsset>("Data/DefaultResources");
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

                var defaultData = JsonConvert.DeserializeObject<ResourcesData>(textAsset.text, settings);

                PlayerData.SetResource(ResourceType.Gold, defaultData.Gold);
                PlayerData.SetResource(ResourceType.Food, defaultData.Food);
                PlayerData.SetResource(ResourceType.PeopleSatisfaction, defaultData.PeopleSatisfaction);
                PlayerData.SetResource(ResourceType.CastleStrength, defaultData.CastleStrength);
            }
            catch (JsonException ex)
            {
                Debug.LogError($"JSON error: {ex.Message}");
            }
        }

        public void SaveData()
        {
            var data = new ResourcesData
            {
                Gold = PlayerData.GetResource(ResourceType.Gold),
                Food = PlayerData.GetResource(ResourceType.Food),
                PeopleSatisfaction = PlayerData.GetResource(ResourceType.PeopleSatisfaction),
                CastleStrength = PlayerData.GetResource(ResourceType.CastleStrength)
            };
            Repository.SetData("GameResources", data);
            Debug.Log("Saved resources data");
        }

        public void IncreaseStat(StatType stat)
        {
            if (PlayerData.FreePoints > 0 && PlayerData.GetStat(stat) < 4)
            {
                PlayerData.AddFreePoints(-1);
                PlayerData.AddStat(stat, 1);
            }
        }

        public void DecreaseStat(StatType stat)
        {
            if (PlayerData.GetStat(stat) > 0)
            {
                PlayerData.AddFreePoints(1);
                PlayerData.AddStat(stat, -1);
            }
        }
    }
}
