using System.Collections;
using System.Diagnostics;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // 양손을 이용한 탄환 발사 패턴
    public class Pattern2 : BossPatternState
    {
        // 왼쪽손은 조준시 x좌표 고정. y값은 플레이어의 y좌표를 따라갑니다.
        // 오른쪽손은 조준시 y좌표가 고정. x값은 플레이어의 x좌표를 따라갑니다.
        
        [System.Serializable] // => 인스펙터에서 노출이 가능하게끔 해주는 키워드
        public class Hand
        {
            [Header("Settings")]
            public bool isLeft = false;
            public Vector3 offset = Vector3.zero;

            [Header("Ref")] // 씬상에서 참조하는 오브젝트들
            public Transform handTransform;
            public GameObject normal;
            public GameObject attack;
            public BossBullet bullet;
            public Transform axis;

            public void SetNormal(Vector3 originPos)
            {
                normal.SetActive(true);

                attack.SetActive(false);
                bullet.gameObject.SetActive(false);

                // 공격상태에서 이동했던 Hand를 원래 위치(캐릭터 왼쪽,오른쪽)로
                // 옮겨줌
                handTransform.position = originPos;
            }

            public void SetAttack(int pattern2Damage)
            {
                normal.SetActive(false);

                attack.SetActive(true);
                bullet.gameObject.SetActive(true);
                bullet.Set(pattern2Damage);
            }
        }

        [SerializeField] private Hand leftHand;
        [SerializeField] private Hand rightHand;

        [SerializeField] private float castingTime = 5.0f;
        [SerializeField] private int damage = 10;

        private Vector3 leftHandOrigin;
        private Vector3 rightHandOrigin;

        public override void Initialize(BossController controller)
        {
            base.Initialize(controller);
            leftHandOrigin = leftHand.handTransform.position;
            rightHandOrigin = rightHand.handTransform.position;
        }

        protected override void OnEnable()
        {
            // 패턴이 활성화되면, Hand의 상태를 바꿔줍니다
            leftHand.SetAttack(damage);
            rightHand.SetAttack(damage);
            StartCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            Transform target = Player.LocalPlayer.transform;
            float waitTime = 0.0f;

            while(waitTime < castingTime)
            {
                // 핸드의 위치 갱신,
                UpdateHand(leftHand, target);
                UpdateHand(rightHand, target);
                yield return null;
                waitTime += Time.deltaTime;
            }

            // 오른손 발사
            rightHand.bullet.Fire();
            yield return new WaitForSeconds(1.0f);
            // 왼손 발사
            leftHand.bullet.Fire();
            yield return new WaitForSeconds(1.0f);

            BossController.ChangeState(typeof(IdleState));
        }

        private void UpdateHand(Hand hand, Transform target)
        {
            // 왼쪽손은 조준시 x좌표 고정. y값은 플레이어의 y좌표를 따라갑니다.
            // 오른쪽손은 조준시 y좌표가 고정. x값은 플레이어의 x좌표를 따라갑니다.

            Vector3 handPosition = hand.handTransform.position;

            if(hand.isLeft)
            {
                handPosition.x = hand.axis.position.x;
                handPosition.y = target.position.y;
            }
            else
            {
                handPosition.y = hand.axis.position.y;
                handPosition.x = target.position.x;
            }

            hand.handTransform.position = handPosition;
        }


        protected override void OnDisable()
        {
            leftHand.SetNormal(leftHandOrigin);
            rightHand.SetNormal(rightHandOrigin);
        }


    }
}

