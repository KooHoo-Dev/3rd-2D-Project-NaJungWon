using Jay;
using Study.Utilities;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.EventSystems.EventTrigger;

namespace Study_ActionPlatformer
{
    public class CombatSystem : SingletonBase<CombatSystem>
    {
        private List<ICombatObserver> observerList = new List<ICombatObserver>();

        protected override void OnInitialize()
        {
            base.OnInitialize();
        }

        // 구독을 하는 함수
        // 구독을 하는 함수

        public void Subscribe(ICombatObserver observer)
        {
            // 두번 등록하는거 방지하기 위한 예외처리(두번 등록 실수 많이함)
            if (observerList.Contains(observer)) return;
            observerList.Add(observer);
        }

        // 구독 해제를 하는 함수
        public void UnSubscribe(ICombatObserver observer)
        {
            if (observerList.Contains(observer) == false) return;
            observerList.Remove(observer);
        }

        // @ : event라고하는 키워드 회피용 접두사 입니다. 추천받아서 내가쓰는거임.
        public void To(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
        {
            // 누가, 누구에게 보내는지는 명확하게 검증을 해줘야 하기 때문에
            // 이 부분은 null 처리 해줍니다. 그리고 방어적 검증을 수행합니다.
            // 호출이 많을수도 있는, 자주 사용되는 함수이기 때문에
            // 방어적으로 코딩을 해줍니다.
            if(sender == null || receiver == null)
            {
                Debug.LogWarning($"CombatSystem ::: " +
                    $"sender : {sender != null}, receiver : {receiver != null}");
                return;
            }

            Debug.Log($"{sender.name}이 {receiver.name}에게 {@event.EventType.ToString()}" +
                $", {@event.Amount}을 전달!");

            switch (@event.EventType)
            {
                case CombatEventType.DamageEvent:
                    HandleDamageEvent(sender, receiver, @event);
                    break;
                case CombatEventType.HealEvent:
                    HandleHealEvent(sender, receiver, @event);
                    break;
            }
        }

        private void HandleDamageEvent(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
        {
            receiver.TakeDamage(@event.Amount);

            for (int i = 0; i < observerList.Count; ++i)
                observerList[i].OnDamageTaken(sender, receiver, @event);
        }

        private void HandleHealEvent(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
        {
            for (int i = 0; i < observerList.Count; ++i)
                observerList[i].OnHealTaken(sender, receiver, @event);
        }

        // Dictionary<Collider2D, CombatEntity> 사용하셔도 무방합니다.
        // : 저는 주로 Collider를 Key값으로 사용하는데 HurtBox때문에 변경한겁니다.
        private Dictionary<Collider2D, HurtBox> HurtBoxDic { get; set; } = new();

        public void AddHurtBox(Collider2D collder, HurtBox hurtBox)
        {
            HurtBoxDic.TryAdd(collder, hurtBox);
        }

        public void RemoveHurtBox(Collider2D collder)
        {
            if (HurtBoxDic.ContainsKey(collder) == false) return;
            HurtBoxDic.Remove(collder);
        }

        public HurtBox GetHurtBoxOrNull(Collider2D collder)
        {
            HurtBoxDic.TryGetValue(collder, out HurtBox reval);
            return reval;
        }
    }

}

