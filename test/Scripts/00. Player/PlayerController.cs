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
        private const string ANIM_TAG_MOVEMENT = "Movement";
        private const string ANIM_TAG_JUMP = "Jump";

        public static readonly int MOVEMENT    = Animator.StringToHash("Movement");
        public static readonly int IS_ATTACK   = Animator.StringToHash("IsAttack");
        public static readonly int IS_Crouch   = Animator.StringToHash("IsCrouch");
        public static readonly int IS_GROUNDED = Animator.StringToHash("IsGrounded");
        public static readonly int JUMP        = Animator.StringToHash("IsJump");
        public static readonly int DESCENDING = Animator.StringToHash("Descending");



        public Animator Animator { get; private set; }
        public Animator[] effectAnim; //캐릭터 연출 애니메이터

        private SpriteRenderer SpriteRenderer { get; set; }
        private CharacterController2D Controller2D { get; set; }

        public int FacingDirection { get; private set; } = 1;

        // 각 상태를 제어하기 위한 멤버 변수들
        private Dictionary<int, PlayerAnimStateBase> StateDic { get; set; } = new();
        private PlayerAnimStateBase defaultState; // 예외처리를 위한 기본 상태
        private PlayerAnimStateBase currentState; // 상태객체를 담아서 제어하기 위한 빈 객체 변수
        
        private bool prevIsGrounded = false;

        [field: SerializeField] private Transform Cursor { get; set; }


        private void Awake()
        {
            Animator = GetComponentInChildren<Animator>();
            SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            Controller2D = GetComponent<CharacterController2D>();

            defaultState = new MovementState(this);

            StateDic.Add(Animator.StringToHash(ANIM_TAG_MOVEMENT), defaultState);
            StateDic.Add(Animator.StringToHash(ANIM_TAG_ATTACK), new AttackState(this));

        }
        private void Update()
        {
            UpdateAnimState();
            UpdateJumpInput();
            UpdateGroundedAnimation();
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

        // 아래부터는 상태 클래스들이 사용하는 공용함수들입니다
        public void HandleMovement()
        {
            Vector2 inputVector = SimpleInput.GetMoveAxisRaw();
            float absMovement = Mathf.Abs(inputVector.x);
            Animator.SetFloat(MOVEMENT, absMovement);

            if (absMovement > 0) // 이동량이 있을때
            {
                // 삼항 연산자로 정면을 설정해준다
                FacingDirection = (inputVector.x < 0) ? -1 : 1;

                // x 스케일을 반전해서 좌우를 뒤집는다.
                // (히트박스를 뒤집기 위해서 스케일 반전을 사용)
                Animator.transform.localScale = new Vector3(FacingDirection, 1, 1);
            }

            Controller2D.SetMoveInput(inputVector.x);

            if(SimpleInput.GetKeyDown(Key.S))
            {
                Animator.SetBool(IS_Crouch, true);
                Controller2D.RequestCrouch();
            }
            if(SimpleInput.GetKeyUp(Key.S))
            {
                Animator.SetBool(IS_Crouch, false);
                Controller2D.ExitCrouch();
            }
            if(SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                //바라보게할 위치
                Vector3 goalPosition = Cursor.position;
                //내위치
                Vector3 myPosition = transform.position;

                // 방향 벡터 구하기(목표 위치 - 내 위치)
                Vector3 dir = (goalPosition - myPosition);

                // 삼항 연산자로 정면을 설정해준다
                FacingDirection = (dir.x < 0) ? -1 : 1;

                // x 스케일을 반전해서 좌우를 뒤집는다.
                // (히트박스를 뒤집기 위해서 스케일 반전을 사용)
                Animator.transform.localScale = new Vector3(FacingDirection, 1, 1);

                Controller2D.RequestAttack(dir);
                Animator.SetBool(IS_ATTACK, true);
            }
            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right))
            {
                Animator.SetBool(IS_ATTACK, true);
            }
        }

        public void StopMovement()
        {
            Controller2D.SetMoveInput(0.0f);
        }

        private void UpdateJumpInput()
        {
            if (SimpleInput.GetKeyDown(Key.W))
            {
                // 점프가 가능한 상태인지는 CharacterController가 판단함.
                Controller2D.RequestJump();
            }
        }

        private void UpdateGroundedAnimation()
        {
            bool isGrounded = Controller2D.IsGrounded;
            Animator.SetBool(IS_GROUNDED, isGrounded);

            // 이전 프레임에서는 접지(바닥에 닿아있는) 상태였다가
            // 현재 프레임에서는 접지 하지 않는 상태가 되었을 때

            if (prevIsGrounded && isGrounded == false)
            {
                // VerticalVelocity가 0보다 크면 => 상승
                if (Controller2D.VerticalVelocity > 0.0f)
                    Animator.SetTrigger(JUMP);
                // 아니면 하강
                else
                    Animator.SetTrigger(DESCENDING);
            }

            prevIsGrounded = isGrounded;
        }
    }
}