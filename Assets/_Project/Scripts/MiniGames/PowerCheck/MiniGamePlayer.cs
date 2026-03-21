using Game.Data;
using System;
using System.Collections;
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

        public string Name
        {
            get => playerName;
            set => playerName = value;
        }

        public uint MaxHealth => maxHealth;
        public uint Health
        {
            get => health;
            set => health = value;
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

        public event Action<float, bool> OnSpeedChanged;

        private PlayerManager playerManager;

        [Inject]
        public void Construct(PlayerManager playerManager)
        {
            this.playerManager = playerManager;
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            ResetHealth();
            if (playerManager != null)
            {
                speedModifier += playerManager.PlayerData.GetStat(StatType.Dex);
                damage += Convert.ToUInt32(playerManager.PlayerData.GetStat(StatType.Strength));
                minDamage += Convert.ToUInt32(playerManager.PlayerData.GetStat(StatType.Strength));
            }
        }

        private void ResolveDependencies()
        {
            playerManager ??= PlayerManager.Instance;
        }

        public string GetName()
        {
            return playerNameForGame;
        }

        public void TakeDamage(uint dmg)
        {
            health = health >= dmg ? health - dmg : 0;
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

            health += healValue;
            if (health > maxHealth)
                health = maxHealth;
        }

        public void TakeSpeedboost(float speedMultiplier, bool isDebuff)
        {
            underDebuff = isDebuff;
            SpeedModifier = speedMultiplier;

            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
                pulseCoroutine = null;
            }
        }

        private void ResetHealth()
        {
            health = maxHealth;
            isDead = false;
        }

        private Camera GetPowerCheckCamera()
        {
            var go = GameObject.FindGameObjectWithTag("PowerCheckCamera");
            return go != null ? go.GetComponent<Camera>() : null;
        }
    }

}

