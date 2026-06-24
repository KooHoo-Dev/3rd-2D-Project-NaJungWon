using System;
using System.Collections.Generic;
using UnityEngine;


namespace Study_ActionPlatformer
{
    public class BossController : MonoBehaviour
    {
        // 상태 관리하는것부터 만들어 봅시다
        // 이번에는 MonoBehaviour를 상속받은 FSM을 만들어 볼겁니다.
        // 게임오브젝트 + 상태 클래스 구조를 갖고 있습니다
        private Dictionary<Type, BossBaseState> StateDic { get; set; } = new();
        private BossBaseState CurrentState { get; set; }
        private SpriteRenderer[] allSpriteRenderer;

        private void Awake()
        {
            // 비활성화된 SpriteRenderer도 검색해서 넣어줍니다
            allSpriteRenderer = GetComponentsInChildren<SpriteRenderer>(true);

            // Boss GameObject 하위에 적용된 모든 상태 클래스들을 찾아 줍니다.
            BossBaseState[] allStates = GetComponentsInChildren<BossBaseState>(true);

            for(int i = 0; i < allStates.Length; ++i)
            {
                BossBaseState state = allStates[i];
                StateDic.Add(state.GetType(), state); // state에 정의된 타입을 가져와서 저장해 줍니다
                // .GetType()
                // : 현재 객체의 타입을 반환합니다. 현재 인스턴스의 정확한 런타임 타입을 반환합니다.
                //  BossBaseState를 상속받응 Idle이 있다면, Idle 타입을 반환할 수 있게끔
                //  구성되어있습니다.
                state.Initialize(this);
                state.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            ChangeState(typeof(IdleState));
        }

        public void ChangeState(Type stateType)
        {
            if(StateDic.TryGetValue(stateType, out BossBaseState nextState) == false)
            {
                Debug.LogError($"등록되지 않은 상태입니다 : {stateType.Name}");
                // stateType.Name = 해당 타입(클래스) 이름입니다.
                return;
            }

            if (CurrentState != null) CurrentState.gameObject.SetActive(false);
            CurrentState = nextState;
            CurrentState.gameObject.SetActive(true);
        }

        public void ChangeAlpha(float alphaValue)
        {
            for(int i = 0; i < allSpriteRenderer.Length; ++i)
            {
                // Color는 struct(value) 타입이기때문에
                // Vector처럼 모든 값을 복사 한후 수정된 값을 다시 대입해줘야 합니다
                Color color = allSpriteRenderer[i].color;
                color.a = alphaValue;
                allSpriteRenderer[i].color = color;
            }
        }

        // Boss에서 호출하는 함수
        public void Dead()
        {
            ChangeState(typeof(DeadState));
        }
    }
}

