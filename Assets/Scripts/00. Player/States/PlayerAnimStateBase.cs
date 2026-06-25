using UnityEngine;

namespace Study_ActionPlatformer
{
    // # PlayerAnimStateBase
    // - 플레이어 애니메이션 상태 클래스들의 공통 부모 (상태 패턴).
    // - 매 프레임 "지금 어떤 태그의 상태인가"를 관찰해서 그에 맞는 행동을 실행한다.
    // - 다른 상태패턴과는 다르게 전이(Transition)를 결정하지 않습니다(전이는 애니메이터가)
    // - 보스 상태패턴과는 다르게 순수 C# 클래스로 작성되어있습니다.
    public abstract class PlayerAnimStateBase
    {
        public const float INPUT_ATTACK_TIME = 0.3f;
        public const float INPUT_ROLL_TIME = 0.9f;
        public const float COMBO_INPUT_END_TIME = 0.6f;

        protected PlayerController Owner { get; }
        protected Animator Animator => Owner.Animator;
        
        protected PlayerAnimStateBase(PlayerController owner)
        {
            Owner = owner;
        }

        // 이 상태로 "진입한 순간" 한 번 호출된다.
        public virtual void Enter() { }
        // 이 상태에 머무는 동안 매 프레임 호출된다. (자식이 반드시 구현)
        public abstract void UpdateState(AnimatorStateInfo stateInfo);
        // 이 상태에서 "떠나는 순간" 한번 호출된다.
        public virtual void Exit() { }
    }
}