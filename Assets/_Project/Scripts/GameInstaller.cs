using Game.Data;
using Zenject;

namespace Game
{
    public class GameInstaller: MonoInstaller
    {
        public override void InstallBindings()
        {
            Container.BindFactory<SceneInteractableManager, SceneInteractableManagerFactory>();
            Container.Bind<LocalizationManager>().AsSingle().NonLazy();
            Container.Bind<LoadingManager>().AsSingle().NonLazy();
            Container.Bind<PlayerManager>().AsSingle().NonLazy();
            Container.Bind<QuestManager>().AsSingle().NonLazy();
            Container.Bind<GameflowManager>().AsSingle().NonLazy();
            Container.Bind<SaveLoadManager>().AsSingle().NonLazy();
            //Container.Bind<QuestManager>().AsSingle().NonLazy();
        }
    }
}
