using Study.Utilities;
using System.Collections;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // 머리에서 쏘는 빔

    // 0. 활성화 되면, 보스 머리의 빔 객체(BossBullet)를 활성화 시킨다
    // 1. BossBullet을 조준한다
    // 2. 시전시간에 도달하면 BossBullet을 발사한다
    // 3. 잠시간의 대기시간을 갖고 Idle로 넘어 간다

    public class Pattern0 : BossPatternState
    {
        [SerializeField] private float catingTime = 3.0f;
        [SerializeField] private BossBullet headBeam;
        [SerializeField] private int headBeamDamage = 10;

        private ComponentPool<BossBullet> bulletPool;

        public override void Initialize(BossController controller)
        {
            base.Initialize(controller);

            bulletPool = new ComponentPool<BossBullet>(headBeam, headBeam.transform.parent);
        }

        protected override void OnEnable()
        {
            StartCoroutine(Coroutine());
        }

        protected override void OnDisable()
        {
            headBeam.gameObject.SetActive(false);
        }

        private IEnumerator Coroutine()
        {
            headBeam.gameObject.SetActive(true);
            yield return new WaitForSeconds(2.0f);

            Transform target = Player.LocalPlayer.transform;
            float waitTime = 0.0f;

            // 조준 + 연사
            while(true)
            {
                if (waitTime > catingTime) break;
                // bullet이 발사될 방향벡터를 계산합니다
                Vector3 direction = target.position - headBeam.transform.position;
                BossBullet bullet = bulletPool.Get();
                bullet.transform.up = direction;
                bullet.Set(headBeamDamage);
                bullet.Fire();

                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }


            yield return new WaitForSeconds(2.5f);
            headBeam.gameObject.SetActive(false);
            BossController.ChangeState(typeof(IdleState));
        }
    }
}


