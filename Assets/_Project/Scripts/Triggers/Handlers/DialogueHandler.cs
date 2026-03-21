using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace Game
{
    internal class DialogueHandler : MonoBehaviour
    {
        [Header("Dialogue Settings")]
        [SerializeField] private List<InfluenceArea> influenceArias = new();

        public Action<TriggerEvent> OnDialogHandled;

        private NPCPrefabFactory npcFactory;

        [Inject]
        private void Construct(NPCPrefabFactory npcFactory)
        {
            this.npcFactory = npcFactory;
        }

        private void OnEnable()
        {
            SubscribeToDialogueAreas();
        }

        private void OnDisable()
        {
            UnsubscribeFromDialogueAreas();
        }

        public async Task HandleAsync(TriggerEvent eventData)
        {
            if (eventData.AreaType != InfluenceType.Dialog || !eventData.IsEnteracted)
            {
                return;
            }

            DialogueManager dialogueManager = DialogueManager.Instance;
            if (dialogueManager == null || dialogueManager.IsInDialogue)
            {
                return;
            }

            DialogueGraph dialogueGraph = GetDialogueGraph(eventData.TriggerObj, eventData.Parameters);
            if (dialogueGraph == null)
            {
                OnDialogHandled?.Invoke(eventData);
                return;
            }

            dialogueManager.StartDialogue(dialogueGraph);
            await Task.CompletedTask;
        }

        private void SubscribeToDialogueAreas()
        {
            foreach (var area in CollectDialogueAreas())
            {
                area.OnEventTriggered.Subscribe(HandleAsync);
            }
        }

        private void UnsubscribeFromDialogueAreas()
        {
            foreach (var area in CollectDialogueAreas())
            {
                area.OnEventTriggered.Unsubscribe(HandleAsync);
            }
        }

        private IEnumerable<InfluenceArea> CollectDialogueAreas()
        {
            foreach (var area in influenceArias)
            {
                if (area != null)
                {
                    yield return area;
                }
            }

            if (npcFactory != null)
            {
                foreach (var npc in npcFactory.instances.Values)
                {
                    if (npc == null) continue;

                    var area = npc.GetComponentInChildren<InfluenceArea>();
                    if (area != null)
                    {
                        yield return area;
                    }
                }
            }
        }

        private DialogueGraph GetDialogueGraph(GameObject obj, string dialogueName)
        {
            string dialoguePath = "Dialogues/NPC/" + obj.name.ToLower() + "/" + dialogueName;

            TextAsset textAsset = Resources.Load<TextAsset>(dialoguePath);
            if (textAsset == null)
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<DialogueGraph>(textAsset.text);
            }
            catch
            {
                return null;
            }
        }
    }
}