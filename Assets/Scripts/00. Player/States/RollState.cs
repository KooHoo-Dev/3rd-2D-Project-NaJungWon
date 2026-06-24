using Study.Utilities;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RollState : PlayerAnimStateBase
    {
        public RollState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            // 공격중엔 이동 금지
            Owner.StopMovement();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            if (stateInfo.normalizedTime < INPUT_RESET_TIME)
            {
                Animator.SetBool(PlayerController.IS_ATTACK, false);
            }
        }
    }
}

