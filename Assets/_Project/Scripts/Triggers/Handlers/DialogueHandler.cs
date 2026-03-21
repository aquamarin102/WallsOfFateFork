using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly HashSet<InfluenceArea> subscribedAreas = new();

        [Inject]
        private void Construct([InjectOptional] NPCPrefabFactory npcFactory)
        {
            this.npcFactory = npcFactory;
        }

        private void OnEnable()
        {
            SyncDialogueAreaSubscriptions();
        }

        private void Update()
        {
            SyncDialogueAreaSubscriptions();
        }

        private void OnDisable()
        {
            foreach (var area in subscribedAreas.ToArray())
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

        private void SyncDialogueAreaSubscriptions()
        {
            List<InfluenceArea> currentAreas = CollectDialogueAreas()
                .Where(area => area != null)
                .Distinct()
                .ToList();

            for (int index = 0; index < currentAreas.Count; index++)
            {
                InfluenceArea area = currentAreas[index];
                if (subscribedAreas.Add(area))
                {
                    area.OnEventTriggered.Subscribe(HandleAsync);
                }
            }

            foreach (var subscribedArea in subscribedAreas.ToArray())
            {
                if (subscribedArea == null || !currentAreas.Contains(subscribedArea))
                {
                    if (subscribedArea != null)
                    {
                        subscribedArea.OnEventTriggered.Unsubscribe(HandleAsync);
                    }

                    subscribedAreas.Remove(subscribedArea);
                }
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
                    if (npc == null)
                    {
                        continue;
                    }

                    var area = npc.GetComponentInChildren<InfluenceArea>();
                    if (area != null)
                    {
                        yield return area;
                    }
                }
            }

            GameObject[] rootObjects = gameObject.scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
            {
                InfluenceArea[] areasInScene = rootObjects[rootIndex].GetComponentsInChildren<InfluenceArea>(true);
                for (int areaIndex = 0; areaIndex < areasInScene.Length; areaIndex++)
                {
                    InfluenceArea area = areasInScene[areaIndex];
                    if (area != null && area.AreaType == InfluenceType.Dialog)
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
