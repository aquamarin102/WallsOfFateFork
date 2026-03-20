using Zenject;

namespace Game.Data
{
    public class SaveLoadManager
    {

        private PlayerManager playerManager;
        private QuestManager questManager;
        private GameflowManager gameflowManager;

        [Inject]
        public SaveLoadManager(PlayerManager playerManager, QuestManager questManager, GameflowManager gameflowManager)
        {
            this.playerManager = playerManager;
            this.questManager = questManager;
            this.gameflowManager = gameflowManager;
        }

        public void LoadGame()
        {
            Repository.LoadState();
            questManager.ClearMinigameRuntimeState();
            playerManager.LoadSavedPlayerData();
            questManager.LoadSavedQuestsStatus();
            gameflowManager.LoadSavedGameflowData();
        }

        /// <summary>
        /// Полное сохранение игрового прогресса (в том числе позиции игрока).
        /// Вызывается, например, при сохранении перед выходом или при переходе в меню "Продолжить".
        /// </summary>
        public void SaveGame()
        {
            playerManager.SavePlayerData();
            questManager.SaveQuestsStatus();
            gameflowManager.SaveGameflowData();
            Repository.SetUserProgress(true);
            Repository.SaveState();
        }

        /// <summary>
        /// Проверяет, есть ли сохранённый игровой прогресс.
        /// </summary>
        public bool CanLoad()
        {
            Repository.LoadState();
            return Repository.HasAnyData();
        }

        /// <summary>
        /// Очищает сохранённые данные и устанавливает флаг новой игры.
        /// </summary>
        public void Clear()
        {
            AssembledPickups.Clear();
            questManager.ClearMinigameRuntimeState();
            Repository.ClearSaveData();
            PlayerSpawnData.ClearData();
        }

    }
}
