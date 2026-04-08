using System;
using UnityEngine;

namespace Game
{
    [Serializable]
    public class DoorParameters
    {
        public string SceneName;
        public Vector3 SpawnPosition;
        public Vector3 SpawnEulerAngles;
        public int DayNumber = -1; // -1 означает любой день
    }
}
