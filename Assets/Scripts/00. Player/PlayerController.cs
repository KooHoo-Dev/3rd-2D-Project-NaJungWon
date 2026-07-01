using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Study.Utilities;
using UnityEngine.Rendering.Universal.Internal;


namespace Study_ActionPlatformer
{
    // 조작, 애니메이션 재생을 담당하는 클래스

    public class PlayerController : MonoBehaviour
    {
        // 애니메이터 상태 Tag

        private const string ANIM_TAG_ATTACK = "Attack";
        private const string ANIM_TAG_ROLL = "Roll";
        private const string ANIM_TAG_MOVEMENT = "Movement";
        private const string ANIM_TAG_JUMP = "Jump";

        public static readonly int MOVEMENT    = Animator.StringToHash("Movement");
        public static readonly int IS_ATTACK   = Animator.StringToHash("IsAttack");
        public static readonly int IS_Crouch   = Animator.StringToHash("IsCrouch");
        public static readonly int IS_ROLL     = Animator.StringToHash("IsRoll");
        public static readonly int IS_GROUNDED = Animator.StringToHash("IsGrounded");
        public static readonly int JUMP        = Animator.StringToHash("IsJump");
        public static readonly int IS_FLIP     = Animator.StringToHash("IsFlip");
        public static readonly int DESCENDING  = Animator.StringToHash("Descending");
        public static readonly int STICK_WALL = Animator.StringToHash("StickWall");

        public static readonly int EFFECT_ATTACK = 0;
        public static readonly int EFFECT_JUMP = 1;
        public static readonly int EFFECT_LAND = 2;

        public Animator Animator { get; private set; }
        public Animator[] effectAnim; //캐릭터 연출 애니메이터
        public Vector3 LandCloudOffset;
        public Vector3 JumpCloudOffset;
        public Vector3 FlipCloudOffset;

        private SpriteRenderer SpriteRenderer { get; set; }
        private CharacterController2D Controller2D { get; set; }

        public int FacingDirection { get; private set; } = 1;

        // 각 상태를 제어하기 위한 멤버 변수들
        private Dictionary<int, PlayerAnimStateBase> StateDic { get; set; } = new();
        private PlayerAnimStateBase defaultState; // 예외처리를 위한 기본 상태
        private PlayerAnimStateBase currentState; // 상태객체를 담아서 제어하기 위한 빈 객체 변수

        private bool prevIsGrounded = false;
        private bool prevIsJumping = false;
        private bool prevIsWallFlipping = false;

        private bool prevCrouchHeld = false;
        private bool prevMoveHeld = false;
        private float prevHorizontalRaw = 0.0f;

        [field: SerializeField] private Transform Cursor { get; set; }


        private void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Controller2D = GetComponent<CharacterController2D>();

            defaultState = new MovementState(this);

            StateDic.Add(Animator.StringToHash(ANIM_TAG_MOVEMENT), defaultState);
            StateDic.Add(Animator.StringToHash(ANIM_TAG_ATTACK), new AttackState(this));
            StateDic.Add(Animator.StringToHash(ANIM_TAG_ROLL), new RollState(this));
        }

        private void Update()
        {
            UpdateAnimState();
            UpdateJumpInput();
            UpdateDownInput();
            UpdateRollInput();
            UpdateGroundedAnimation();
            UpdateEffectAnimation();
            UpdateAttackInput();
            SyncFacingVisual();
        }

        private void UpdateAnimState()
        {
            AnimatorStateInfo stateInfo = Animator.GetCurrentAnimatorStateInfo(0);

            if(StateDic.TryGetValue(
                stateInfo.tagHash, out PlayerAnimStateBase nextState) == false)
            {
                // 검색해서 없으면 next State = defaultState처리
                nextState = defaultState;
            }

            // 상태가 변경이 되었다면
            if(currentState != nextState)
            {
                currentState?.Exit();
                // ?. : null 체크하는 키워드. null이 아닐때만 호출한다는 표현
                // =>if(currentState != null) currentState.Exit() 과 같은 표현
                currentState = nextState;
                nextState.Enter();
            }

            currentState.UpdateState(stateInfo);
        }

        private void SyncFacingVisual()
        {
            FacingDirection = Controller2D.FacingDirection;
            Animator.transform.localScale = new Vector3(FacingDirection, 1, 1);
        }

        // 아래부터는 상태 클래스들이 사용하는 공용함수들입니다
        public void HandleMovement()
        {
            Vector2 inputVector = SimpleInput.GetMoveAxisRaw();
            float absMovement = Mathf.Abs(inputVector.x);

            if (absMovement > 0.0f)
            {
                int inputFacing = (inputVector.x < 0.0f) ? -1 : 1;
                Controller2D.SetFacingDirection(inputFacing);
            }

            Controller2D.SetMoveInput(inputVector.x);

            Animator.SetFloat(MOVEMENT, Controller2D.MoveVectorX);
            Animator.SetBool(STICK_WALL, Controller2D.stickWall);
        }

        private void UpdateAttackInput()
        {
            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                Vector2 goalPosition = Cursor.position;
                Vector2 myPosition = transform.position;
                Vector2 dir = (goalPosition - myPosition);

                if (Mathf.Abs(dir.x) > 0.001f)
                {
                    int attackFacing = (dir.x < 0.0f) ? -1 : 1;
                    Controller2D.SetFacingDirection(attackFacing);
                }

                // 구르기 중이라도 공격 요청 전달
                Controller2D.RequestAttack(dir);
                Animator.SetBool(IS_ATTACK, true);

                effectAnim[0].gameObject.transform.right = dir.normalized;
                effectAnim[0].transform.localScale = new Vector3(1, FacingDirection, 1);
            }

            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right))
            {
                Animator.SetBool(IS_ATTACK, true);
            }
        }

        private void UpdateDownInput()
        {
            bool crouchHeld = SimpleInput.GetKey(Key.S);
            float horizontalRaw = SimpleInput.GetAxisHorizontalRaw();

            bool moveHeld = !Mathf.Approximately(horizontalRaw, 0.0f);

            Controller2D.RequestCrouch(crouchHeld);

            // 이동 중이면 crouch 애니메이션은 무시
            Animator.SetBool(IS_Crouch, crouchHeld && !moveHeld && !Controller2D.IsRolling);
            Animator.SetBool(IS_ROLL, Controller2D.IsRolling);
        }

        private void UpdateRollInput()
        {
            bool crouchHeld = SimpleInput.GetKey(Key.S);
            float horizontalRaw = SimpleInput.GetAxisHorizontalRaw();

            bool moveHeld = !Mathf.Approximately(horizontalRaw, 0.0f);

            // S 재입력
            bool crouchDown = crouchHeld && !prevCrouchHeld;
            // 이동 재입력
            bool moveReInput = false;

            // 0 -> 좌/우 입력 시작
            if (!prevMoveHeld && moveHeld)
            {
                moveReInput = true;
            }

            // 카타나 제로식 구르기 판정
            if (crouchHeld && moveHeld && (crouchDown || moveReInput) && Controller2D.CanStartRoll)
            {
                Controller2D.RequestRoll(Mathf.Sign(horizontalRaw));
            }

            prevCrouchHeld = crouchHeld;
            prevMoveHeld = moveHeld;
            prevHorizontalRaw = horizontalRaw;
        }

        private void UpdateJumpInput()
        {
            if (SimpleInput.GetKeyDown(Key.W))
            {
                if (Controller2D.stickWall)
                {
                    Controller2D.RequestFlip();
                    Animator.SetTrigger(IS_FLIP);
                }
                else
                {
                    Controller2D.RequestJump();
                }
            }
        }

        private void UpdateGroundedAnimation()
        {
            bool isGrounded = Controller2D.IsGrounded;

            Animator.SetBool(IS_GROUNDED, isGrounded);

            if (prevIsGrounded && !isGrounded)
            {
                if (Controller2D.VerticalVelocity > 0.0f)
                {
                    Animator.SetTrigger(JUMP);
                }
                else
                {
                    Animator.SetTrigger(DESCENDING);
                }
            }
            // 이전 프레임 상태 저장
            prevIsGrounded = isGrounded;

        }

        private void UpdateEffectAnimation()
        {
            bool isJumping = Controller2D.isJumping;
            bool isWallFlipping = Controller2D.isWallFlipping;

            // 1. 기본 점프 이펙트 (점프 시작 순간)
            bool justStartedJump = isJumping && !prevIsJumping;
            if (justStartedJump)
            {
                effectAnim[EFFECT_JUMP].transform.position = transform.position + JumpCloudOffset;
                effectAnim[EFFECT_JUMP].transform.up = Vector3.up;
                effectAnim[EFFECT_JUMP].Play("JumpCloud", 0, 0.0f);
            }

            // 2. 벽 공중제비(Flip) 이펙트
            bool justStartedWallFlip = isWallFlipping && !prevIsWallFlipping;
            if (justStartedWallFlip)
            {
                effectAnim[EFFECT_JUMP].transform.position = transform.position;

                // ApplyFlip()에서 FacingDirection이 이미 벽 반대 방향으로 설정됨
                // Y값은 고려하지 않고 수평 방향만 사용
                Vector3 flipDirection = (Controller2D.FacingDirection > 0) ? Vector3.right : Vector3.left;
                effectAnim[EFFECT_JUMP].transform.up = flipDirection;
                effectAnim[EFFECT_JUMP].Play("JumpCloud", 0, 0.0f);
            }

            // 3. 착지 이펙트 (공중 → 지면 전환 순간)
            if (Controller2D.isLanding)
            {
                effectAnim[EFFECT_LAND].transform.position = transform.position + LandCloudOffset;
                effectAnim[EFFECT_LAND].Play("LandCloud", 0, 0.0f);
            }

            prevIsJumping = isJumping;
            prevIsWallFlipping = isWallFlipping;
        }

        #region 입력 보내기
        public void ApplyGravity(bool apply)
        {
            Controller2D.useGravity = apply;
        }

        public void StopMovement()
        {
            Controller2D.SetMoveInput(0.0f);
        }

        public void ApplyRoll(bool apply)
        {
            Controller2D.IsRolling = apply;
        }

        #endregion
    }
}