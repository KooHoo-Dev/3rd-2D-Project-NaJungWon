using Study.Utilities;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class RollState : PlayerAnimStateBase
    {

        public RollState(PlayerController owner) : base(owner) 
        {

        }

        public override void Enter()
        {
            Owner.StopMovement();
            ScreenEffectManager.StartAfterimage();
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            
        }

        public override void Exit()
        {
            ScreenEffectManager.StopAfterimage();
        }
    }
}

