using Game.Data;
using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace Game.MiniGame.PowerCheck
{
    public class MiniGamePlayer : MonoBehaviour
    {
        // ============================
        // Настройки игровых параметров игрока
        // ============================
        [SerializeField] private string playerName;      // Имя игрока
        [SerializeField] private string playerNameForGame;      // Имя игрока
        [SerializeField] private uint maxHealth;         // Максимальное здоровье
        [SerializeField] private uint health;            // Текущее здоровье

        // Новый: минимальное и максимальное значения урона
        [Header("Damage Range")]
        [SerializeField] private uint minDamage = 1;     // Нижняя граница урона
        [SerializeField] private uint damage = 1;        // Верхняя граница урона (раньше — просто damage)

        // Базовая скорость
        [SerializeField] private float speed;
        [SerializeField] private float speedModifier;

        // Новый: минимальное и максимальное значения лечения
        [Header("Healing Range")]
        [SerializeField] private uint minHealingAmount = 1;  // Нижняя граница лечения
        [SerializeField] private uint healingAmount = 1;     // Верхняя граница лечения (раньше — просто healingAmount)

        public bool isDead = false;                     // Флаг смерти

        // ============================
        // Настройки VFX
        // ============================
        [Header("World-Prefab VFX")]
        [SerializeField] public string Portrait = "";

        private bool underDebuff;
        private Coroutine pulseCoroutine;
        private uint baseMinDamage;
        private uint baseDamage;
        private uint baseMinHealingAmount;
        private uint baseHealingAmount;
        private float baseSpeed;
        private bool baseStatsCaptured;
        private readonly Dictionary<object, float> activeBuffSpeedEffects = new Dictionary<object, float>();
        private readonly Dictionary<object, float> activeDebuffSpeedEffects = new Dictionary<object, float>();

        public string Name
        {
            get => playerName;
            set => playerName = value;
        }

        public uint MaxHealth => maxHealth;
        public uint Health
        {
            get => health;
            set
            {
                uint clampedHealth = value > maxHealth ? maxHealth : value;
                if (health == clampedHealth)
                {
                    return;
                }

                health = clampedHealth;
                OnHealthChanged?.Invoke(health, maxHealth);
            }
        }

        // Теперь свойство Damage возвращает случайное значение в заданном диапазоне [minDamage; damage]
        public uint Damage
        {
            get
            {
                if (damage <= minDamage)
                    return damage;
                int min = (int)minDamage;
                int maxExclusive = (int)damage + 1;
                return (uint)UnityEngine.Random.Range(min, maxExclusive);
            }
        }

        public float SpeedModifier
        {
            get => speedModifier;
            set
            {
                speedModifier = value;
                OnSpeedChanged?.Invoke(speedModifier, underDebuff);
            }
        }

        public float Speed => speed;

        // HealingAmount теперь хранит верхнюю границу,
        // реальное лечение считается внутри метода TakeHeal
        public uint HealingAmount => healingAmount;
        public float AverageDamage => damage <= minDamage ? damage : (minDamage + damage) * 0.5f;
        public float AverageHealingAmount => healingAmount <= minHealingAmount ? healingAmount : (minHealingAmount + healingAmount) * 0.5f;

        public event Action<float, bool> OnSpeedChanged;
        public event Action<uint, uint> OnHealthChanged;

        private PlayerManager playerManager;

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        private void Awake()
        {
            CacheBaseStats();
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            ApplyProgressionStats();
            ResetHealth();
        }

        private void ResolveDependencies()
        {
            playerManager ??= PlayerManager.Instance;
        }

        private void CacheBaseStats()
        {
            if (baseStatsCaptured)
            {
                return;
            }

            baseSpeed = speed;
            baseMinDamage = minDamage;
            baseDamage = damage;
            baseMinHealingAmount = minHealingAmount;
            baseHealingAmount = healingAmount;
            baseStatsCaptured = true;
        }

        private void ApplyProgressionStats()
        {
            CacheBaseStats();

            speed = baseSpeed;
            minDamage = baseMinDamage;
            damage = baseDamage;
            minHealingAmount = baseMinHealingAmount;
            healingAmount = baseHealingAmount;

            activeBuffSpeedEffects.Clear();
            activeDebuffSpeedEffects.Clear();
            underDebuff = false;
            speedModifier = 1f;

            if (playerManager == null)
            {
                return;
            }

            int dexterity = Mathf.Max(0, playerManager.PlayerData.GetStat(StatType.Dex));
            int strength = Mathf.Max(0, playerManager.PlayerData.GetStat(StatType.Strength));

            speed += dexterity;
            damage += Convert.ToUInt32(strength);
            minDamage += Convert.ToUInt32(strength);
        }

        public string GetName()
        {
            return playerNameForGame;
        }

        public void TakeDamage(uint dmg)
        {
            Health = health >= dmg ? health - dmg : 0;
        }

        public void TakeHeal()
        {
            // Вычисляем случайное лечение в диапазоне [minHealingAmount; healingAmount]
            uint healValue;
            if (healingAmount <= minHealingAmount)
            {
                healValue = healingAmount;
            }
            else
            {
                int min = (int)minHealingAmount;
                int maxExc = (int)healingAmount + 1;
                healValue = (uint)UnityEngine.Random.Range(min, maxExc);
            }

            Health = health + healValue;
        }

        public void TakeSpeedboost(float speedMultiplier, bool isDebuff)
        {
            underDebuff = isDebuff && !Mathf.Approximately(speedMultiplier, 1f);
            SpeedModifier = speedMultiplier;

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        public void ApplySpeedEffect(object source, float speedMultiplier, bool isDebuff)
        {
            if (source == null)
            {
                return;
            }

            Dictionary<object, float> targetEffects = isDebuff ? activeDebuffSpeedEffects : activeBuffSpeedEffects;
            targetEffects[source] = speedMultiplier;
            RefreshSpeedEffects();
        }

        public void ClearSpeedEffect(object source, bool isDebuff)
        {
            if (source == null)
            {
                return;
            }

            Dictionary<object, float> targetEffects = isDebuff ? activeDebuffSpeedEffects : activeBuffSpeedEffects;
            if (targetEffects.Remove(source))
            {
                RefreshSpeedEffects();
            }
        }

        private void RefreshSpeedEffects()
        {
            float resultingSpeedModifier = 1f;
            bool hasDebuffEffect = activeDebuffSpeedEffects.Count > 0;

            if (hasDebuffEffect)
            {
                foreach (float debuffMultiplier in activeDebuffSpeedEffects.Values)
                {
                    resultingSpeedModifier = Mathf.Min(resultingSpeedModifier, debuffMultiplier);
                }
            }
            else if (activeBuffSpeedEffects.Count > 0)
            {
                foreach (float buffMultiplier in activeBuffSpeedEffects.Values)
                {
                    resultingSpeedModifier = Mathf.Max(resultingSpeedModifier, buffMultiplier);
                }
            }

            underDebuff = hasDebuffEffect && !Mathf.Approximately(resultingSpeedModifier, 1f);
            SpeedModifier = resultingSpeedModifier;

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        private void ResetHealth()
        {
            Health = maxHealth;
            isDead = false;
        }

        private Camera GetPowerCheckCamera()
        {
            var go = GameObject.FindGameObjectWithTag("PowerCheckCamera");
            return go != null ? go.GetComponent<Camera>() : null;
        }
    }

}

