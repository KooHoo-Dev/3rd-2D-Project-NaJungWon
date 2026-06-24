using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Study_ActionPlatformer
{
    // # HitBox?
    // - 히트박스는 게임에서 캐릭터나 사물의 위치, 충돌, 피탄, 피격 판정을
    //  계산하기 위해 설정한 "가상의 상자" 입니다.
    // - HitBox(공격 판정) <-> HurtBox(피격판정) 구조로 보통 작성되며,
    // (HurtBox는 일반 콜라이더 써도 되긴함)
    // - 상자를 밀어내거나 부수는 판정도 포함됩니다.

    public class HitBox : MonoBehaviour
    {
        [field: SerializeField] public AttackInfo AttackInfo { get; private set; }

        // 이 히트박스의 주인. 부모 계층에서 찾아 보관한다
        private CombatEntity Owner { get; set; }

        // 충돌한 HurtBox의 정보들을 담아준다.
        // 연속 공격을 방지하기 위해 HashSet을 사용합니다.
        // List를 이용해도 상관없습니다
        private HashSet<HurtBox> checkList = new HashSet<HurtBox>();

        private void Awake()
        {
            //  GetComponentInParent<타입>() : 부모 게임오브젝트들을 탐색함녀 <타입>
            // 의 컴포넌트를 찾는다. 
            Owner = GetComponentInParent<CombatEntity>();
            
            if(Owner == null)
            {
                Debug.LogError($"{name} : 부모 계층에서 CombatEntity를 찾지 못했습니다." +
                    $"오브젝트를 삭제합니다");
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            // 히트박스가 다시 켜질때 (재사용 될때 checkList를 비워준다)
            checkList.Clear();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // 아래는 Legacy Code
            // HurtBox가 없을경우 종료한다. (Physics Layer 설정으로 아예 안들어오게 좋음)
            //if (collision.TryGetComponent<HurtBox>(out HurtBox other) == false) return;
            
            HurtBox hurtBox = CombatSystem.Instance.GetHurtBoxOrNull(collision);
            if (hurtBox == null) return;
            
            // 현재 HashSet에 해당 HurtBox가 존재할 경우 종료한다.
            if (checkList.Contains(hurtBox)) return;

            // 중복 체크 방지를 위해 HashSet에 해당 HurtBox를 추가한다
            checkList.Add(hurtBox);

            // 데미지 처리하는 로직.
            // 미리 준비해놓고
            CombatEntity sender = Owner;
            CombatEntity receiver = hurtBox.Owner;
            int damage = AttackInfo.RollDamage();

            // 담아주고
            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = damage;
            @event.Position = collision.ClosestPoint(sender.transform.position);

            // 보낸다
            CombatSystem.Instance.To(sender, receiver, @event);
        }
    }
}


