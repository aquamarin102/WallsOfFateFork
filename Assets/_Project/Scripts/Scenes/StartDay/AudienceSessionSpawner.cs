using Game;
using Game.Data;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

public class AudienceSessionSpawner : MonoBehaviour
{
    /* ───── точки на сцене ───── */
    [Header("Points")]
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private Transform _dialogSpot;
    [SerializeField] private Transform _exitSpot;
    [SerializeField] private DialogueManager _dialogueManager;

    private readonly Queue<NPCDefinition> _sessionQueue = new();
    private NPCController currentNpc;

    /* ───────────────────────────────────────────────────────────── */

    private LoadingManager loadingManager;
    private GameflowManager gameflowManager;
    private QuestManager questManager;

    [Inject]
    private void Construct(LoadingManager loadingManager, GameflowManager gameflowManager, QuestManager questManager)
    {
        this.loadingManager = loadingManager;
        this.gameflowManager = gameflowManager;
        this.questManager = questManager;
    }

    private void Start()
    {
        PrepareQueueAndStart();
    }

    /* ─────────── подготовка очереди ─────────── */
    private void PrepareQueueAndStart()
    {
        var advisor = Resources.Load<NPCDefinition>("Characters/StartDayPrefabs/Advisor");
        _sessionQueue.Enqueue(advisor);

        /*for (int i = 0; i < 1; i++)
            if (AudiencePool.Instance.TryTakeRandom(out var def))
                _sessionQueue.Enqueue(def);*/

        // var mainQuest = gameflowManager.GetCurrentDayQuests().FirstOrDefault(quest => quest.Main);

        /*if (mainQuest != null)
        {
            var mainQuestGiver = Resources.Load<NPCDefinition>("Characters/StartDayPrefabs/ScriptableObjects/Quest_" + mainQuest.Id + "_npc");
            _sessionQueue.Enqueue(mainQuestGiver);
        }*/

        _sessionQueue.Enqueue(Resources.Load<NPCDefinition>("Characters/StartDayPrefabs/person1"));
        _sessionQueue.Enqueue(Resources.Load<NPCDefinition>("Characters/StartDayPrefabs/person2"));
        _sessionQueue.Enqueue(Resources.Load<NPCDefinition>("Characters/StartDayPrefabs/keymaster"));

        _dialogueManager.OnFinished += OnDialogueFinished;
        SpawnNext();
    }

    /* ─────────── спавн одного НПС ─────────── */
    private void SpawnNext()
    {
        if (_sessionQueue.Count == 0)
        {
            currentNpc = null;
            return;
        }

        NPCDefinition def = _sessionQueue.Dequeue();
        GameObject go = Instantiate(def.prefab, _spawnPoint.position, _spawnPoint.rotation);
        go.name = def.npcName;

        currentNpc = go.GetComponent<NPCController>();
        currentNpc.Init(_dialogSpot, _exitSpot);

        currentNpc.Left += OnNpcLeft;
    }

    /* ─────────── колбэки ─────────── */
    private void OnDialogueFinished(DialogueGraph dialogue)
    {
        if (dialogue.Name == "Keymaster")
        {
            var advisorDialogueJson = Resources.Load<TextAsset>("Dialogues/StartDay/Advisor/cellar_quest_start");
            var advisorDialogue = JsonConvert.DeserializeObject<DialogueGraph>(advisorDialogueJson.text);
            _dialogueManager.StartDialogue(advisorDialogue);


            Quest keyMasterQuest = questManager.GetQuest(3);
            questManager.UpdateQuest(keyMasterQuest.Id, QuestState.InProgress);

            QuestTask task = questManager.GetQuestTask(keyMasterQuest.Id, 0);
            questManager.UpdateQuestTask(keyMasterQuest.Id, task.Id, QuestState.InProgress);

        }

        if (dialogue.Name == "Cellar_Quest_Start")
        {
            loadingManager.LoadSceneAsync("MainRoom");
        }

        if (currentNpc != null)
        {
            currentNpc?.Leave();
        }
    }

    private void OnNpcLeft(NPCController npc)
    {
        npc.Left -= OnNpcLeft;
        Destroy(npc.gameObject, 0.3f);
        SpawnNext();
    }
}
