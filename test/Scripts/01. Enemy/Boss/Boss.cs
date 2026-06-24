using UnityEngine;

namespace Study_ActionPlatformer
{
    public class Boss : Enemy
    {
        // 보스는
        // 콜라이더, 스프라이트 렌더러 각각 3개씩 가지고 있음
        // BossParts를 만들어서 각 개체를 분리해놓은 구조에서
        // 전투시스템이 작동하게끔 구현

        private BossController BossController { get; set; }
        [SerializeField] private float PartsDamageMultiplier = 0.8f;

        protected override void Awake()
        {
            base.Awake();
            BossController = GetComponent<BossController>();
        }

        public override void TakeHeal(int heal)
        {
            
        }

        // 부위 공격 데미지에 배율을 적용하여 최종 데미지를 계산하는 함수
        public override int CalculateFinalDamage(int damage)
        {
            return Mathf.RoundToInt(damage * PartsDamageMultiplier);
        }

        // 함수 오버로딩
        // 매개변수가 다른 함수들을 같은 함수명으로 정의하는 것
        // 실제 체력 차감은 아래 ApplyDamage에 위임한다.
        // (피격 연출은 맞은 부위(BossParts)가 스스로 재생하므로 여기서는 처리하지 않는다)
        public void TakeDamage(BossParts parts, int damage)
        {
            ApplyDamage(damage);
        }

        public override void TakeDamage(int damage)
        {
            ApplyDamage(damage);
            StartCoroutine(HitEffectCoroutine());
        }

        private void ApplyDamage(int damage)
        {
            if (Stat.ApplyDamage(damage)) BossController.Dead();
        }
    }

}

