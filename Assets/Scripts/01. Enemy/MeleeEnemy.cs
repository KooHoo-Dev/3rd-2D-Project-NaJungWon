using UnityEngine;

namespace Study_ActionPlatformer
{
    public class MeleeEnemy : Enemy
    {
        public override void TakeDamage(int damage, Vector2 Hitdirection, float power)
        {
            base.TakeDamage(damage, Hitdirection, power);
        }

        public override void TakeHeal(int heal)
        {
            
        }
    }

}

