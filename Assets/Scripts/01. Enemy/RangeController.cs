using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RangeController : EnemyController
    {
        [field: SerializeField] public Bullet BulletPrefab { get; set; }
        [field: SerializeField] public Transform FirePoint { get; private set; }

        public List<Bullet> enableBullet = new List<Bullet>();
        public List<Bullet> disableBullet = new List<Bullet>();

        public GameObject aimArm;
        public Animator fireAnim;

        protected override void ProcessAttack()
        {
            UpdateDirection(Target.position);
            Bullet bullet = Instantiate(BulletPrefab, FirePoint.position, Quaternion.identity);
            if (fireAnim != null) fireAnim.Play("Eenemy_Fire_Effect");

            Vector2 dir = (Target.position - transform.position).normalized;
            bullet.Set(dir);
        }
    }
}

