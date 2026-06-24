using UnityEngine;
using UnityEngine.InputSystem;

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
        public float     rollPower           = 10.0f;
        public float     dashPower           = 6.0f;
        public float     rollDrag            = 25.0f;   // 구르기 감쇠 (클수록 빨리 멈춤)
        public float     dashDrag            = 20.0f;   // 돌진 감쇠
        public int       maxJumpCount        = 1;
        public float     groundCheckDistance = 0.5f;
        public float     skinWidth           = 0.02f;
        public LayerMask collisionLayer;

        private Rigidbody2D rBody;
        private Collider2D  col;

        // 내부 상태 변수들
        private int     jumpCount        = 0;
        private bool    attackRequested  = false;
        private bool    jumpRequested    = false;
        public bool    crouchRequested  = false;
        public bool    rollRequested    = false;
        public bool    stickWall        = false;

        private Vector2 pendingDashDirection; //마우스방향좌표
        private float   pendingRollForward; // 구르기 방향값 -1/+1:좌/우
        private Vector2 boostVelocity    = Vector2.zero; //이동 메서드에 의한 이동값
        private Vector3 externalDelta    = Vector3.zero; //외부 플랫폼에 의한 이동값
        private float   moveInput        = 0.0f;

        // 아래의 두개는 외부에서 조회가 가능해야 합니다.
        // 읽기 전용으로 바꿔줍니다
        public float VerticalVelocity { get; private set; } = 0.0f;
        public bool IsGrounded { get; private set; } = false;

        #region Unity Methods
        private void Awake()
        {
            rBody = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();

            rBody.bodyType = RigidbodyType2D.Kinematic;
        }

        private void FixedUpdate()
        {
            IsGrounded = CheckGrounded();
            
            ApplyGravaity();
            ApplyJump();
            ApplyAttack();
            ApplyCrouch();
            //ApplyRoll();
            //StickWall();
            DecayBoostVelocity();  // ← 매 프레임 감쇠

            float horizontalVelocity = moveInput * speed + boostVelocity.x;
            float verticalVelocity   = VerticalVelocity + boostVelocity.y;

            Vector3 desireMove =
                new Vector3(horizontalVelocity, verticalVelocity) * Time.fixedDeltaTime
                + externalDelta;

            externalDelta = Vector3.zero;

            Vector3 moveVector = Vector3.zero;
            moveVector.x = ResolveAxisMovement(desireMove.x, Vector3.right);
            moveVector.y = ResolveAxisMovement(desireMove.y, Vector3.up);

            // 벽/바닥 충돌 시 해당 축 속도 제거 (점프와 동일 패턴)
            if (moveVector.x != desireMove.x) boostVelocity.x = 0f;
            if (moveVector.y != desireMove.y)
            {
                VerticalVelocity = 0.0f;
                boostVelocity.y = 0f;
            } 

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

        public void RequestCrouch()
        {
            crouchRequested = true;
        }

        public void ExitCrouch()
        {
            crouchRequested = false;
        }

        #endregion

        #region Private Methods (운동 파이프라인)

        private void ApplyGravaity()
        {
            if (IsGrounded && VerticalVelocity <= 0)
            {
                VerticalVelocity += GROUND_STICK_SPEED;
                jumpCount = 0;
            }
            else // 공중에 있는 상태
            {
                VerticalVelocity += gravity * Time.fixedDeltaTime;
            }
        }

        private float ResolveAxisMovement(float move, Vector3 axis)
        {
            if (move == 0.0f) return 0.0f;

            Vector3 dir = move > 0.0f ? axis : -axis;
            float distance = Mathf.Abs(move);

            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, dir, distance, collisionLayer);

            if (hit.collider == null) return move;

            float allowed = Mathf.Max(0, hit.distance - skinWidth);

            return Mathf.Sign(move) * allowed;
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
            if (jumpRequested == false) return;
            jumpRequested = false; //우편함을 비웁니다.

            if (jumpCount >= maxJumpCount) return;

            VerticalVelocity = jumpPower;
            jumpCount++;
            IsGrounded = false; // 바닥에서 떨어졌음을 설정합니다.
        }

        public void ApplyAttack() // 마우스 방향으로 돌진
        {
            if (attackRequested == false) return;
            attackRequested = false;
            Vector2 dir = pendingDashDirection;
            boostVelocity = dir * dashPower;

            // 돌진 중 점프 Y 속도는 유지하고 싶으면 boostVelocity.y만 덮어쓰기
            boostVelocity.x = dir.x * dashPower;
            boostVelocity.y = dir.y * dashPower;
        }

        public void ApplyCrouch()
        {
            if (crouchRequested == false) return;

            // 1. 점프중인지 아닌지 판정
            if (IsGrounded == false)
            {
                //여기 땅으로 빠르게 떨어지는 코드 필요
                //if(IsGrounded)
            }
            else if (IsGrounded)
            {
                // 2. 아래로 내려갈 수 있는지 판정
                Bounds box = GetCastBox();
                //여기 아래땅으로 떨어지는 코드 필요
                RaycastHit2D hit = Physics2D.BoxCast(
                    box.center, box.size, 0.0f, Vector2.down, groundCheckDistance, collisionLayer);
                if (hit.collider.gameObject.layer == 6 && moveInput == 0)
                {
                    Debug.Log("내려갈 수 없음");
                }
                // 3. 이동입력이 있는지 판정
                if (moveInput != 0)
                {
                    ApplyRoll();
                    crouchRequested = false;
                }
            }

        }

        public void ApplyRoll() // 바라보는 방향으로 돌진
        {
            if (rollRequested == false) return;
            rollRequested = false;
            // 구르기는 보통 지상 X만
            if (IsGrounded == false) return; // 공중 구르기 허용 시 이 줄 제거
            boostVelocity.x = pendingRollForward * rollPower;
            boostVelocity.y = 0f;
        }   

        private void StickWall()
        {
            //공중에 뜬상태고 이동중 벽에 닿았을때
            if (IsGrounded == false && moveInput != 0)
            {
                Bounds box = GetCastBox();
                if (moveInput == 1.0f)
                {
                    RaycastHit2D hit = Physics2D.BoxCast(
                    box.center, box.size, 0.0f, Vector2.right, groundCheckDistance, collisionLayer);
                    stickWall = true;
                }
                else if(moveInput == -1.0f)
                {

                }
            }
            else if (IsGrounded || boostVelocity.x != 0f)
            { 
                stickWall = false;
            }
        }

        private void DecayBoostVelocity()
        {
            if (boostVelocity.sqrMagnitude < 0.05f)
            {
                boostVelocity = Vector2.zero;
                return;
            }
            // 현재 활성 boost 종류에 따라 drag 선택 (둘 다 같으면 하나만 써도 됨)
            float drag = boostVelocity.y != 0f ? dashDrag : rollDrag;
            boostVelocity = Vector2.MoveTowards(
            boostVelocity,
            Vector2.zero,
            drag * Time.fixedDeltaTime
            );
        }

        private bool CheckGrounded()
        {
            Bounds box = GetCastBox();
            RaycastHit2D hit = Physics2D.BoxCast(
                box.center, box.size, 0.0f, Vector2.down, groundCheckDistance, collisionLayer);
            bool result = (hit.collider != null);

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


