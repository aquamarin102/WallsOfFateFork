using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game
{
    public class TimeStopper : MonoBehaviour
    {
        [SerializeField] private List<GameObject> targetObjects;
        public List<GameObject> TargetObjects => targetObjects;

        // Переменная для отслеживания текущего состояния паузы
        private bool isPaused = false;

        private void Update()
        {
            // Проверяем, должен ли быть вызван режим паузы: если список не null и хотя бы один объект активен
            bool shouldPause = targetObjects != null && targetObjects.Any(obj => obj.activeSelf);

            // Если состояние изменилось, то вызываем TimeStop
            if (shouldPause != isPaused)
            {
                SetTimePause(shouldPause);
                isPaused = shouldPause;
            }
        }

        private void SetTimePause(bool pause)
        {
            if (pause)
            {
                Time.timeScale = 0f; // Останавливаем время
                ////Debug.Log("Игра на паузе");
            }
            else
            {
                Time.timeScale = 1f; // Восстанавливаем время
                ////Debug.Log("Игра продолжается");
            }
        }
    }
}
