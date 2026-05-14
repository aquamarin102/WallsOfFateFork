using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.MiniGame.PowerCheck
{
    public class GameProcess : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MineSpawner _mineSpawner;
        [SerializeField] private PlayerMove _playerMove;
        [SerializeField] private AIController _enemyController;

        private IReadOnlyList<Mine> _healMines;
        private IReadOnlyList<Mine> _damageMines;
        private IReadOnlyList<Mine> _buffMines;
        private IReadOnlyList<Mine> _debuffMines;

        public MiniGamePlayer PlayerChar { get; private set; }
        public MiniGamePlayer EnemyChar { get; private set; }

        private FXManager playerFX;
        private FXManager enemyFX;
        private CancellationTokenSource _gameplayCancellation;

        private bool _isInitialized;
        private bool _gameEnded;

        public event Action<bool> OnEndGame;

        public void Initialize(FXManager playerFX, FXManager enemyFx)
        {
            if (_isInitialized) return;

            this.playerFX = playerFX;
            enemyFX = enemyFx;
            _gameplayCancellation = new CancellationTokenSource();

            _mineSpawner ??= FindAnyObjectByType<MineSpawner>();
            _playerMove ??= FindAnyObjectByType<PlayerMove>();
            _enemyController ??= FindAnyObjectByType<AIController>();

            InitializeLogic();
            _isInitialized = true;
        }

        private void InitializeLogic()
        {
            if (_playerMove == null || _enemyController == null)
            {
                Debug.LogError("Не удалось найти PlayerMove или AIController!");
                return;
            }

            PlayerChar = _playerMove.GetComponent<MiniGamePlayer>();
            EnemyChar = _enemyController.GetComponent<MiniGamePlayer>();

            if (PlayerChar != null)
            {
                PlayerChar.OnSpeedChanged -= _playerMove.ChangeSpeed;
                PlayerChar.OnSpeedChanged += _playerMove.ChangeSpeed;
            }

            if (EnemyChar != null)
            {
                EnemyChar.OnSpeedChanged -= _enemyController.ChangeSpeed;
                EnemyChar.OnSpeedChanged += _enemyController.ChangeSpeed;
            }

            _healMines = _mineSpawner.HealMines;
            _damageMines = _mineSpawner.DamageMines;
            _buffMines = _mineSpawner.BuffMines;
            _debuffMines = _mineSpawner.DebuffMines;

            SubscribeToMineEvents(_healMines);
            SubscribeToMineEvents(_damageMines);
            SubscribeToMineEvents(_buffMines);
            SubscribeToMineEvents(_debuffMines);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _gameEnded || PlayerChar == null || EnemyChar == null)
            {
                return;
            }

            if ((PlayerChar.Health <= 0 && !PlayerChar.isDead) ||
                (EnemyChar.Health <= 0 && !EnemyChar.isDead))
            {
                bool playerWin = PlayerChar.Health > 0;

                PlayerChar.isDead = true;
                EnemyChar.isDead = true;
                _gameEnded = true;
                CancelPendingGameplay();

                OnEndGame?.Invoke(playerWin);
            }
        }

        private void SubscribeToMineEvents(IEnumerable<Mine> mines)
        {
            if (mines == null) return;

            foreach (Mine mine in mines)
            {
                GameObject minePrefab = mine.MineGameObject;
                if (minePrefab == null) continue;

                TriggerHandler mineTriggerHandler = minePrefab.GetComponent<TriggerHandler>();
                if (mineTriggerHandler != null)
                {
                    mineTriggerHandler.OnObjectEnteredTrigger -= HandleMineTrigger;
                    mineTriggerHandler.OnObjectEnteredTrigger += HandleMineTrigger;
                }
            }
        }

        private void UnsubscribeFromMineEvents(IEnumerable<Mine> mines)
        {
            if (mines == null) return;

            foreach (Mine mine in mines)
            {
                GameObject minePrefab = mine.MineGameObject;
                if (minePrefab == null) continue;

                TriggerHandler mineTriggerHandler = minePrefab.GetComponent<TriggerHandler>();
                if (mineTriggerHandler != null)
                {
                    mineTriggerHandler.OnObjectEnteredTrigger -= HandleMineTrigger;
                }
            }
        }

        private void HandleMineTrigger(GameObject triggeredObject, GameObject objectWhoTrigger)
        {
            if (_gameEnded || triggeredObject == null)
            {
                return;
            }

            Mine mine = FindMineByGameObject(triggeredObject);
            if (mine != null)
            {
                HandleMineTriggered(mine, objectWhoTrigger);
            }
        }

        private Mine FindMineByGameObject(GameObject triggeredObject)
        {
            Mine mine = FindMineInList(triggeredObject, _healMines);
            if (mine != null) return mine;

            mine = FindMineInList(triggeredObject, _damageMines);
            if (mine != null) return mine;

            mine = FindMineInList(triggeredObject, _buffMines);
            if (mine != null) return mine;

            return FindMineInList(triggeredObject, _debuffMines);
        }

        private static Mine FindMineInList(GameObject triggeredObject, IEnumerable<Mine> mines)
        {
            if (mines == null)
            {
                return null;
            }

            foreach (Mine mine in mines)
            {
                if (mine.MineGameObject == triggeredObject)
                {
                    return mine;
                }
            }

            return null;
        }

        private void HandleMineTriggered(Mine givenMine, GameObject givenPlayer)
        {
            if (_gameEnded || PlayerChar == null || EnemyChar == null || givenPlayer == null)
            {
                return;
            }

            MiniGamePlayer givenPlayerChar = givenPlayer.GetComponent<MiniGamePlayer>();
            if (givenPlayerChar == null)
            {
                return;
            }

            bool triggeredByPlayer = ReferenceEquals(givenPlayerChar, PlayerChar);
            bool triggeredByEnemy = ReferenceEquals(givenPlayerChar, EnemyChar);
            if (!triggeredByPlayer && !triggeredByEnemy)
            {
                return;
            }

            if (givenMine is HealMine healMine)
            {
                healMine.Heal(givenPlayerChar);
                if (triggeredByPlayer)
                {
                    playerFX.PlayHealingEffect();
                }
                else
                {
                    enemyFX.PlayHealingEffect();
                }
            }
            else if (givenMine is DamageMine damageMine)
            {
                if (triggeredByPlayer)
                {
                    damageMine.Damage(EnemyChar, PlayerChar);
                    enemyFX.PlayAttackEffect();
                }
                else
                {
                    damageMine.Damage(PlayerChar, EnemyChar);
                    playerFX.PlayAttackEffect();
                }
            }
            else if (givenMine is BuffSpeedMine buffSpeedMine)
            {
                if (buffSpeedMine.GetSpeedBuff() > 0)
                {
                    if (triggeredByPlayer)
                    {
                        playerFX.PlayBuffedEffect();
                    }
                    else
                    {
                        enemyFX.PlayBuffedEffect();
                    }
                }
                else
                {
                    if (triggeredByPlayer)
                    {
                        playerFX.PlaySttopedEffect();
                    }
                    else
                    {
                        enemyFX.PlaySttopedEffect();
                    }
                }

                _ = MineExplosionAsync(buffSpeedMine, PlayerChar, EnemyChar);
            }

            givenMine.SetActive(false);
        }

        private async Task MineExplosionAsync(BuffSpeedMine mine, params MiniGamePlayer[] players)
        {
            if (mine == null || _gameEnded)
            {
                return;
            }

            try
            {
                CancellationToken cancellationToken = _gameplayCancellation?.Token ?? CancellationToken.None;
                Vector3 initialMinePosition = mine.MineGameObject.transform.position;
                await Task.Delay(mine.GetTimeBeforeExplosion(), cancellationToken);

                if (_gameEnded)
                {
                    return;
                }

                List<MiniGamePlayer> affectedPlayers = mine.FindDistanceToMine(initialMinePosition, players);
                await mine.BuffSpeedList(affectedPlayers, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void CancelPendingGameplay()
        {
            if (_gameplayCancellation != null && !_gameplayCancellation.IsCancellationRequested)
            {
                _gameplayCancellation.Cancel();
            }
        }

        private void OnDestroy()
        {
            CancelPendingGameplay();

            UnsubscribeFromMineEvents(_healMines);
            UnsubscribeFromMineEvents(_damageMines);
            UnsubscribeFromMineEvents(_buffMines);
            UnsubscribeFromMineEvents(_debuffMines);

            if (PlayerChar != null && _playerMove != null)
            {
                PlayerChar.OnSpeedChanged -= _playerMove.ChangeSpeed;
            }

            if (EnemyChar != null && _enemyController != null)
            {
                EnemyChar.OnSpeedChanged -= _enemyController.ChangeSpeed;
            }

            _gameplayCancellation?.Dispose();
            _gameplayCancellation = null;
        }
    }
}
