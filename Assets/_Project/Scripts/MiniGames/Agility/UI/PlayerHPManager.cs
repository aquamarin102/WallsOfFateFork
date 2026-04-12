using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MiniGame.Agility
{
    public class PlayerHPManager : MonoBehaviour
    {
        [Header("HP Icons")]
        public List<Image> HPIcons = new List<Image>();

        [Header("References")]
        public Sprite FirstHPSprite;
        public Sprite NormalHPSprite;
        public Sprite DeadHPSprite;

        private int currentHP;

        public void InitializeHP(int startHP)
        {
            RenderHP(startHP, startHP);
        }

        public void UpdateHP(int newHP)
        {
            RenderHP(newHP, Mathf.Max(currentHP, newHP));
        }

        public void UpdateHP(int newHP, int maxHP)
        {
            RenderHP(newHP, maxHP);
        }

        private void RenderHP(int hp, int maxHP)
        {
            currentHP = Mathf.Clamp(hp, 0, maxHP);

            for (int i = 0; i < HPIcons.Count; i++)
            {
                Image icon = HPIcons[i];
                if (icon == null)
                    continue;

                bool active = i < maxHP;
                icon.gameObject.SetActive(active);
                if (!active)
                    continue;

                if (i >= currentHP)
                    icon.sprite = DeadHPSprite;
                else if (i == 0 && FirstHPSprite != null)
                    icon.sprite = FirstHPSprite;
                else
                    icon.sprite = NormalHPSprite;
            }
        }
    }
}
