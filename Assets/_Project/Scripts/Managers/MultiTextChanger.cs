using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System.Linq;
using Game.Data;
using Zenject;

namespace Game
{
    public class MultiTextChanger : MonoBehaviour
    {
        [SerializeField] private List<TMP_Text> _textMeshProLinks;
        [SerializeField] private List<GameObject> _iconsLinks;

        [Header("Quest Settings")]
        [SerializeField] private string _defaultTextAllQuests = "Все квесты выполнены! Вы можете закончить день!";
        [SerializeField] private string _defaultTextStillQuests = "Основной квест выполнен! Вы можете пообщаться с другими жителями!";

        private QuestManager questManager;

        [Inject]
        private void Construct([InjectOptional] QuestManager questmanager)
        {
            this.questManager = questmanager;
        }

        private void Update()
        {
            if (!TryResolveQuestManager())
            {
                return;
            }

            UpdateQuestUI();
        }

        private void UpdateQuestUI()
        {
            if (_textMeshProLinks == null || _iconsLinks == null || _textMeshProLinks.Count == 0 || _iconsLinks.Count == 0)
            {
                return;
            }

            var currentQuests = questManager.GetCurrentQuests();

            ResetUI();

            if (currentQuests == null || currentQuests.Count == 0)
            {
                ShowDefaultMessage(_defaultTextAllQuests);
                return;
            }

            bool hasMainQuest = currentQuests.Any(q => q != null && q.Main);

            if (!hasMainQuest)
            {
                ShowDefaultMessage(_defaultTextStillQuests);
                //return;
            }

            int displayCount = Mathf.Min(currentQuests.Count, Mathf.Min(_iconsLinks.Count, _textMeshProLinks.Count));

            for (int i = 0; i < displayCount; i++)
            {
                var quest = currentQuests[i];
                if (quest == null)
                {
                    continue;
                }

                var questStatus = questManager.GetQuestStatus(quest.Id);
                if (questStatus?.TasksStatusData == null)
                {
                    continue;
                }

                var activeTaskStatus = questStatus.TasksStatusData.Values
                    .FirstOrDefault(ts => ts.State == QuestState.InProgress);

                if (activeTaskStatus != null)
                {
                    QuestTask task = questManager.GetQuestTask(quest.Id, activeTaskStatus.TaskId);

                    if (task != null)
                    {
                        _iconsLinks[i].SetActive(true);
                        _textMeshProLinks[i].gameObject.SetActive(true);
                        _textMeshProLinks[i].text = task.Description;
                    }
                }
            }
        }

        private void ShowDefaultMessage(string message)
        {
            if (_textMeshProLinks.Count > 0 && _iconsLinks.Count > 0)
            {
                _iconsLinks[0].SetActive(true); 
                _textMeshProLinks[0].gameObject.SetActive(true);
                _textMeshProLinks[0].text = message;
            }
        }

        private void ResetUI()
        {
            for (int i = 0; i < _iconsLinks.Count; i++)
            {
                if (_iconsLinks[i] != null) _iconsLinks[i].SetActive(false);
            }

            for (int i = 0; i < _textMeshProLinks.Count; i++)
            {
                if (_textMeshProLinks[i] != null) _textMeshProLinks[i].gameObject.SetActive(false);
            }
        }

        private bool TryResolveQuestManager()
        {
            questManager ??= QuestManager.Instance;
            return questManager != null;
        }
    }
}
