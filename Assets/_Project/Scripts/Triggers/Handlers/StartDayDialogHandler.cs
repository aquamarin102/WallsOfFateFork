using Game.Data;
using Newtonsoft.Json;
using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    internal class StartDayDialogHandler : MonoBehaviour, ITriggerHandler
    {
        [SerializeField] private StartDayDialogTriggerZone influenceAria;

        private void OnEnable()
        {
            influenceAria.OnEventTriggered += Handle;
        }


        private void OnDisable()
        {
            influenceAria.OnEventTriggered -= Handle;
        }

        public void Handle(TriggerEvent eventData)
        {
            DialogGraph dialogueGraph;
            DialogManager _dialogueManager = DialogManager.Instance;

            if (_dialogueManager == null || _dialogueManager.Active)
                return;

            GameObject npc = ResolveNpcObject(eventData);
            if (npc == null)
                return;

            dialogueGraph = GetDialogueGraph(npc);
            if (dialogueGraph != null)
            {
                _dialogueManager.StartDialog(dialogueGraph);
            }
        }

        private static GameObject ResolveNpcObject(TriggerEvent eventData)
        {
            if (eventData.IsEnteracted)
                return eventData.TriggerObj != null ? eventData.TriggerObj : eventData.PlayerObj;

            return eventData.PlayerObj != null ? eventData.PlayerObj : eventData.TriggerObj;
        }

        private DialogGraph GetDialogueGraph(GameObject obj)
        {
            string dialoguePath = "Dialogues/StartDay/" + obj.name.ToLower();

            TextAsset textAsset = Resources.Load<TextAsset>(dialoguePath);
            if (textAsset == null)
            {
                Debug.LogError($"StartDayDialogueHandler: Failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }

            try
            {
                var dialogueGraph = JsonConvert.DeserializeObject<DialogGraph>(textAsset.text);
                return dialogueGraph;
            }
            catch
            {
                Debug.LogError($"StartDayDialogueHandler: Failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }
        }
    }
}

