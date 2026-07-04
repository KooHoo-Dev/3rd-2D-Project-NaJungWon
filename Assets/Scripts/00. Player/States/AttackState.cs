using Study.Utilities;
using UnityEngine;
namespace Study_ActionPlatformer
{
    public class AttackState : PlayerAnimStateBase
    {
        public AttackState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            // 공격중엔 이동 금지
            Owner.StopMovement();
            Owner.ApplyGravity(false);
            ScreenEffectManager.StartAfterimage();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            // 상태 초반부에는 직전 공격 입력을 제거해준다
            if(stateInfo.normalizedTime < INPUT_ATTACK_TIME)
            {
                Animator.SetBool(PlayerController.IS_ATTACK, false);
            }
        }

        public override void Exit()
        {
            Owner.ApplyGravity(true);
            ScreenEffectManager.StopAfterimage();
        }
    }
}

