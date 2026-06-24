using UnityEngine;

namespace Study_ActionPlatformer
{
    // # 이름을 CombatEventType으로 지은 이유?
    // 그냥 EventType이라고 하면 유니티 내장 타입인
    // UnityEngine.EventType(IMGUI용)과 이름이 겹쳐서 
    
    // 자동완성 짜증나서 그냥 이렇게 지었음.
    // 다른걸로 해도 무방함

    public enum CombatEventType
    {
        DamageEvent,
        HealEvent,
    }

    public struct CombatEvent
    {
        public CombatEventType EventType;
        public int Amount;
        public Vector3 Position;
    }

    // # interface?
    // - 클래스나 구조체가 반드시 제공해야 하는 멤버(메서드, 속성, 이벤트, 인덱서)의
    //  시그니처만 명시한 일종의 "계약"이자 "설계도"
    // - 무엇을 할지만 정의할 뿐, 어떻게 할지는 상속받는 녀석에게 위임합니다. 철저한
    //  추상화를 위해 사용됩니다.
    // - 추상클래스와는 달리 필드가 없어서 더 유연한 상속구조를 제공합니다.
    // - 추상클래스는 형태가 있는 것을 상속하여 수직적 구조를 갖게 되는 반면, 인터페이스는
    //  무형의 기능만 상속하기에 수평적 구조를 갖게 됩니다.
    // - C++의 다중상속의 문제점을 해결하기위해 제안된 개념입니다.

    public interface ICombatObserver
    {
        void OnDamageTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event);
        void OnHealTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event);
    }
}