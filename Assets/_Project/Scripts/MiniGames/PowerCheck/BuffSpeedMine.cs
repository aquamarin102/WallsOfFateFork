using System.Collections;
using System.Collections.Generic;
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
        private Dictionary<MiniGamePlayer, bool> activeBuffs = new Dictionary<MiniGamePlayer, bool>();

        public BuffSpeedMine(uint number, float cooldown, GameObject mine, float speedbuff, float buffcooldown, int timebeforeexplosion, float radius, uint damage, bool isDebuff)
            : base(number, cooldown, mine)
        {
            this.SpeedBuff = speedbuff;
            this.BuffCooldown = buffcooldown;
            this.TimeBeforeExplosion = timebeforeexplosion;
            this.MaxRadius = radius;
            this.Damage = damage;
            this.IsDebuff = isDebuff;
        }

        public float GetSpeedBuff() => this.SpeedBuff;
        public float GetBuffCooldown() => this.BuffCooldown;
        public int GetTimeBeforeExplosion() => this.TimeBeforeExplosion;

        public async Task BuffSpeed(MiniGamePlayer player)
        {
            // Проверяем, есть ли активный бафф на данном объекте
            if (activeBuffs.ContainsKey(player) && activeBuffs[player])
            {
                //Debug.LogWarning($"Buff is already active for {player.name}");
                return; // Не применяем бафф повторно
            }

            activeBuffs[player] = true;

            try
            {
                // Применяем начальный бафф
                player.TakeSpeedboost(this.SpeedBuff, IsDebuff);
                player.TakeDamage(this.Damage);

                await Task.Delay((int)(this.BuffCooldown * 1000));

                // Убираем бафф
                player.TakeSpeedboost(1f, IsDebuff);
            }
            finally
            {
                activeBuffs[player] = false;
            }
        }

        public async Task BuffSpeedList(List<MiniGamePlayer> players)
        {
            foreach (var player in players)
            {
                if (player != null)
                {
                    await BuffSpeed(player);
                }
            }
        }

        public List<MiniGamePlayer> FindDistanceToMine(Vector3 minePosition, params GameObject[] playerspositions)
        {
            List<MiniGamePlayer> closeObjects = new List<MiniGamePlayer>();

            foreach (var obj in playerspositions)
            {
                if (obj != null) // Проверяем, что объект не null
                {
                    float distance = Vector3.Distance(minePosition, obj.transform.position);
                    if (distance <= this.MaxRadius)
                    {
                        MiniGamePlayer objChar = obj.GetComponent<MiniGamePlayer>();
                        if (objChar != null) closeObjects.Add(objChar);
                    }
                }
                else
                {
                    //Debug.LogWarning("One of the passed GameObjects is null.");
                }
            }

            return closeObjects;
        }
    }
}
