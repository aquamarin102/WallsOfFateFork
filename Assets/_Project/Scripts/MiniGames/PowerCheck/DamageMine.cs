using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.MiniGame.PowerCheck
{
    public class DamageMine : Mine
    {
        public DamageMine(uint number, float cooldown, GameObject mine) : base(number, cooldown, mine) { }

        public void Damage(MiniGamePlayer player1, MiniGamePlayer player2)
        {
            ////Debug.Log(player2.Name + " בüוע " + player1.Name);
            player1.TakeDamage(player2.Damage);
        }
    }
}

