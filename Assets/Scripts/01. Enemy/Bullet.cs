using Study.Utilities;
using System.Collections.Generic;
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

        [SerializeField] private float lifeTime = 5f; // 아무것도 안 맞고 계속 날아갈 경우를 대비한 안전장치
        private float aliveTimer;

        // Update is called once per frame
        private void Update()
        {
            //  재생 / 역재생중 코드 정지
            if (TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Replaying ||
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Rewinding)
                return;

            aliveTimer += Time.deltaTime;
            if (aliveTimer >= lifeTime)
            {
                gameObject.SetActive(false);
                UnitManagement.Instance.EnemyBullets.Enqueue(this); // 아무것도 못 맞히고 시간 초과되면 회수
                return;
            }

            transform.Translate(fireDirection * Speed * Time.deltaTime, Space.World);
        }

        private void OnEnable()
        {
            checkList.Clear();
            aliveTimer = 0f;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Replaying ||
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Rewinding)
                return;

            //팀킬방지 코드
            if (Owner.gameObject == Player.LocalPlayer.gameObject &&
                other.gameObject == Player.LocalPlayer.gameObject) return;
            else if (Owner.gameObject != Player.LocalPlayer.gameObject &&
                other.gameObject != Player.LocalPlayer.gameObject) return;

            // 플레이어가 구르는동안 안맞도록 처리
            if (Owner != Player.LocalPlayer && other.gameObject == Player.LocalPlayer.gameObject)
            {
                CharacterController2D char2D = Player.LocalPlayer.GetComponent<CharacterController2D>();
                PlayerController playerController = Player.LocalPlayer.GetComponent<PlayerController>();
                bool isInvincible = playerController.isDead || char2D.IsRolling;
                if (isInvincible) return;
            }

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

            ScreenEffectManager.PlayAttackHitEffect(
                    receiver.transform.position,    // 슬래시가 관통할 타겟 위치
                    hitDir                          // 슬래시 진행 방향
                );

            gameObject.SetActive(false);
            UnitManagement.Instance.EnemyBullets.Enqueue(this);
        }

        public void Set(Vector2 direction , CombatEntity sender)
        {
            Owner = sender;
            transform.right = direction;
            fireDirection = direction;
        }

        public void ReflectBullet(CombatEntity sender)
        {
            ScreenEffectManager.HitStop();
            ScreenEffectManager.CameraShake();
            Owner = sender;
            fireDirection = fireDirection * new Vector2(-1, -1);
        }
    }

}


