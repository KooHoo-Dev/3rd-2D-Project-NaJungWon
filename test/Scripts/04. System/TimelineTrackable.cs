using UnityEngine;

namespace Study_System
{
    /// <summary>
    /// TimeLineRecorder가 추적해야 하는 오브젝트에 붙이는 보조 컴포넌트.
    ///
    /// 이 스크립트의 역할:
    /// 1. 자신이 "적"인지 "일반 동적 오브젝트"인지 구분
    /// 2. OnEnable 시점에 TimeLineRecorder에 자동 등록
    /// 3. Animator / SpriteRenderer 같은 자주 쓰는 참조 캐싱
    /// 4. 현재 애니메이션 상태, 방향 같은 공통 정보를 쉽게 꺼내도록 도와줌
    ///
    /// 보통 붙이는 대상:
    /// - 적 프리팹
    /// - 총알 프리팹
    /// - 던지는 무기 프리팹
    /// - 재생/역재생 대상 동적 오브젝트
    /// </summary>
    public class TimelineTrackable : MonoBehaviour
    {
        /// <summary>
        /// 레코더가 이 오브젝트를 어떤 그룹으로 다룰지 구분하는 타입
        /// Enemy  : 적
        /// Object : 총알, 던지는 무기, 움직이는 일반 오브젝트
        /// </summary>
        public enum Type
        {
            Enemy,
            Object,
        }

        // 인스펙터에서 어떤 타입인지 지정
        [field: SerializeField] public Type TrackType { get; private set; }

        // TimeLineRecorder가 부여하는 고유 ID
        // 같은 오브젝트를 프레임마다 일관되게 식별하기 위해 필요
        public int TrackId { get; private set; } = -1;

        // 자주 사용하는 컴포넌트는 Awake에서 캐싱해 두는 편이 좋음
        public Animator CachedAnimator { get; private set; }
        public SpriteRenderer CachedSpriteRenderer { get; private set; }

        private void Awake()
        {
            // 자식 포함 검색
            CachedAnimator = GetComponentInChildren<Animator>();
            CachedSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            // 활성화될 때마다 레코더에 등록 시도
            // 처음 생성된 총알 / 던지는 무기 같은 런타임 오브젝트가
            // 자동으로 기록 대상에 들어가게 해주는 핵심 부분
            TimeLineRecorder.RegisterTrackable(this);
        }

        /// <summary>
        /// 레코더가 최초 등록 시 고유 ID를 할당
        /// 이미 ID가 있으면 덮어쓰지 않음
        /// </summary>
        public void SetTrackId(int id)
        {
            if (TrackId >= 0) return;
            TrackId = id;
        }

        /// <summary>
        /// 현재 Animator의 상태 해시 반환
        /// 애니메이션이 없으면 0 반환
        /// </summary>
        public int GetCurrentAnimHash()
        {
            if (CachedAnimator == null) return 0;
            return CachedAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        }

        /// <summary>
        /// 현재 Animator의 normalizedTime 반환
        /// 애니메이션이 없으면 0 반환
        /// </summary>
        public float GetCurrentAnimNormalizedTime()
        {
            if (CachedAnimator == null) return 0f;
            return CachedAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        }

        /// <summary>
        /// 현재 좌우 방향 반환
        /// 
        /// 시각 표현 기준은 Animator가 붙은 Transform을 우선 사용합니다.
        /// Animator가 없으면 자기 자신의 Transform을 사용합니다.
        ///
        /// localScale.x >= 0 이면 오른쪽(1), 아니면 왼쪽(-1)로 간주
        /// </summary>
        public int GetFacing()
        {
            Transform target = CachedAnimator != null ? CachedAnimator.transform : transform;
            return target.localScale.x >= 0f ? 1 : -1;
        }

        /// <summary>
        /// 저장된 방향값(-1 or 1)을 실제 스케일에 적용
        /// 
        /// 절댓값을 먼저 취한 뒤 방향 부호만 곱해서
        /// 원래 스케일 크기는 유지하면서 좌우만 반전시키게 함
        /// </summary>
        public void SetFacing(int facing)
        {
            Transform target = CachedAnimator != null ? CachedAnimator.transform : transform;

            Vector3 scale = target.localScale;
            scale.x = Mathf.Abs(scale.x) * (facing >= 0 ? 1 : -1);
            target.localScale = scale;
        }
    }
}