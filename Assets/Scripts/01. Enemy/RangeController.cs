using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RangeController : EnemyController
    {
        [field: SerializeField] public Bullet BulletPrefab { get; set; }
        [field: SerializeField] public Transform FirePoint { get; private set; }

        protected override void ProcessAttack()
        {
            UpdateDirection(Target.position);
            Bullet bullet = Instantiate(BulletPrefab, FirePoint.position, Quaternion.identity);

            float dir = Target.position.x - transform.position.x;
            Vector3 right = Vector3.right * Mathf.Sign(dir);
            bullet.Set(right);
        }
    }
}

