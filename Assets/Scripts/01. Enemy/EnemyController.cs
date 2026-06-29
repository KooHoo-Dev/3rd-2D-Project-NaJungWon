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

        private static readonly int IS_WALK = Animator.StringToHash("IsWalk");
        private static readonly int IS_RUN = Animator.StringToHash("IsRun");
        private static readonly int ATTACK = Animator.StringToHash("Attack");
        private static readonly int IS_DEAD = Animator.StringToHash("IsDead");

        private const float IDLE_WAIT_TIME = 3.0f;
        private const float ATTACK_HIT_DELAY = 0.5f;
        private const float ATTACK_COOLDOWN = 2.0f;

        private const float GROUND_STICK_SPEED = -2.0f;


        [SerializeField] private float walkSpeed = 1f;
        [SerializeField] private float runSpeed = 3f;
        [SerializeField] private float traceRange = 10.0f;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float baseUpdateTerm = 0.1f;

        [Header("Physics Settings")]
        public float gravity = -9.81f;
        public float skinWidth = 0.02f;
        public float groundCheckDistance = 0.5f;

        [Header("Step Settings")]
        public float maxStepHeight = 0.3f;   // 한 번에 올라갈 수 있는 최대 계단 높이
        public int stepCheckCount = 4;       // 위로 몇 번 나눠서 검사할지

        [Header("Knockback Settings")]
        [SerializeField] private float externalDrag = 12.0f;

        public LayerMask collisionLayer;
        public LayerMask originLayer;
        public LayerMask droppingLayer;

        public bool IsGrounded { get; private set; } = false;
        public bool IsDead { get; private set; } = false;

        //private Coroutine fsmRoutine;
        public Rigidbody2D rb;
        public Collider2D col;

        private bool useGravity = true;
        private bool isDropping = false;

        public float desiredMoveX = 0.0f;
        private float baseVerticalVelocity = 0.0f; //기본 점프 + 중력값
        private Vector2 externalVelocity = Vector2.zero;


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

            rb = GetComponent<Rigidbody2D>();
            col = GetComponentInChildren<Collider2D>();

            rb.bodyType = RigidbodyType2D.Kinematic;

            originalScale = transform.localScale;
            originLayer = collisionLayer;

            // y값 보정이 필요합니다.
            pointA = transform.position;
            pointA.y = transform.position.y;

            pointB = patrolPoint.position;
            pointB.y = transform.position.y;

            goalPoint = pointB;
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();

            ApplyGravaity();

            // 죽으면 AI 이동은 막고, 넉백/중력만 남길지 선택 가능
            float horizontalVelocity = IsDead ? 0.0f : desiredMoveX + externalVelocity.x;
            float verticalVelocity = baseVerticalVelocity + externalVelocity.y;

            Vector3 desireMove =
                new Vector3(horizontalVelocity, verticalVelocity, 0.0f) * Time.fixedDeltaTime;

            Bounds box = GetCastBox();
            Vector3 moveVector = Vector3.zero;

            if(TryResolveStairMovement(box, desireMove.x, Vector3.right, out Vector2 stepMove))
            {
                moveVector.x = stepMove.x;
                moveVector.y = stepMove.y;

                if (stepMove.y > 0.0f && baseVerticalVelocity < 0.0f)
                {
                    baseVerticalVelocity = 0.0f;
                }
            }
            else
            {
                float boxX = ResolveAxisMovement(box, desireMove.x, Vector3.right);
                box.center += new Vector3(boxX, 0, 0);
                float boxY = ResolveAxisMovement(box, desireMove.y, Vector3.up);

                moveVector = new Vector3(boxX, boxY, 0);

                if (moveVector.y != desireMove.y)
                {
                    baseVerticalVelocity = 0.0f;
                    externalVelocity.y = 0.0f;
                }

                if (moveVector.x != desireMove.x)
                {
                    externalVelocity.x = 0.0f;
                }
            }

            // 죽은 뒤에도 넉백은 잠깐 남게 두고 감쇠
            externalVelocity = Vector2.Lerp(
                externalVelocity,
                Vector2.zero,
                externalDrag * Time.fixedDeltaTime);

            if (IsGrounded && externalVelocity.y < 0.0f)
                externalVelocity.y = 0.0f;

            rb.MovePosition(rb.position + (Vector2)moveVector);
        }
        public void AddExternalForce(Vector2 force)
        {
            externalVelocity += force;
        }

        private void ApplyGravaity()
        {
            // 여기 공격중 중력끄는 코드 필요
            if (useGravity == false) return;

            if (IsGrounded && baseVerticalVelocity <= 0)
            {
                baseVerticalVelocity += GROUND_STICK_SPEED;
            }
            else // 공중에 있는 상태
            {
                baseVerticalVelocity += gravity * Time.fixedDeltaTime;
            }
        }

        #region 이동값 보정

        private float ResolveAxisMovement(Bounds box, float move, Vector3 axis) //기본 이동값 보정
        {
            if (move == 0.0f) return 0.0f;

            Vector3 dir = move > 0.0f ? axis : -axis;
            float distance = Mathf.Abs(move);

            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, dir, distance, collisionLayer);

            if (hit.collider == null) return move;

            float allowed = Mathf.Max(0, hit.distance - skinWidth);

            return Mathf.Sign(move) * allowed;
        }

        private bool TryResolveStairMovement(Bounds box, float moveX, Vector3 axis, out Vector2 stairMove) //기본 계단 이동값 보정
        {
            stairMove = Vector2.zero;

            if (Mathf.Approximately(moveX, 0.0f))
                return false;

            if (IsGrounded == false || baseVerticalVelocity > 0.0f)
                return false;

            Vector3 dir = moveX > 0.0f ? axis : -axis;
            float distance = Mathf.Abs(moveX);

            RaycastHit2D frontHit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                dir,
                distance,
                collisionLayer);

            if (frontHit.collider == null)
                return false;

            // 정면에 계단있는지 확인
            bool isStair = frontHit.collider.gameObject.layer == 8;
            if (isStair == false) return false;


            // maxStepHeight 안에서 stepCheckCount번 나눠서 위로 올려가며 시도
            int checks = Mathf.Max(1, stepCheckCount);
            float stepUnit = maxStepHeight / checks;

            for (int i = 1; i <= checks; i++)
            {
                float stepY = stepUnit * i;

                // 위로 stepY 만큼 올릴 공간이 있는지 확인
                RaycastHit2D upHit = Physics2D.BoxCast(
                    box.center,
                    box.size,
                    0.0f,
                    Vector2.up,
                    stepY,
                    collisionLayer);

                if (upHit.collider != null)
                    continue;

                // 올린 위치에서 다시 앞으로 갈 수 있는지 확인
                Bounds raisedBox = box;
                raisedBox.center += new Vector3(0.0f, stepY, 0.0f);

                RaycastHit2D moveHit = Physics2D.BoxCast(
                    raisedBox.center,
                    raisedBox.size,
                    0.0f,
                    dir,
                    distance,
                    collisionLayer);

                if (moveHit.collider == null)
                {
                    stairMove = new Vector2(moveX, stepY);
                    return true;
                }

                // 여기서 바로 포기하지 않고 다음 높이를 계속 시도
            }

            // Debug.Log("계단 이동 실패");

            return false;
        }

        #endregion

        public IEnumerator DropDownPlatform(Collider2D collider)
        {
            // 플레이어가 플랫폼 위 또는 겹쳐져있다면 플레이어의 CollisionLayer에서 해당 레이어 끄기
            float platformTop = collider.bounds.max.y;
            float platformBottom = collider.bounds.min.y;

            collisionLayer = droppingLayer;
            isDropping = true;

            // 플레이어가 플랫폼 위에서 벗어나거나 아래로 완전히 내려가면 해당 레이어 키기
            yield return new WaitUntil(() =>
            GetCastBox().max.y + skinWidth < platformBottom || //완전히 내려갈 때까지 기다리기
            GetCastBox().min.y + skinWidth > platformTop);//또는 아예 다시 올라가버리기 

            isDropping = false;
            collisionLayer = originLayer;
        }

        private Bounds GetCastBox()
        {
            Bounds box = col.bounds;

            Vector3 size = box.size;
            size.x -= skinWidth * 2;
            size.y -= skinWidth * 2;
            box.size = size;

            return box;
        }

        private bool CheckGrounded()
        {
            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, Vector2.down, groundCheckDistance, collisionLayer);
            //bool result = (hit.collider != null);
            if (hit.collider == null) return false;
            // 현재 바닥이 땅인지 계단인지 체크

            bool Stair = (hit.collider.gameObject.layer == 8);
            collisionLayer = Stair ? droppingLayer : originLayer;

            return true;
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
                    desiredMoveX = 0.0f;
                    Animator.SetBool(IS_WALK, false);

                    yield break;
                }

                Move(adjustGoalPoint, walkSpeed, false);
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
                    Animator.SetBool(IS_RUN, false);
                    yield break;
                }
                
                // 2. Target이 범위(추적 범위) 밖으로 이동할 경우
                //  : 타겟이 매우 빠르게 추적 가능한 범위 바깥으로 이동하게된 케이스
                if(Target.IsInRange(transform.position, traceRange) == false)
                {
                    nextStateCoroutine = IdleStateCoroutine();
                    desiredMoveX = 0.0f;
                    Animator.SetBool(IS_RUN, false);
                    yield break;
                }

                //======== 반복문 탈출 검사가 끝나면 =======
                // 공격하거나 (타겟이 사거리 안에 있으면)
                Vector3 adjustTargetPosition = Target.position;
                adjustTargetPosition.y = transform.position.y;

                if(transform.IsInRange(adjustTargetPosition, attackRange))
                {
                    Animator.SetBool(IS_RUN, false);
                    desiredMoveX = 0.0f;
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
                else Move(Target.position, runSpeed, true); // 이동 한다(타겟이 사거리 안에 들어올때까지)

                yield return null;
            }
        }

        protected void Move(Vector3 goalPosition, float speed, bool istracking)
        {
            // 1차원 방향(사이드뷰, 플랫포머 이니까)
            float moveDirection = UpdateDirection(goalPosition);

            desiredMoveX = moveDirection * speed;

            Animator.SetBool(IS_RUN, istracking);
            Animator.SetBool(IS_WALK, !istracking);
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
            if (IsDead) return;
            IsDead = true;

            Animator.SetBool(IS_RUN, false);
            Animator.SetBool(IS_WALK, false);
            Animator.SetBool(IS_DEAD, true);

            desiredMoveX = 0.0f;
            nextStateCoroutine = null;

            StopAllCoroutines();

            // Enemy가 죽게 되면 죽음 이펙트를 생성하고, 스스로를 삭제합니다.
            /*
            GameObject effect = Instantiate(deadEffect, 
                transform.position + deadEffectOffset, Quaternion.identity);

            // 이펙트는 일정시간이 지난뒤에 자동으로 삭제 됩니다.
            // 여기서는 Destroy(삭제할 대상, 지연시간); 함수를 사용합니다.
            Destroy(effect, deadEffectLifeTime); // effect를 deadEffectLifeTime시간 이후에 삭제한다.
            */
            //Destroy(gameObject);
        }
    }





}

