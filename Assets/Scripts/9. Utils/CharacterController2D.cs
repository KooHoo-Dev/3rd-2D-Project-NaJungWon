using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngineInternal;

namespace Study.Utilities
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterController2D : MonoBehaviour
    {
        private const float GROUND_STICK_SPEED = -2.0f;

        [Header("기본 설정")]
        public float     speed               = 2.0f;
        public float     gravity             = -9.81f;
        public float     groundCheckDistance = 0.5f;
        public float     skinWidth           = 0.02f;
        public bool      useGravity          = true; //중력 적용
        public LayerMask collisionLayer;
        public LayerMask originLayer;
        public LayerMask droppingLayer;

        private Rigidbody2D rBody;
        private Collider2D  col;

        [Header("공격(대쉬) 설정")]
        public  float dashPower         = 6.0f; // 공격 대쉬 거리
        public  float dashDuration      = 0.12f;  // 공격 대쉬 시간(초)
        public  float dashCooldown      = 0.18f;
        public  bool  IsDashing { get; private set; } = false;

        private float dashCooldownTimer = 0.0f;

        [Header("점프 설정")]
        public float jumpPower    = 8.0f;
        public int   maxJumpCount = 1;
        public bool isJumping { get; private set; } = false;
        public bool isLanding { get; private set; } = false;

        private int  jumpCount    = 0;

        [Header("포복 설정")]
        public float fastFallSpeed = 1.0f; //빠른 하강 속도
        public bool IsCrouching { get; private set; } = false;

        private bool isFalling = false; // 플랫폼에서 떨어짐 변수

        [Header("벽 타기 설정")]
        public float stickWallDirection = 0.0f; //벽 타기 방향
        public bool stickWall { get; private set; } = false;

        [Header("구르기 설정")]
        public float rollPower    = 10.0f; // 구르기 거리
        public float rollDuration = 0.10f;  // 구르기 시간(초)
        public float rollCooldown = 0.18f;

        public bool CanStartRoll =>
            IsGrounded &&
            !IsRolling &&
            rollCooldownTimer <= 0.0f;
        public bool IsRolling { get; private set; } = false;

        private float pendingRollForward; // 구르기 방향값 -1/+1:좌/우
        private float rollCooldownTimer = 0.0f;

        [Header("공중제비 설정")]
        public float wallFlipHorizontalPower    = 6.0f; // 공중제비 좌우힘
        public float wallFlipVerticalPower      = 8.0f; // 공중제비 위쪽힘
        public float wallFlipDuration           = 0.18f; //공중제비 지속시간
        public float wallStickProbeDistance     = 0.03f; // 벽감지 거리
        public float wallReattachLockTime       = 0.08f; //벽감지 쿨타임
        public bool isWallFlipping { get; private set; } = false;

        private float wallReattachLockTimer = 0.0f;

        [Header("계단 설정")]
        public int   stepCheckCount = 4;       // 위로 몇 번 나눠서 검사할지 카운트
        public float maxStepHeight  = 0.3f;   // 한 번에 올라갈 수 있는 최대 계단 높이

        [Header("내부 이동값 설정")]
        public Vector2 pendingDashDirection { get; private set; } //마우스 방향 좌표

        private float boostTimer = 0.0f; //대쉬/구르기 이동 시간 타이머
        private Vector2 boostVelocity    = Vector2.zero; //이동 메서드에 의한 이동값

        [Header("외부 이동값 설정")]
        public float externalDrag = 12.0f;

        private Vector2 externalVelocity = Vector3.zero; //외부에 의한 이동값

        [Header("입력 요청 설정")]
        private bool attackRequested = false;
        private bool jumpRequested   = false;
        private bool crouchRequested = false;
        private bool flipRequested   = false;
        private bool rollRequested   = false;

        [Header("내부 설정")]
        private float   moveInput            = 0.0f; //기본 이동x값
        private float   baseVerticalVelocity = 0.0f; //기본 점프 + 중력값
        // 읽기 전용으로 바꿔줍니다
        public float HorizontalVelocity { get; private set; } = 0.0f; // 최종 x값
        public float VerticalVelocity { get; private set; } = 0.0f; // 최종 y값
        public float MoveVectorX { get; private set; } = 0.0f; // 벽 앞에서 멈추게 하기 용
        public int FacingDirection { get; private set; } = 1; // 정면 방향
        public bool IsGrounded { get; private set; } = false;
        public bool prevGroundedInternal { get; private set; } = false;

        #region Unity Methods
        private void Awake()
        {
            rBody = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            originLayer = collisionLayer;
            rBody.bodyType = RigidbodyType2D.Kinematic;
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            // 착지 감지: 이전 프레임 공중 → 현재 프레임 접지
            isLanding = !prevGroundedInternal && IsGrounded;
            prevGroundedInternal = IsGrounded;

            if (dashCooldownTimer > 0.0f) dashCooldownTimer -= Time.fixedDeltaTime;
            if (rollCooldownTimer > 0.0f) rollCooldownTimer -= Time.fixedDeltaTime;
            if (wallReattachLockTimer > 0.0f) wallReattachLockTimer -= Time.fixedDeltaTime;

            ApplyFlip();
            ApplyJump();
            ApplyAttack();
            ApplyCrouch();
            ApplyRoll();
            ApplyGravaity();

            if (isWallFlipping && baseVerticalVelocity <= 0f)
                isWallFlipping = false;

            UpdateBoostMovement();  // ← 매 프레임 감쇠

            if (IsDashing || IsRolling || isWallFlipping) moveInput = 0; 

            HorizontalVelocity = moveInput * speed + boostVelocity.x + externalVelocity.x;
            VerticalVelocity = baseVerticalVelocity + boostVelocity.y + externalVelocity.y;

            MoveToPlatform();

            Vector3 desireMove =
                new Vector3(HorizontalVelocity, VerticalVelocity) * Time.fixedDeltaTime;

            Bounds box = GetCastBox();

            Vector3 moveVector = Vector3.zero;

            StickWall(box, desireMove.x, Vector3.right);

            if (IsRolling && ResolveRollMovementBySteps(box, desireMove.x, out Vector2 rollMove))
            {
                moveVector = rollMove;

                // 구르기 도중 벽에 막혀 일부만 이동했으면 힘 제거
                if (Mathf.Abs(rollMove.x) < Mathf.Abs(desireMove.x))
                    boostVelocity.x = 0.0f;

                if (rollMove.y > 0.0f && baseVerticalVelocity < 0.0f)
                {
                    baseVerticalVelocity = 0.0f;
                }
            }
            else if (IsDashing == false && TryResolveStairMovement(box, desireMove.x, Vector3.right, out Vector2 stepMove))
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

                if (moveVector.x != desireMove.x) boostVelocity.x = 0f;
                if (moveVector.y != desireMove.y)
                {
                    baseVerticalVelocity = 0.0f;
                    boostVelocity.y = 0f;
                    VerticalVelocity = 0.0f;
                }
            }

            // 죽은 뒤에도 넉백은 잠깐 남게 두고 감쇠
            externalVelocity = Vector2.Lerp(
                externalVelocity,
                Vector2.zero,
                externalDrag * Time.fixedDeltaTime);
            if (IsGrounded && externalVelocity.y < 0.0f)
                externalVelocity.y = 0.0f;

            MoveVectorX = (Mathf.Abs(moveVector.x) > 0) ? 1 : 0;

            rBody.MovePosition(transform.position + moveVector);
        }

        #endregion

        #region Public Methods (명령 통로)

        public void SetMoveInput(float horizontal)
        {
            if (IsDashing || IsRolling || isWallFlipping) return;

            moveInput = Mathf.Clamp(horizontal, -1.0f, 1.0f);
            SetFacingDirection(moveInput);
        }
        public void SetFacingDirection(float direction)
        {
            if (direction == 0f) return;
            FacingDirection = (direction < 0f) ? -1 : 1;
        }

        public void ApplyImpact(Vector2 direction, float power)
        {
            if (power <= 0.0f) return;

            moveInput = 0.0f;

            externalVelocity += direction * power;
        }

        public void RequestAttack(Vector2 mouseVec)
        {
            pendingDashDirection = mouseVec;
            attackRequested = true;
        }

        public bool RequestJump()
        {
            if (jumpCount >= maxJumpCount) return false;
            jumpRequested = true;
            return true;
        }

        public bool RequestFlip()
        {
            if (stickWall == false) return false;

            flipRequested = true;
            return true;
        }


        public void RequestCrouch(bool apply)
        {
            crouchRequested = apply;
        }

        public void RequestRoll(float forward)
        {
            pendingRollForward = forward;
            rollRequested = true;
        }

        public void KnockbackReset()
        {
            externalVelocity = Vector3.zero;
        }

        // 라운드 재시작 시 초기화
        public void ResetRuntimeState()
        {
            jumpRequested = false;
            attackRequested = false;
            crouchRequested = false;
            flipRequested = false;
            rollRequested = false;

            isJumping = false;
            isLanding = false;
            isFalling = false;
            stickWall = false;
            stickWallDirection = 0.0f;
            isWallFlipping = false;

            IsGrounded = false;
            prevGroundedInternal = false;
            IsCrouching = false;
            IsDashing = false;
            IsRolling = false;

            moveInput = 0.0f;
            baseVerticalVelocity = 0.0f;
            boostVelocity = Vector2.zero;
            boostTimer = 0.0f;
            externalVelocity = Vector2.zero;

            dashCooldownTimer = 0.0f;
            rollCooldownTimer = 0.0f;
            wallReattachLockTimer = 0.0f;

            useGravity = true;
            collisionLayer = originLayer;
        }

        #endregion

        #region 플랫폼 이동
        public void MoveToPlatform() //기본 플랫폼 이동 
        {
            if (isFalling == true || VerticalVelocity > 0)
            {
                collisionLayer = droppingLayer;
                return;
            }

            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                Vector2.down,
                groundCheckDistance,
                collisionLayer);

            if (hit.collider == null)
            {
                collisionLayer = originLayer;
                return;
            }

            //하강중 벽에 낌처리
            bool isStuck =
                VerticalVelocity <= 0 &&
                hit.collider.gameObject.layer == 7 &&
                (box.min.y + skinWidth) < hit.collider.bounds.max.y;

            if (isStuck)
            {
                StartCoroutine(DropDownPlatform(hit.collider));
            }
        }

        public IEnumerator DropDownPlatform(Collider2D collider)
        {
            // 플레이어가 플랫폼 위 또는 겹쳐져있다면 플레이어의 CollisionLayer에서 해당 레이어 끄기
            float platformTop = collider.bounds.max.y;
            float platformBottom = collider.bounds.min.y;

            collisionLayer = droppingLayer;
            isFalling = true;

            // 플레이어가 플랫폼 위에서 벗어나거나 아래로 완전히 내려가면 해당 레이어 키기
            yield return new WaitUntil(() =>
            GetCastBox().max.y + skinWidth < platformBottom || //완전히 내려갈 때까지 기다리기
            GetCastBox().min.y + skinWidth > platformTop);//또는 아예 다시 올라가버리기 

            isFalling = false;
            collisionLayer = originLayer;
        }

        #endregion

        #region Private Methods (운동 파이프라인)

        private void ApplyGravaity()
        {
            if (useGravity == false) return;

            if (IsGrounded && baseVerticalVelocity <= 0)
            {
                baseVerticalVelocity += GROUND_STICK_SPEED;
                jumpCount = 0;
            }
            else // 공중에 있는 상태
            {
                baseVerticalVelocity += gravity * Time.fixedDeltaTime;
            }
        }

        private void UpdateBoostMovement()
        {
            bool wasDashing = IsDashing;

            // 타이머가 없으면 두 상태 모두 확실히 해제
            if (boostTimer <= 0.0f)
            {
                boostTimer = 0.0f;
                boostVelocity = Vector2.zero;
                IsDashing = false;
                IsRolling = false;
                return;
            }

            // 동시에 true 방지
            if (IsDashing && IsRolling)
                IsRolling = false; // 대쉬 우선

            // 타이머 감소
            boostTimer -= Time.fixedDeltaTime;

            // 타이머 종료 시 상태 해제
            if (boostTimer <= 0.0f)
            {
                boostTimer = 0.0f;
                boostVelocity = Vector2.zero;
                IsDashing = false;
                IsRolling = false;
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
            }

            //Debug.LogError("계단 이동 실패");

            return false;
        }

        private bool ResolveRollMovementBySteps(Bounds startBox, float moveX, out Vector2 moveResult) //구르기 계단 이동값 보정
        {
            moveResult = Vector2.zero;

            if (Mathf.Approximately(moveX, 0.0f))
                return false;

            // 구르기 이동을 잘게 나눠서 step 방식으로 처리
            int sliceCount = Mathf.CeilToInt(Mathf.Abs(moveX) / maxStepHeight);
            sliceCount = Mathf.Max(1, sliceCount);

            float unitMoveX = moveX / sliceCount;

            Bounds box = startBox;

            for (int i = 0; i < sliceCount; i++)
            {
                Vector2 partMove = Vector2.zero;

                // 1) 먼저 "올라가는 step" 시도
                if (TryResolveStairMovement(box, unitMoveX, Vector3.right, out Vector2 stepUpMove))
                {
                    partMove = stepUpMove;
                    moveResult += partMove;
                    box.center += (Vector3)partMove;

                    if (partMove.y > 0.0f && baseVerticalVelocity < 0.0f)
                    {
                        baseVerticalVelocity = 0.0f;
                    }

                    continue;
                }

                // 2) step up이 아니면 일단 수평으로 갈 수 있는 만큼만 이동
                float resolvedX = ResolveAxisMovement(box, unitMoveX, Vector3.right);

                if (Mathf.Approximately(resolvedX, 0.0f))
                {
                    // 더 이상 앞으로 못 가면 종료
                    break;
                }

                partMove.x = resolvedX;
                box.center += new Vector3(resolvedX, 0.0f, 0.0f);

                // 3) 수평 이동 후 "내려가는 step" 시도
                // maxStepHeight 만큼 아래에 바닥이 있으면 그만큼 내려 붙음
                if (TrySnapDownStep(box, maxStepHeight, out float downMove))
                {
                    partMove.y = downMove;
                    box.center += new Vector3(0.0f, downMove, 0.0f);
                }

                moveResult += partMove;

                // 벽 등에 걸려 일부만 이동했다면 더 진행하지 않음
                if (Mathf.Abs(resolvedX) < Mathf.Abs(unitMoveX))
                    break;
            }

            return moveResult != Vector2.zero;
        }

        private bool TrySnapDownStep(Bounds box, float maxDownDistance, out float downMove)
        {
            downMove = 0.0f;

            if (maxDownDistance <= 0.0f)
                return false;

            RaycastHit2D downHit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                Vector2.down,
                maxDownDistance + skinWidth,
                collisionLayer);

            if (downHit.collider == null)
                return false;

            // 이미 거의 붙어 있으면 굳이 내릴 필요 없음
            float dropDistance = downHit.distance - skinWidth;
            if (dropDistance <= 0.0f)
                return false;

            downMove = -Mathf.Min(dropDistance, maxDownDistance);
            return true;
        }

        #endregion

        private Bounds GetCastBox()
        {
            Bounds box = col.bounds; 

            Vector3 size = box.size;
            size.x -= skinWidth * 2;
            size.y -= skinWidth * 2;
            box.size = size;

            return box;
        }

        private void ApplyJump()
        {
            if(IsGrounded == false)
            {
                jumpRequested = false;
                return;
            }
            if (jumpRequested == false) return;
            jumpRequested = false; //우편함을 비웁니다.

            if (jumpCount >= maxJumpCount) return;

            if (useGravity == false) useGravity = true; //중력값 미적용 상태라면 키기
            boostVelocity = Vector2.zero; // 점프시 남은 공격값 삭제
            boostTimer = 0f;

            baseVerticalVelocity = jumpPower;
            jumpCount++;
            IsGrounded = false; // 바닥에서 떨어졌음을 설정합니다.
            isJumping = true;
        }

        public void ApplyAttack() // 마우스 방향으로 돌진
        {
            if (attackRequested == false) return;
            attackRequested = false;
            if (dashCooldownTimer > 0) return;
            if (dashDuration <= 0.0f) return;

            Vector2 dir = pendingDashDirection.normalized;
            if (dir.sqrMagnitude <= 0.0001f) return;

            // ✅ 이미 있던 벡터의 x부호만 보고 정면 전환. 새 변수 없음.
            SetFacingDirection(dir.x);

            baseVerticalVelocity = 0; // 공격시 남은 점프값 삭제
            IsDashing = true; // 대쉬 시작

            float dashSpeed = dashPower / dashDuration;   // 거리 / 시간 = 속도
            boostVelocity = dir * dashSpeed;
            boostTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
        }

        public void ApplyCrouch()
        {
            if (IsRolling)
                return;

            if (crouchRequested)
            {
                // 1. 공중이면 빠른 하강
                if (!IsGrounded)
                {
                    //하강 속도를 강하게 주는 방식으로 처리.
                    baseVerticalVelocity += -fastFallSpeed;
                }

                // 2. 아래로 내려갈 수 있는 지면이면 관통 하강
                Bounds box = GetCastBox();
                RaycastHit2D hit = Physics2D.BoxCast(
                    box.center,
                    box.size,
                    0.0f,
                    Vector2.down,
                    groundCheckDistance,
                    collisionLayer);

                if (hit.collider == null) return;
                bool canDropDown = hit.collider.gameObject.GetComponent<PlatformEffector2D>();

                if (canDropDown)
                {
                    //Debug.Log("아래로 내려가야함");
                    StartCoroutine(DropDownPlatform(hit.collider));
                    return;
                }
                //else Debug.Log("내려갈 수 없는 지형입니다.");

                // 3. 아니면 crouch loop 진입
                // [수정] 구르기 시작 판정은 PlayerController.UpdateRollInput() 한 곳에서만 처리합니다.
                // 여기서는 앉기 상태만 관리하고, 입력 순서(S→A/D, A/D→S)에 따른 판정 차이를 만들지 않게 합니다.
                IsCrouching = true;
                //else IsCrouching = false;
            }
        }

        public void ApplyRoll() // 바라보는 방향으로 돌진 (경사 따라가게 수정할것)
        {
            if (rollRequested == false) return;
            rollRequested = false;
            if (IsGrounded == false) return;
            if (rollCooldownTimer > 0) return;
            if (rollDuration <= 0.0f) return;

            IsCrouching = false;
            IsRolling = true; // 구르기 시작

            float rollSpeed = rollPower / rollDuration;   // 거리 / 시간 = 속도
            boostVelocity = new Vector2(pendingRollForward * rollSpeed, 0.0f);
            boostTimer = rollDuration;
            rollCooldownTimer = rollCooldown;
        }

        private void StickWall(Bounds box, float move, Vector3 axis)
        {
            // 착지하면 상태 정리
            if (IsGrounded)
            {
                if (stickWall)
                {
                    stickWall = false;
                    stickWallDirection = 0.0f;
                }

                isWallFlipping = false;
                wallReattachLockTimer = 0.0f;
                flipRequested = false;
                return;
            }

            // 벽점프 직후 바로 같은 벽에 재부착되는 것 방지
            if (wallReattachLockTimer > 0.0f)
                return;

            // 이미 벽에 붙어있을 때
            if (stickWall)
            {
                // 실제로 아직도 벽에 닿아있는지 먼저 확인
                // 입력이 없어도 위/아래로 벽에서 벗어나면 stickWall 해제
                if (IsStillTouchingWall(box) == false)
                {
                    stickWall = false;
                    stickWallDirection = 0.0f;
                    return;
                }

                // 반대 방향 입력 시 해제
                if (!Mathf.Approximately(moveInput, 0.0f))
                {
                    bool oppositeInput = Mathf.Sign(moveInput) != Mathf.Sign(stickWallDirection);
                    if (oppositeInput)
                    {
                        stickWall = false;
                        stickWallDirection = 0.0f;
                    }
                }

                return;
            }

            float probeDistance = Mathf.Max(Mathf.Abs(move), wallStickProbeDistance);

            // 평소에는 입력 방향으로만 벽 부착 검사
            if (!Mathf.Approximately(moveInput, 0.0f))
            {
                Vector3 dir = moveInput > 0 ? axis : -axis;
                TryAttachWall(box, dir, probeDistance);
                return;
            }

            // wall flip 중에는 입력이 없어도 좌우 벽 검사
            if (isWallFlipping)
            {
                if (TryAttachWall(box, Vector3.left, wallStickProbeDistance))
                    return;

                TryAttachWall(box, Vector3.right, wallStickProbeDistance);
            }
        }

        public void ApplyFlip()
        {
            if (flipRequested == false) return;

            flipRequested = false;

            if (stickWall == false) return;

            float awayDirection = -stickWallDirection; // 벽 반대 방향

            // 벽 부착 해제
            stickWall = false;
            stickWallDirection = 0.0f;

            // 상태 정리
            IsGrounded = false;
            IsCrouching = false;
            IsRolling = false;
            IsDashing = false;
            useGravity = true;

            // 공중제비 시작
            isWallFlipping = true;
            wallReattachLockTimer = wallReattachLockTime;

            // 정면도 공중제비 방향으로 전환
            SetFacingDirection(awayDirection);

            // 세로는 점프, 가로는 짧은 추진
            baseVerticalVelocity = wallFlipVerticalPower;
            boostVelocity = new Vector2(awayDirection * wallFlipHorizontalPower, 0.0f);
            boostTimer = wallFlipDuration;

            // 벽점프를 점프 1회 사용으로 볼지 여부는 취향
            jumpCount = maxJumpCount;
        }
        private bool IsStillTouchingWall(Bounds box)
        {
            if (Mathf.Approximately(stickWallDirection, 0.0f))
                return false;

            Vector3 wallDir = (stickWallDirection > 0.0f) ? Vector3.right : Vector3.left;

            RaycastHit2D hit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                wallDir,
                wallStickProbeDistance,
                collisionLayer);

            if (hit.collider == null)
                return false;

            float wallSide = -Mathf.Sign(hit.normal.x);
            if (Mathf.Approximately(wallSide, 0.0f))
                return false;

            return Mathf.Sign(wallSide) == Mathf.Sign(stickWallDirection);
        }

        private bool TryAttachWall(Bounds box, Vector3 dir, float distance)
        {
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                dir,
                distance,
                collisionLayer);

            if (hit.collider == null)
                return false;
            if (hit.collider.gameObject.layer != 9) return false;
            float newWallDirection = -Mathf.Sign(hit.normal.x);
            if (Mathf.Approximately(newWallDirection, 0.0f))
                return false;

            stickWall = true;
            stickWallDirection = newWallDirection;
            isWallFlipping = false;

            // 벽에 다시 붙으면 가로 추진 제거
            boostVelocity.x = 0.0f;

            if (baseVerticalVelocity < 0.0f)
                baseVerticalVelocity = 0.0f;

            return true;
        }

        private bool CheckGrounded()
        {
            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, Vector2.down, groundCheckDistance, collisionLayer);
            //bool result = (hit.collider != null);
            if (hit.collider == null) return false;
            // 현재 바닥이 땅인지 계단인지 체크
            bool isStuck =
                VerticalVelocity <= 0 &&
                hit.collider.gameObject.GetComponent<PlatformEffector2D>() &&
                (box.min.y + skinWidth) < hit.collider.bounds.max.y;
            if (isStuck) return false;

            bool Stair = (hit.collider.gameObject.layer == 8);
            collisionLayer = Stair ? droppingLayer : originLayer;

            isJumping = false;
            return true;
        }

        #endregion

        #region ResolveAxisMovement 시각화 코드

        [Header("캐스트 시각화 설정")]
        // 시각화 표시 on/off
        public bool showCastGizmos = true;

        // 시각화에서 박스를 쏴 볼 거리 (보기 좋게, 실제 이동 거리와는 무관)
        public float castPreviewDistance = 0.5f;

        // 선/박스를 그릴 때 쓰는 1x1 흰색 텍스처
        private static Texture2D whiteTexture;

        // # OnGUI : 매 프레임 화면(게임 뷰)에 GUI를 그리는 이벤트 함수
        private void OnGUI()
        {
            if (showCastGizmos == false) return;

            Camera cam = Camera.main;
            if (cam == null || col == null) return;

            // 캐스트에 쓸 박스(콜라이더보다 살짝 작게)를 여기서 직접 계산합니다.
            Bounds bounds = col.bounds;
            Vector2 boxCenter = bounds.center;
            Vector2 boxSize = (Vector2)bounds.size - Vector2.one * (skinWidth * 2.0f);

            // 상/하/좌/우 네 방향으로 박스를 쏴 보고 그 결과를 그립니다.
            DrawBoxCast(cam, boxCenter, boxSize, Vector2.up);
            DrawBoxCast(cam, boxCenter, boxSize, Vector2.down);
            DrawBoxCast(cam, boxCenter, boxSize, Vector2.left);
            DrawBoxCast(cam, boxCenter, boxSize, Vector2.right);
        }

        // 한 방향으로 BoxCast를 직접 수행하고, 그 모양을 박스로 그립니다.
        private void DrawBoxCast(Camera cam, Vector2 center, Vector2 size, Vector2 dir)
        {
            RaycastHit2D hit = Physics2D.BoxCast(
                center, size, 0.0f, dir, castPreviewDistance, collisionLayer);

            Rect startRect = WorldBoxToGuiRect(cam, center, size);
            Rect endRect = WorldBoxToGuiRect(cam, center + dir * castPreviewDistance, size);

            // 검사 영역(시작~끝 전체) : 노란색
            DrawRectOutline(Encapsulate(startRect, endRect), 2.0f, Color.yellow);
            // 캐스트 시작 박스 : 흰색
            DrawRectOutline(startRect, 2.0f, Color.white);

            if (hit.collider != null)
            {
                // 무언가 걸렸으면 걸린 자리에 빨간 박스
                Rect hitRect = WorldBoxToGuiRect(cam, center + dir * hit.distance, size);
                DrawRectOutline(hitRect, 2.0f, Color.red);
            }
            else
            {
                // 아무것도 없으면 끝까지 간 자리에 초록 박스
                DrawRectOutline(endRect, 2.0f, Color.green);
            }
        }

        // 월드 공간의 축정렬 박스(center, size)를 GUI 좌표 Rect로 변환합니다.
        private Rect WorldBoxToGuiRect(Camera cam, Vector2 center, Vector2 size)
        {
            float z = transform.position.z;
            Vector3 worldMin = new Vector3(center.x - size.x * 0.5f, center.y - size.y * 0.5f, z);
            Vector3 worldMax = new Vector3(center.x + size.x * 0.5f, center.y + size.y * 0.5f, z);

            // 월드 좌표 -> 스크린 좌표 (스크린은 좌하단이 원점)
            Vector3 a = cam.WorldToScreenPoint(worldMin);
            Vector3 b = cam.WorldToScreenPoint(worldMax);

            float left = Mathf.Min(a.x, b.x);
            float right = Mathf.Max(a.x, b.x);
            // GUI 좌표는 좌상단이 원점이고 y가 아래로 증가 -> y를 뒤집어 줍니다.
            float top = Screen.height - Mathf.Max(a.y, b.y);
            float bottom = Screen.height - Mathf.Min(a.y, b.y);

            return new Rect(left, top, right - left, bottom - top);
        }

        // 두 Rect를 모두 감싸는 가장 작은 Rect를 만듭니다.
        private Rect Encapsulate(Rect a, Rect b)
        {
            return Rect.MinMaxRect(
                Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin),
                Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        }

        // 사각형 "테두리"를 얇은 박스 4개(위/아래/왼/오른)로 그립니다.
        private void DrawRectOutline(Rect r, float thickness, Color color)
        {
            if (whiteTexture == null)
            {
                whiteTexture = new Texture2D(1, 1);
                whiteTexture.SetPixel(0, 0, Color.white);
                whiteTexture.Apply();
            }

            Color prev = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(new Rect(r.xMin, r.yMin, r.width, thickness), whiteTexture);              // 위
            GUI.DrawTexture(new Rect(r.xMin, r.yMax - thickness, r.width, thickness), whiteTexture);  // 아래
            GUI.DrawTexture(new Rect(r.xMin, r.yMin, thickness, r.height), whiteTexture);             // 왼쪽
            GUI.DrawTexture(new Rect(r.xMax - thickness, r.yMin, thickness, r.height), whiteTexture); // 오른쪽

            GUI.color = prev;
        }


        #endregion
    }
}


