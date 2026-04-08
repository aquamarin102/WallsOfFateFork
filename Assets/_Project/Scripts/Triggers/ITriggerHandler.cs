using UnityEngine;

namespace Game
{
    public interface ITriggerHandler
    {
        public void Handle(TriggerEvent eventData);
    }
}