using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Study_ActionPlatformer
{
    //하늘에서 검(탄환)을 꽂아버리는 패턴
    public class Pattern1 : BossPatternState
    {
        // 0. Pattern1이 활성화가 되면 sword를 Set시킨다
        // 1. sword을 castingTime 만큼 조준한다
        // 2. castingTime을 지나치면 sword의 Fire를 호출한다
        // 3. 잠시간의 대기시간을 갖고 Idle 패턴으로 넘어간다

        [SerializeField] private BossBullet sword;
        [SerializeField] private float castingTime;
        [SerializeField] private Vector3 distanceToTarget; // 타겟과 상대적인 거리를 저장하는 변수
        [SerializeField] private int swordDamage = 10;

        protected override void OnEnable()
        {
            sword.Set(swordDamage);
            StartCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            Transform target = Player.LocalPlayer.transform;
            float waitTime = 0.0f;

            //조준
            while (waitTime < castingTime)
            {
                sword.transform.position = target.position + distanceToTarget;
                yield return null;
                waitTime += Time.deltaTime;
            }

            //발사
            sword.Fire();

            //Idle로 넘어가기
            yield return new WaitForSeconds(2.5f);
            BossController.ChangeState(typeof(IdleState));
        }

        protected override void OnDisable()
        {
            sword.gameObject.SetActive(false);
        }
    }
}

