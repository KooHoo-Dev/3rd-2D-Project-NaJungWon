using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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
            Bullet bullet = (UnitManagement.Instance.EnemyBullets.Count > 0) ? 
                UnitManagement.Instance.EnemyBullets.Dequeue() : Instantiate(BulletPrefab);

            bullet.transform.position = FirePoint.position;
            bullet.gameObject.SetActive(true); // → OnEnable() 호출 → TimelineTrackable 등록 + checkList 초기화
            Vector2 dir = (Target.position - FirePoint.position).normalized;

            bullet.Set(dir, transform.GetComponent<Enemy>());
            if (fireAnim != null) fireAnim.Play("Eenemy_Fire_Effect",0,0);
        }
    }
}

