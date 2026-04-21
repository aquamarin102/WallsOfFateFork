using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MiniGame.PowerCheck
{
    public class HealMine : Mine
    {
        public HealMine(uint number, float cooldown, GameObject mine) : base(number, cooldown, mine) { }

        public void Heal(MiniGamePlayer player)
        {
            player.TakeHeal();
        }
    }
}

