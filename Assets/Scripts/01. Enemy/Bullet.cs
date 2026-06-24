using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Bullet : MonoBehaviour
    {
        [field: SerializeField] public float Speed { get; private set; }
        private Vector3 foward = Vector3.right;

        // Update is called once per frame
        private void Update()
        {
            transform.Translate(foward * Speed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if(other.gameObject == Player.LocalPlayer.gameObject)
            {
                Destroy(gameObject);
            }
        }

        public void Set(Vector3 direction)
        {
            foward = direction;
        }
    }

}


