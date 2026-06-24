using Study.Utilities;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RollState : PlayerAnimStateBase
    {
        public RollState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            Owner.StopMovement();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            // 상태 초반부에는 직전 공격 입력을 제거해준다
            if (stateInfo.normalizedTime < INPUT_RESET_TIME)
            {
                Owner.StopRoll();
                //Animator.SetBool(PlayerController.IS_ROLL, false);
                
            }

            if (stateInfo.normalizedTime > COMBO_INPUT_END_TIME)
            {
                Owner.ApplyGravity();
            }
        }
    }
}

