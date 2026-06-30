using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Bullet : MonoBehaviour
    {
        [field: SerializeField] public float Speed { get; private set; }
        private Vector2 fireDirection = Vector2.zero;

        // Update is called once per frame
        private void Update()
        {
            transform.Translate(fireDirection * Speed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(other.gameObject == Player.LocalPlayer.gameObject)
            {
                Destroy(gameObject);
            }
        }

        public void Set(Vector2 direction)
        {
            transform.right = direction;
            fireDirection = direction;
        }
    }

}


