using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class Sentence
    {
        #region GraphVariables 
        public int Id;
        public bool IsPlayer = false;
        public string Text;
        public int NextSentenceId = -1;
        public bool IsOption = false;
        #endregion

        #region GameVariables
        public bool StartMinigame = false;
        public MiniGame.MiniGameType MiniGameType = MiniGame.MiniGameType.None;
        public string MiniGameSceneName = "";

        [SerializeField, TextArea(3, 5)] private string _parametersJson = "{}";

        private Dictionary<string, object> _cachedParams;
        public Dictionary<string, object> MinigameParams
        {
            get
            {
                if (_cachedParams == null || _cachedParams.Count == 0)
                {
                    try
                    {
                        _cachedParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(_parametersJson);
                    }
                    catch
                    {
                        _cachedParams = new Dictionary<string, object>();
                    }
                }
                return _cachedParams;
            }
            set
            {
                _cachedParams = value;
                _parametersJson = JsonConvert.SerializeObject(value, Formatting.Indented);
            }
        }

        #region Resources
        public int Gold = 0;
        public int Food = 0;
        public int PeopleSatisfaction = 0;
        public int CastleStrength = 0;
        #endregion

        #endregion
        public Sentence(int _id, bool _IsPlayer, string _Text)
        {
            Id = _id;
            IsPlayer = _IsPlayer;
            Text = _Text;
        }

        public Sentence() { }
    }

    [Serializable]
    public class DialogueGraph
    {
        public string Name;

        public string CharacterName;

        public string Portrait;

        public List<Sentence> Sentences = new();
    }
}