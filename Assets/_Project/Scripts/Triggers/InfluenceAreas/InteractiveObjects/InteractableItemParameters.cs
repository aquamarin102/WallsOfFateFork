using Game.Data;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game
{
    [System.Serializable]
    public class InteractableItemParameters
    {
        public ResourceType ResourceType = ResourceType.Gold;
        public int Amount = 1;
        public string Message = "+1 resource";
        public string Key = "key";
        public string LocalizationFileName = "interactive-items";
        public Vector3 SpawnOffset = new Vector3(0f, 2.5f, 0f);
        public bool DestroyAfterUse = true;
        public float ApproachDistance = 1.2f;
        public bool DependFromQuests = false;
        public List<int> RequiredQuestIds = new List<int>();
        public string ItemName = "";
        public int questId = -1; // от какого квеста зависит 
        public int questTaskId = -1; // от какого таска зависит 
    }
}
