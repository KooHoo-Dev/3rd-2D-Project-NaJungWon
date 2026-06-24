using Study.Utilities;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_ActionPlatformer
{
    public class AttackState : PlayerAnimStateBase
    {
        public AttackState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            // 공격중엔 이동 금지
            Owner.StopMovement();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            // 상태 초반부에는 직전 공격 입력을 제거해준다
            if(stateInfo.normalizedTime < INPUT_RESET_TIME)
            {
                Animator.SetBool(PlayerController.IS_ATTACK, false);
            }
            /*
            // 콤보 입력구간 (30% ~ 90%)
            else if(INPUT_RESET_TIME < stateInfo.normalizedTime && 
                stateInfo.normalizedTime < COMBO_INPUT_END_TIME)
            {
                // 다음 콤보 입력이 없다면 입력을 받아준다
                if(Animator.GetBool(PlayerController.IS_ATTACK) == false)
                {
                    Animator.SetBool(PlayerController.IS_ATTACK,
                        SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left));
                }
            }
            */
        }
    }
}

