using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.MiniGame.PowerCheck
{
    public class BuffSpeedMine : Mine
    {
        private float SpeedBuff;
        private float BuffCooldown;
        private int TimeBeforeExplosion;
        private float MaxRadius;
        private uint Damage;
        private bool IsDebuff;

        // Словарь для отслеживания активных баффов
        private readonly Dictionary<MiniGamePlayer, bool> activeBuffs = new Dictionary<MiniGamePlayer, bool>();

        public BuffSpeedMine(uint number, float cooldown, GameObject mine, float speedbuff, float buffcooldown, int timebeforeexplosion, float radius, uint damage, bool isDebuff)
            : base(number, cooldown, mine)
        {
            SpeedBuff = speedbuff;
            BuffCooldown = buffcooldown;
            TimeBeforeExplosion = timebeforeexplosion;
            MaxRadius = radius;
            Damage = damage;
            IsDebuff = isDebuff;
        }

        public float GetSpeedBuff() => SpeedBuff;
        public float GetBuffCooldown() => BuffCooldown;
        public int GetTimeBeforeExplosion() => TimeBeforeExplosion;

        public async Task BuffSpeed(MiniGamePlayer player, CancellationToken cancellationToken = default)
        {
            if (player == null || player.isDead)
            {
                return;
            }

            if (activeBuffs.TryGetValue(player, out bool isActive) && isActive)
            {
                return;
            }

            activeBuffs[player] = true;
            bool buffApplied = false;

            try
            {
                player.ApplySpeedEffect(this, SpeedBuff, IsDebuff);
                player.TakeDamage(Damage);
                buffApplied = true;

                await Task.Delay((int)(BuffCooldown * 1000f), cancellationToken);

                if (player != null && !player.isDead)
                {
                    player.ClearSpeedEffect(this, IsDebuff);
                }
            }
            catch (OperationCanceledException)
            {
                if (buffApplied && player != null)
                {
                    player.ClearSpeedEffect(this, IsDebuff);
                }

                throw;
            }
            finally
            {
                activeBuffs[player] = false;
            }
        }

        public Task BuffSpeedList(IReadOnlyList<MiniGamePlayer> players, CancellationToken cancellationToken = default)
        {
            if (players == null || players.Count == 0)
            {
                return Task.CompletedTask;
            }

            List<Task> tasks = null;
            for (int i = 0; i < players.Count; i++)
            {
                MiniGamePlayer player = players[i];
                if (player != null)
                {
                    tasks ??= new List<Task>(players.Count);
                    tasks.Add(BuffSpeed(player, cancellationToken));
                }
            }

            return tasks == null || tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }

        public List<MiniGamePlayer> FindDistanceToMine(Vector3 minePosition, params MiniGamePlayer[] players)
        {
            List<MiniGamePlayer> closeObjects = new List<MiniGamePlayer>(players.Length);

            for (int i = 0; i < players.Length; i++)
            {
                MiniGamePlayer player = players[i];
                if (player == null || player.isDead)
                {
                    continue;
                }

                float distance = Vector3.Distance(minePosition, player.transform.position);
                if (distance <= MaxRadius)
                {
                    closeObjects.Add(player);
                }
            }

            return closeObjects;
        }
    }
}
