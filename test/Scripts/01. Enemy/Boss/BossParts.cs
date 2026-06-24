using UnityEngine;

namespace Study_ActionPlatformer
{
    public class BossParts : Enemy
    {
        [field: SerializeField] private Boss Owner { get; set; }

        // 부위가 받는 데미지는 
        public override int CalculateFinalDamage(int damage)
        {
            return Owner.CalculateFinalDamage(damage);
        }

        public override void TakeDamage(int damage)
        {
            Owner.TakeDamage(this, damage);
            StartCoroutine(HitEffectCoroutine());
        }

        public override void TakeHeal(int heal)
        {

        }
    }

}

