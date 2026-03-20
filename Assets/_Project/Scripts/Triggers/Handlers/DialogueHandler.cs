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

        private readonly HashSet<InfluenceArea> subscribedAreas = new();
        private NPCPrefabFactory npcFactory;

        [Inject]
        private void Construct(NPCPrefabFactory npcFactory)
        {
            this.npcFactory = npcFactory;
        }

        private void Start()
        {
            SubscribeToDialogueAreas();
        }

        private void OnEnable()
        {
            SubscribeToDialogueAreas();
        }

        private void OnDisable()
        {
            foreach (var area in subscribedAreas)
            {
                if (area != null)
                {
                    area.OnEventTriggered.Unsubscribe(HandleAsync);
                }
            }

            subscribedAreas.Clear();
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
                OnDialogHandled?.Invoke(eventData);
                return;
            }

            DialogueGraph dialogueGraph = GetDialogueGraph(eventData.TriggerObj, eventData.Parameters);
            if (dialogueGraph == null)
            {
                Debug.LogWarning(
                    $"DialogueHandler: no DialogueGraph found for trigger '{eventData.TriggerObj?.name}'. " +
                    "Check the scene or prefab setup and the triggerObject reference.",
                    eventData.TriggerObj);
                return;
            }

            dialogueManager.StartDialogue(dialogueGraph);
            await Task.CompletedTask;
        }

        private void SubscribeToDialogueAreas()
        {
            foreach (var area in CollectDialogueAreas())
            {
                if (area == null || !subscribedAreas.Add(area))
                {
                    continue;
                }

                area.OnEventTriggered.Subscribe(HandleAsync);
            }

            if (subscribedAreas.Count == 0)
            {
                Debug.LogWarning("DialogueHandler: no dialogue influence areas found.", this);
            }
        }

        private IEnumerable<InfluenceArea> CollectDialogueAreas()
        {
            foreach (var area in influenceArias)
            {
                if (IsDialogueArea(area))
                {
                    yield return area;
                }
            }

            if (npcFactory != null)
            {
                foreach (var npc in npcFactory.instances.Values)
                {
                    if (npc == null)
                    {
                        continue;
                    }

                    foreach (var area in npc.GetComponentsInChildren<InfluenceArea>(true))
                    {
                        if (IsDialogueArea(area))
                        {
                            yield return area;
                        }
                    }
                }
            }

            foreach (var area in FindObjectsOfType<InfluenceArea>(true))
            {
                if (IsDialogueArea(area))
                {
                    yield return area;
                }
            }
        }

        private static bool IsDialogueArea(InfluenceArea area)
        {
            return area != null
                   && area.AreaType == InfluenceType.Dialog
                   && area.GetType() == typeof(InfluenceArea);
        }

        private DialogueGraph GetDialogueGraph(GameObject obj, string dialogueName)
        {
            string dialoguePath = "Dialogues/NPC/" + obj.name.ToLower() + "/" + dialogueName;

            TextAsset textAsset = Resources.Load<TextAsset>(dialoguePath);
            if (textAsset == null)
            {
                Debug.LogError($"StartDayDialogueHandler: Failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }

            try
            {
                var dialogueGraph = JsonConvert.DeserializeObject<DialogueGraph>(textAsset.text);
                return dialogueGraph;
            }
            catch (JsonException ex)
            {
                Debug.LogError($"StartDayDialogueHandler: Failed to load dialogue graph at path: {dialoguePath}");
                return null;
            }
        }
    }
}
