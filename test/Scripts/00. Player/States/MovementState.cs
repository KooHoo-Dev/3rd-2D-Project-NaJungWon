using UnityEngine;

namespace Study_ActionPlatformer
{
    public class MovementState : PlayerAnimStateBase
    {
        public MovementState(PlayerController owner) : base(owner)
        {

        }

        public override void UpdateState(AnimatorStateInfo stateInfo)
        {
            Owner.HandleMovement();
        }
    }
}