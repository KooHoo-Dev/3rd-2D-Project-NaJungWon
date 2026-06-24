using Study.Utilities;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class CrouchState : PlayerAnimStateBase
    {
        public CrouchState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            
        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            Owner.HandleMovement();
        }
    }
}

