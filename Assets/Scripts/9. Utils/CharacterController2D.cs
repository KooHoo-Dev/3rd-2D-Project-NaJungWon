using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Study.Utilities
{
    // RequireComponent?
    // : 아래의 선언된 클래스가 동작하려면 typeof(컴포넌트)의
    // 컴포넌트가 필요하다고 강제하는 키워드 입니다.
    // GameObject에 Attach하게 되면 필요한 컴포넌트를
    // 자동으로 생성합니다.
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterController2D : MonoBehaviour
    {
        private const float GROUND_STICK_SPEED = -2.0f;

        [Header("Settings")]
        public float     speed               = 2.0f;
        public float     gravity             = -9.81f;
        public float     jumpPower           = 8.0f;
        public float     dashPower           = 6.0f; // 공격 대쉬 거리
        public float dashDuration = 0.12f;  // 공격 대쉬 시간(초)
        public float     rollPower           = 10.0f; // 구르기 거리
        public float rollDuration = 0.10f;  // 구르기 시간(초)        public float     dashDrag            = 20.0f;   // 돌진 감쇠
        public int       maxJumpCount        = 1;
        public float     groundCheckDistance = 0.5f;
        public float     skinWidth           = 0.02f;
        public LayerMask collisionLayer;
        public LayerMask originLayer;
        public LayerMask droppingLayer;
        //public LayerMask layerB;


        private Rigidbody2D rBody;
        private Collider2D  col;

        // 내부 상태 변수들
        private int    jumpCount        = 0;
        public bool    jumpRequested = false;
        public bool    attackRequested = false;
        public bool    crouchRequested = false;
        public float fastFallSpeed = 1.0f; //빠른 하강 속도
        public bool isDropping = false;
        public bool    rollRequested = false;
        public bool    stickWall = false;
        public bool useGravity = true; //중력 적용

        // 계단 오르기 변수
        public LayerMask stairLayer;         // 계단 레이어
        public float maxStepHeight = 0.3f;   // 한 번에 올라갈 수 있는 최대 계단 높이
        public int stepCheckCount = 4;       // 위로 몇 번 나눠서 검사할지

        private Vector2 pendingDashDirection; //마우스 방향 좌표
        private float   pendingRollForward; // 구르기 방향값 -1/+1:좌/우
        private Vector2 boostVelocity    = Vector2.zero; //이동 메서드에 의한 이동값
        private float boostTimer = 0.0f; //대쉬/구르기 이동 시간
        private Vector3 externalDelta    = Vector3.zero; //외부 플랫폼에 의한 이동값

        private float   moveInput        = 0.0f; //기본 이동x값
        private float   baseVerticalVelocity = 0.0f; //기본 점프 + 중력값

        // 읽기 전용으로 바꿔줍니다
        public float HorizontalVelocity { get; private set; } = 0.0f; // 최종 x값
        public float VerticalVelocity { get; private set; } = 0.0f; // 최종 y값

        public float MoveVectorX { get; private set; } = 0.0f; 
        public bool IsGrounded { get; private set; } = false;
        public bool IsCrouching { get; private set; } = false;
        public bool IsRolling = false;

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
            
            
            ApplyJump();
            ApplyAttack();
            ApplyCrouch();
            ApplyRoll();
            ApplyGravaity();
            UpdateBoostMovement();  // ← 매 프레임 감쇠
            

            HorizontalVelocity = moveInput * speed + boostVelocity.x;
            VerticalVelocity = baseVerticalVelocity + boostVelocity.y;

            MoveToPlatform();

            Vector3 desireMove =
                new Vector3(HorizontalVelocity, VerticalVelocity) * Time.fixedDeltaTime
                + externalDelta;

            externalDelta = Vector3.zero;

            Bounds box = GetCastBox();

            Vector3 moveVector = Vector3.zero;

            //StickWall(box, desireMove.x, Vector3.right);

            if (TryResolveStairMovement(box, desireMove.x, Vector3.right, out Vector2 stepMove))
            {
                moveVector.x = stepMove.x;
                moveVector.y = stepMove.y;

                
                // 계단을 올라가는 중에는 아래로 눌리는 속도를 제거합니다.
                if (stepMove.y > 0.0f && baseVerticalVelocity < 0.0f)
                {
                    baseVerticalVelocity = 0.0f;
                    //VerticalVelocity = 0.0f;
                }
            }
            else
            {
                float boxX = ResolveAxisMovement(box, desireMove.x, Vector3.right);
                box.center += new Vector3(boxX, 0, 0);
                float boxY = ResolveAxisMovement(box, desireMove.y, Vector3.up);

                moveVector = new Vector3(boxX, boxY, 0);

                // 벽/바닥 충돌 시 해당 축 속도 제거 (점프와 동일 패턴)
                if (moveVector.x != desireMove.x) boostVelocity.x = 0f;
                if (moveVector.y != desireMove.y)
                {
                    baseVerticalVelocity = 0.0f;
                    boostVelocity.y = 0f;
                    VerticalVelocity = 0.0f;
                }
            }

            MoveVectorX = (Mathf.Abs(moveVector.x) > 0) ? 1 : 0;

            rBody.MovePosition(transform.position + moveVector);
        }

        #endregion

        #region Public Methods (명령 통로)

        public void SetMoveInput(float horizontal)
        {
            moveInput = Mathf.Clamp(horizontal, -1.0f, 1.0f);
        }

        public void AddExternalMovement(Vector3 delta)
        {
            externalDelta += delta;
        }

        public void RequestAttack(Vector2 mouseVec)
        {
            if (mouseVec.sqrMagnitude < 0.0001f) return;
            pendingDashDirection = mouseVec.normalized;
            attackRequested = true;
        }

        public bool RequestJump()
        {
            if (jumpCount >= maxJumpCount) return false;
            jumpRequested = true;
            return true;
        }

        public void ApplyCrouch(bool apply)
        {
            crouchRequested = apply;
        }

        public void RequestRoll(float forward)
        {
            pendingRollForward = forward;
            rollRequested = true;
        }

        #endregion

        #region 플랫폼 이동 (isStuck으로 내려갈때 버벅임 해결할 것)
        public void MoveToPlatform() //기본 플랫폼 이동 
        {
            if (isDropping == true || VerticalVelocity > 0)
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
                hit.collider.gameObject.GetComponent<PlatformEffector2D>() &&
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
            isDropping = true;

            // 플레이어가 플랫폼 위에서 벗어나거나 아래로 완전히 내려가면 해당 레이어 키기
            yield return new WaitUntil(() =>
            GetCastBox().max.y + skinWidth < platformBottom || //완전히 내려갈 때까지 기다리기
            GetCastBox().min.y + skinWidth > platformTop);//또는 아예 다시 올라가버리기 

            isDropping = false;
            collisionLayer = originLayer;
        }

        #endregion

        #region Private Methods (운동 파이프라인)

        private void ApplyGravaity()
        {
            // 여기 공격중 중력끄는 코드 필요
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
            if (boostTimer <= 0.0f)
            {
                boostTimer = 0.0f;
                boostVelocity = Vector2.zero;
                return;
            }

            boostTimer -= Time.fixedDeltaTime;

            if (boostTimer <= 0.0f)
            {
                boostTimer = 0.0f;
                boostVelocity = Vector2.zero;
            }
        }

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

        private bool TryResolveStairMovement(Bounds box, float moveX, Vector3 axis, out Vector2 stairMove) //계단 이동값 보정
        {
            stairMove = Vector2.zero;

            if (moveX == 0.0f) return false;
            if (IsGrounded == false || baseVerticalVelocity > 0.0f) return false;

            Vector3 dir = moveX > 0.0f ? axis : -axis;
            float distance = Mathf.Abs(moveX);

            RaycastHit2D hit = Physics2D.BoxCast(
                box.center,
                box.size,
                0.0f,
                dir,
                distance,
                collisionLayer);

            if (hit.collider == null)
                return false;

            // 레이어가 계단이 아니면 false
            if (hit.collider.gameObject.layer != 8)
            {
                //Debug.Log("계단 레이어 아님");
                return false;
            }

            int count = 0; // 무한반복 해제용 안전장치

            while(true) //위로 조금씩 올려서 다시 이동시키기
            {
                if (count > 5)
                {
                    //Debug.Log("횟수 초과");
                    break;
                }

                float stepY = maxStepHeight;

                RaycastHit2D upHit = Physics2D.BoxCast(
                    box.center,
                    box.size,
                    0.0f,
                    Vector2.up,
                    stepY,
                    collisionLayer);

                if (upHit.collider != null)
                {
                    count++;
                    stepY += maxStepHeight;
                    
                    //Debug.Log("카운트 증가");
                    continue;
                }

                box.center += new Vector3(0, stepY, 0);

                RaycastHit2D moveHit = Physics2D.BoxCast(
                    box.center,
                    box.size,
                    0.0f,
                    dir,
                    distance,
                    collisionLayer);

                if (moveHit.collider == null)
                {
                    stairMove = new Vector2(moveX, stepY);
                    return true;
                }
                else
                {
                    Debug.LogError("이동 불가");
                    break;
                }
            }
            return false;
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
        }

        public void ApplyAttack() // 마우스 방향으로 돌진
        {
            if (attackRequested == false) return;
            attackRequested = false;

            if (dashDuration <= 0.0f) return;

            baseVerticalVelocity = 0; // 공격시 남은 점프값 삭제

            Vector2 dir = pendingDashDirection.normalized;

            float dashSpeed = dashPower / dashDuration;   // 거리 / 시간 = 속도
            boostVelocity = dir * dashSpeed;
            boostTimer = dashDuration;
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
                    //여기 아래로 내려가는 코드 써야함
                    //Debug.Log("아래로 내려가야함");
                    StartCoroutine(DropDownPlatform(hit.collider));
                    return;
                }
                //else Debug.Log("내려갈 수 없는 지형입니다.");

                // 3. 아니면 crouch loop 진입
                IsCrouching = true;

                if (IsCrouching && MoveVectorX != 0)
                {
                    RequestRoll(moveInput);
                    return;
                }
                //else IsCrouching = false;
            }
        }

        public void ApplyRoll() // 바라보는 방향으로 돌진 (경사 따라가게 수정할것)
        {
            if (rollRequested == false) return;
            rollRequested = false;

            if (IsGrounded == false) return;
            if (rollDuration <= 0.0f) return;

            IsCrouching = false;
            IsRolling = true;

            float rollSpeed = rollPower / rollDuration;   // 거리 / 시간 = 속도
            boostVelocity = new Vector2(pendingRollForward * rollSpeed, 0.0f);
            boostTimer = rollDuration;
        }   

        private void StickWall(Bounds box, float move, Vector3 axis) // 만들어야함
        {
            //1. 공중아니면 안됌
            if (IsGrounded == false)
            {
                if (stickWall) stickWall = false;
                return;
            }

            if (move == 0.0f) return;

            Vector3 dir = move > 0.0f ? axis : -axis;
            float distance = Mathf.Abs(move);

            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, dir, distance, collisionLayer);

            //2. 입력 방향에 붙을수 있는 벽이 없으면 리턴 (레이어 설정 Wall등으로 다르게처리해야할듯)
            if (hit.collider == null) return;

            if(moveInput != 0)
            {
                stickWall = true;
                //3.벽에 붙은상태로 벽방향이 아닌 뚫린 방향 으로 입력값이 존재하면 벽붙기 상태가 해제됌

            }
            // w(점프)입력시 붙은 위치에서 대각선방향으로 공중제비를 시전. 
            if (stickWall && jumpRequested)
            {

            }
        }

        private bool CheckGrounded()
        {
            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, Vector2.down, groundCheckDistance, collisionLayer);
            bool result = (hit.collider != null);

            // 현재 바닥이 땅인지 계단인지 체크
            if (result)
            {
                bool Stair = (hit.collider.gameObject.layer == 8);
                collisionLayer = Stair ? droppingLayer : originLayer;
            }
            
            
            return result;
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


