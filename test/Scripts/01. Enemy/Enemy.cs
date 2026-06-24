using System.Collections;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public abstract class Enemy : CombatEntity
    {
        // HitEffectBlend 라는 public 프로퍼티를 가져온다.
        // Shader라고 하는 특별한 스크립트는 아래처럼 해당 스크립트의 프로퍼티를 가져올 수 있다.
        // 보통 "_{프로퍼티명}" 으로 설정되어있다.
        private static readonly int HIT_EFFECT_BLEND = Shader.PropertyToID("_HitEffectBlend");

        // 아래 필드들 퍼블릭 변수나 프로퍼티로 하셔도 무방합니다
        [SerializeField] private float hitEffectDuration = 0.2f;
        private const float HIT_EFFECT_VALUE = 0.5f;

        protected SpriteRenderer SpriteRenderer { get; private set; }
        protected Material instanceMaterial { get; set; }

        [field:SerializeField] public BaseStat Stat { get; private set; }
        protected EnemyController EnemyController { get; private set; }

        protected virtual void Awake()
        {
            SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            instanceMaterial = SpriteRenderer.material;
            // !주의
            // : renderer.material에 접근하는 순간 머티리얼 "복사본"이 생성됩니다.
            //  덕분에 개체마다 따로 피격효과를 줄 수 있지만, 이 복사본은 엔진이
            //  자동으로 지워주지 않으므로 OnDestory에서 직접 파괴하여 메모리 누수를
            //  막아야 합니다(6.3인 지금은 어떤지 모르겠음)
            // PS : 모든 개체가 공유하는 원본이 필요하면, sharedMaterial을 사용합니다.

            EnemyController = GetComponent<EnemyController>();
            Stat.ResetToFull();
        }

        protected virtual void Start()
        {

        }

        protected virtual void OnDestory()
        {
            if (instanceMaterial != null) Destroy(instanceMaterial);
        }

        public override void TakeDamage(int damage)
        {
            if(Stat.ApplyDamage(damage))
            {
                // 죽었다면
                EnemyController.Dead();
                return;
            }

            StartCoroutine(HitEffectCoroutine());
        }

        protected IEnumerator HitEffectCoroutine()
        {
            float waitTime = 0.0f;

            // 깜빡이는 로직
            while(true)
            {
                if (waitTime > hitEffectDuration) break;

                // 시작할때는 HIT_EFFECT_VALUE => 시간에 흐름에 따라 0.0f 까지 차감됨
                float blendValue = Mathf.Lerp(HIT_EFFECT_VALUE, 0.0f, waitTime / hitEffectDuration);
                
                // 셰이더의 프로퍼티를 변경할때는 material.Set~() 함수들을 사용합니다.
                // 런타임에 셰이더의 프로퍼티 항목의 값을 조회할때는? : materail.Get~() 함수를 사용합니다
                instanceMaterial.SetFloat(HIT_EFFECT_BLEND, blendValue);

                yield return null; // 한프레임 대기
                waitTime += Time.deltaTime; // 대기한만큼 시간 더해줌
            }

            // 깜빡이는 로직이 끝나면 초기화 해준다
            instanceMaterial.SetFloat(HIT_EFFECT_BLEND, 0.0f);

        }
    }
}

