using UnityEngine;
using UnityEngine.UIElements;

namespace Study_ActionPlatformer
{
    public class BossBullet : MonoBehaviour
    {
        // 1. 특정 방향으로 운동
        // 2. 플레이어와 충돌하면 데미지를 줌(전투이벤트 생성 후 전달, Pass) 
        // 3. 재사용이 가능해야함

        [SerializeField] private Vector3 startLocalPosition;
        [SerializeField] private Vector3 moveVector = Vector3.up;
        [SerializeField] private float speed = 1.0f;
        
        // 못맞췄을 경우 영원히 날아가는것을 막기위해 lifeTime을 설정합니다
        [SerializeField] private float lifeTime = 5.0f;
        private float currentTime = 0.0f;
        private bool isFired = false;

        // Bullet 객체를 초기화 합니다.
        // (데미지는 알아서)
        public void Set(int damage)
        {
            isFired = false;
            gameObject.SetActive(true);
            transform.localPosition = startLocalPosition;
        }

        public void Fire()
        {
            isFired = true;
            currentTime = 0.0f;
        }

        private void Update()
        {
            if(isFired == false) return;

            currentTime += Time.deltaTime;
            
            if(currentTime >= lifeTime)
            {
                gameObject.SetActive(false);
                return;
            }

            // 아래부터는 운동로직
            transform.Translate(moveVector * (speed * Time.deltaTime), Space.Self);
            // Translate(이동량, Space.Self) 내 기준으로 이동
            // Translate(이동량, Space.World) 월드를 기준으로 이동
        }

        protected virtual void OnTriggerEnter2D(Collider2D collision)
        {
            if(collision.gameObject.CompareTag("Player"))
            {
                // 원래는 전투 이벤트 처리 로직이 들어가야함.
                
                gameObject.SetActive(false);
            }
        }
    }

}

