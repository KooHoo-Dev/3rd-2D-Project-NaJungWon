using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RefelectionBullet : BossBullet
    {
        [SerializeField] private LayerMask reflectLayer;

        protected override void OnTriggerEnter2D(Collider2D collision)
        {
            base.OnTriggerEnter2D(collision);

            if(reflectLayer.Contains(collision.gameObject))
            {
                Vector3 newUp = transform.up;
                newUp.y *= -1f;
                transform.up = newUp;
            }
        }

    }

}

