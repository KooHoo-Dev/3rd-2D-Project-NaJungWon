using UnityEngine;

namespace Study_ActionPlatformer
{
    public abstract class BossBaseState : MonoBehaviour
    {
        protected BossController BossController { get; set; }
        public virtual void Initialize(BossController controller)
        {
            BossController = controller;
        }

        // abstract로 선언해서 자식 상태 클래스들이 반드시 구현하도록 강제한다.
        // 단, OnEnable / OnDisable 등은 유니티가 직접 호출하는 메시지 함수라서
        // 이렇게 abstract로 강제하는건 트릭에 가깝다.
        
        // 정석은 public abstract void EnterState(); 처럼
        // 직접 호출하는 메서드를 강제하는것이 맞습니다

        protected abstract void OnEnable();
        protected abstract void OnDisable();
    }

    public abstract class BossPatternState : BossBaseState
    {
        // 묶어줄수 있는 용도로 선언한 공격 상태
        // 패턴 상태객체 검색하는 용도로 씁니다.
    }
}

