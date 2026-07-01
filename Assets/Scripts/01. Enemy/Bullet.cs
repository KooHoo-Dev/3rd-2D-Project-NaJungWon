using System.Collections.Generic;
using Unity.Android.Types;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Bullet : MonoBehaviour
    {
        //히트박스 주인
        [field: SerializeField] public CombatEntity Owner { get; private set; }
        [field: SerializeField] public AttackInfo AttackInfo { get; private set; }
        [field: SerializeField] public float Speed { get; private set; }

        private Vector2 fireDirection = Vector2.zero;
        private HashSet<HurtBox> checkList = new HashSet<HurtBox>();


        // Update is called once per frame
        private void Update()
        {
            transform.Translate(fireDirection * Speed * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Owner.gameObject == Player.LocalPlayer.gameObject &&
                other.gameObject == Player.LocalPlayer.gameObject) return;
            else if (Owner.gameObject != Player.LocalPlayer.gameObject &&
                other.gameObject != Player.LocalPlayer.gameObject) return;

            //gameObject.SetActive(false);
            

            HurtBox hurtBox = CombatSystem.Instance.GetHurtBoxOrNull(other);
            if (hurtBox == null) return;

            // 현재 HashSet에 해당 HurtBox가 존재할 경우 종료한다.
            if (checkList.Contains(hurtBox)) return;

            // 중복 체크 방지를 위해 HashSet에 해당 HurtBox를 추가한다
            checkList.Add(hurtBox);

            // 데미지 처리하는 로직.
            // 미리 준비해놓고
            CombatEntity sender = Owner;
            CombatEntity receiver = hurtBox.Owner;
            int damage = AttackInfo.Damage;

            Vector2 hitDir = (receiver.transform.position - sender.transform.position).normalized;

            // 담아주고
            CombatEvent @event;
            @event.EventType = CombatEventType.DamageEvent;
            @event.Amount = damage;
            @event.Position = other.ClosestPoint(sender.transform.position);
            @event.HitDirection = hitDir;
            @event.KnockbackPower = AttackInfo.KnockbackPower;

            // 보낸다
            CombatSystem.Instance.To(sender, receiver, @event);
        }

        public void Set(Vector2 direction , CombatEntity sender)
        {
            Owner = sender;
            transform.right = direction;
            fireDirection = direction;
        }

        public void ReflectBullet(CombatEntity sender)
        {
            Owner = sender;
            fireDirection = fireDirection * new Vector2(-1, -1);
        }
    }

}


