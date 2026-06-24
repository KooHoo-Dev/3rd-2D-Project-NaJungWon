using Study.Utilities;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Study_ActionPlatformer
{
    public class EnemyController : MonoBehaviour
    {
        // 코루틴을 이용한 FSM 만들기
        // 코루틴의 yield return StartCoroutine(코루틴); 함수를 이용해서
        // 코루틴들끼리 연결되어 끊임없이 순환하는 구조의
        // 소규모 인공지능 캐릭터를 만들어 봅시다.
        
        // : Simple FSM 이라고 부름(나만)

        private static readonly int IS_MOVE = Animator.StringToHash("IsMove");
        private static readonly int ATTACK = Animator.StringToHash("Attack");

        private const float ATTACK_HIT_DELAY = 0.5f;
        private const float ATTACK_COOLDOWN = 2.0f;

        [SerializeField] private float moveSpeed = 1f;
        [SerializeField] private float traceRange = 10.0f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float baseUpdateTerm = 0.1f;

        



        [field: SerializeField] public Transform Target;

        private Animator Animator { get; set; }
        private Enemy Enemy { get; set; }

        
        private Vector3 originalScale;

        // 빈 코루틴 필드 (빈 객체랑 동일하다)
        protected IEnumerator nextStateCoroutine;

        private void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            Enemy = GetComponentInChildren<Enemy>();
            originalScale = transform.localScale;

            // y값 보정이 필요합니다.
            pointA = transform.position;
            pointA.y = transform.position.y;

            pointB = patrolPoint.position;
            pointB.y = transform.position.y;

            goalPoint = pointB;
        }

        private void OnEnable()
        {
            StartCoroutine(FiniteStateMachineCoroutine());
        }

        // 메인 코루틴 루프
        private IEnumerator FiniteStateMachineCoroutine()
        {
            // 기본상태를 넣어주고 루프를 시작한다.
            // IEnumerator
            // : 유니티의 코루틴 한정으로, 특정 코루틴의 진행상태를
            //  저장하는 변수라고 생각해주세요
            
            nextStateCoroutine = IdleStateCoroutine();

            // 게임 오브젝트가 켜져있다면 반복하는
            // 루프를 구성합니다
            while(gameObject.activeInHierarchy)
            {
                // yield return StartCoroutine(코루틴);
                // : 매개변수로 주어진 코루틴이 종료될때까지
                //  처리를 양보합니다. => 대기한다
                yield return StartCoroutine(nextStateCoroutine);
            }
        }

        private IEnumerator IdleStateCoroutine()
        {
            float waitTime = 0.0f;
            const float IDLE_WAIT_TIME = 3.0f; // 개발하실때 이런 변수는 밖으로 빼시는걸 추천

            WaitForSeconds term = new WaitForSeconds(baseUpdateTerm);

            while(true)
            {
                // Idle상태의 탈출조건

                // 1. 3초가 지났을때 PatrolState로 전환(Transition)
                if (IDLE_WAIT_TIME < waitTime)
                {
                    nextStateCoroutine = PatrolStateCoroutine();
                    yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                }

                // 2. 타깃이 TraceRange안에 있을때 AttackState로 전환
                if (Target != null && Target.IsInRange(transform.position, traceRange))
                {
                    // 플레이어와 내가 같은 층에 있을때 (조건 검사)
                    if (CompareFloor(transform.position, Target.position) == 0)
                    {
                        nextStateCoroutine = AttackStateCoroutine();
                        yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                    }
                }

                yield return term;
                waitTime += baseUpdateTerm;
            }
        }

        [SerializeField] private Transform patrolPoint;
        private Vector3 pointA, pointB, goalPoint; // y좌표를 내 좌표계로 바꿔줘야 한다.

        private IEnumerator PatrolStateCoroutine()
        {
            // Patrol은 목표지점(goalPoint)을 향해 움직입니다
            // 목표지점에 도달하면
            // 내 다음목적지 포인트를 갱신하고
            // Idle 상태로 전환 됩니다

            const float STOPPING_DISTANCE = 0.1f;

            while (true)
            {
                Vector3 adjustGoalPoint = transform.position;
                adjustGoalPoint.x = goalPoint.x;

                if (Target != null && Target.IsInRange(transform.position, traceRange))
                {
                    // 플레이어와 내가 같은 층에 있을때 (조건 검사)
                    if (CompareFloor(transform.position, Target.position) == 0)
                    {
                        nextStateCoroutine = AttackStateCoroutine();
                        yield break; // 코루틴 자체를 탈출하는 키워드 입니다
                    }
                }

                // 내가 현재 목표지점에 가까운지?
                // - 목표지점이 B인지 ?  A지점으로 갱신 : B지점으로 갱신

                // 목적지에 가까워졌으면
                if (transform.IsInRange(adjustGoalPoint, STOPPING_DISTANCE))
                {
                    // 삼항 연산자를 사용해서 pointB의 위치라면 pointA, 아니라면 pointB 바꿔준다\
                    // 내가 가까운 목표 지점에 따라서 해당 목표지점의 반대 지점으로 바꿔주기
                    goalPoint = transform.IsSamePosition(pointB, STOPPING_DISTANCE) ? pointA : pointB;
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }

                Move(adjustGoalPoint);
                yield return null;
            }
        }

        private IEnumerator AttackStateCoroutine()
        {
            while(true)
            {
                // 1. Target이 사라질경우
                if(Target == null)
                {
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }
                
                // 2. Target이 범위(추적 범위) 밖으로 이동할 경우
                //  : 타겟이 매우 빠르게 추적 가능한 범위 바깥으로 이동하게된 케이스
                if(Target.IsInRange(transform.position, traceRange) == false)
                {
                    nextStateCoroutine = IdleStateCoroutine();
                    Animator.SetBool(IS_MOVE, false);
                    yield break;
                }

                //======== 반복문 탈출 검사가 끝나면 =======
                // 공격하거나 (타겟이 사거리 안에 있으면)
                Vector3 adjustTargetPosition = Target.position;
                adjustTargetPosition.y = transform.position.y;

                if(transform.IsInRange(adjustTargetPosition, attackRange))
                {
                    Animator.SetBool(IS_MOVE, false);
                    Animator.SetTrigger(ATTACK);

                    // 공격 모션이 타격 프레임에 도달 할 때까지 대기한다
                    // - 정석은 애니메이션 이벤트 데이터등을 넣어서
                    // 다격 프레임을 정확하게 알아내는게 맞습니다.
                    // - 여기서는 간단하게 시간으로 처리합니다.

                    // 공격 판정을 위한 대기
                    yield return new WaitForSeconds(ATTACK_HIT_DELAY);
                    
                    ProcessAttack();

                    // 공격 전체 쿨다운 대기
                    yield return new WaitForSeconds(ATTACK_COOLDOWN - ATTACK_HIT_DELAY);
                }
                else Move(Target.position); // 이동 한다(타겟이 사거리 안에 들어올때까지)

                yield return null;
            }
        }

        protected void Move(Vector3 goalPosition)
        {
            Animator.SetBool(IS_MOVE, true);
            // 1차원 방향(사이드뷰, 플랫포머 이니까)
            float moveDirection = UpdateDirection(goalPosition);
            transform.Translate(new Vector3(moveDirection, 0, 0) * (moveSpeed * Time.deltaTime));
        }

        /// <summary>
        /// 캐릭터의 방향을 업데이트하고, x축으로 전방 방향(-1, 1 부호)을 반환합니다
        /// </summary>
        /// <param name="direction"></param>
        /// <returns></returns>
        protected float UpdateDirection(Vector3 goalPosition)
        {
            float dirToGoal = goalPosition.x - transform.position.x;
            // 부호만 받아낸다.
            float moveDirection = Mathf.Sign(dirToGoal);
            // 방향에 맞춰서 오른쪽/왼쪽 전환(스케일 x 값을 이용)
            transform.localScale =
                (new Vector3(moveDirection * originalScale.x, originalScale.y, originalScale.z));
            return moveDirection;
        }

        protected virtual void ProcessAttack()
        {
            
        }

        // 내 Transform과 Target의 Transform의 y값을 비교하여
        // 같은 층에 있는지를 조회하는 함수.
        // a가 b보다 낮은 층에 있으면 -1을
        // a와 b가 갖은 층에 있으면 0을
        // a가 b보다 높은 층에 있으면 1을

        // Floor에 대한 정의 필요함

        // 프로젝트마다 정의가 달라져야함 
        private int CompareFloor(Vector3 a, Vector3 b)
        {
            const float EPSILON = 1.0f; // 천장고 이런 느낌의 변수
            float yDistance = a.y - b.y;

            if (Mathf.Abs(yDistance) <= EPSILON) return 0;
            else if (yDistance > 0) return 1;
            else return -1; //(yDistance < 0)
        }


        // 변동될 가능성이 높은것 같은디 허허
        [SerializeField] private GameObject deadEffect;
        [SerializeField] private Vector3 deadEffectOffset;
        [SerializeField] private float deadEffectLifeTime = 0.5f;

        public void Dead()
        {
            // Enemy가 죽게 되면 죽음 이펙트를 생성하고, 스스로를 삭제합니다.
            GameObject effect = Instantiate(deadEffect, 
                transform.position + deadEffectOffset, Quaternion.identity);

            // 이펙트는 일정시간이 지난뒤에 자동으로 삭제 됩니다.
            // 여기서는 Destroy(삭제할 대상, 지연시간); 함수를 사용합니다.
            Destroy(effect, deadEffectLifeTime); // effect를 deadEffectLifeTime시간 이후에 삭제한다.

            Destroy(gameObject);
        }
    }





}

