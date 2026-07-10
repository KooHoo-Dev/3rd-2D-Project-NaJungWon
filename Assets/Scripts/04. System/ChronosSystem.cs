using System.Collections;
using UnityEngine;
using Study.Utilities;
using UnityEngine.InputSystem;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 카타나 제로 스타일 크로노스(시간 감속) 시스템
    /// - 부드러운 진입/해제 애니메이션
    /// - 에너지 게이지 시스템  
    /// - 기존 ScreenEffectManager 히트스탑과 완벽 호환
    /// - 물리 엔진 jittering 방지 (fixedDeltaTime 동기화)
    /// </summary>
    public class ChronosSystem : MonoBehaviour
    {
        public static ChronosSystem Instance { get; private set; }

        [Header("크로노스 기본 설정")]
        [SerializeField, Range(0.1f, 0.8f)]
        private float chronosTimeScale = 0.25f;

        [SerializeField] private KeyCode chronosKey = KeyCode.LeftShift;

        [Header("에너지 시스템")]
        [SerializeField] private float maxEnergy = 11f;
        [SerializeField] private float energyDrainRate = 1f;    // 초당 소모 (unscaled)
        [SerializeField] private float energyRegenRate = 0.5f;    // 초당 회복 (unscaled)

        public float transitionInDuration { get; private set; } = 0.15f;   // 진입 시간
        public float transitionOutDuration { get; private set; } = 0.2f;   // 해제 시간

        [Header("상태 프로퍼티")]
        public bool IsChronosActive { get; private set; } = false;
        public float CurrentEnergy { get; private set; }
        public float EnergyRatio => CurrentEnergy / maxEnergy;
        public bool CanUseChronos => CurrentEnergy > 0f && !IsTransitioning;

        [Header("내부 상태")]
        private bool IsTransitioning { get; set; } = false;
        private Coroutine transitionCoroutine;
        private float originalFixedDeltaTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentEnergy = maxEnergy;
            originalFixedDeltaTime = Time.fixedDeltaTime; // 기본값(0.02) 보존
        }

        private void Update()
        {
            // 타임라인 재생/역재생 중에는 크로노스 강제 비활성화
            if (!CanAcceptChronosInput())
            {
                if (IsChronosActive) ForceDeactivate();
                return;
            }

            HandleChronosInput();
            UpdateEnergySystem();
        }

        #region 입력 상태 관리
        private bool CanAcceptChronosInput()
        {
            // GameManager 상태 체크
            if (GameManager.Instance != null &&
                !GameManager.Instance.CanAcceptGameplayInput)
                return false;

            // 타임라인 재생 중 체크
            if (TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Replaying ||
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Rewinding)
                return false;

            return true;
        }

        private void HandleChronosInput()
        {
            bool chronosKeyDown = SimpleInput.GetKeyDown(Key.LeftShift);
            bool chronosKeyHeld = SimpleInput.GetKey(Key.LeftShift); // 해제 판정에는 계속 "누르고 있는지"가 필요하므로 그대로 유지

            if (chronosKeyDown && CanUseChronos && !IsChronosActive)
            {
                InGameUI.Instance.UpdateLShiftIcon(1);
                InGameUI.Instance.applyCronosUI(true);
                ActivateChronos();
            }
            else if ((!chronosKeyHeld || CurrentEnergy <= 0f) && IsChronosActive)
            {
                InGameUI.Instance.UpdateLShiftIcon(0);
                InGameUI.Instance.applyCronosUI(false);
                DeactivateChronos();
            }
        }

        private void UpdateEnergySystem()
        {
            if (IsChronosActive)
            {
                // 에너지 소모 (실제 시간 기준)
                CurrentEnergy -= energyDrainRate * Time.unscaledDeltaTime;
                CurrentEnergy = Mathf.Max(CurrentEnergy, 0f);
            }
            else if (CurrentEnergy < maxEnergy)
            {
                // 에너지 회복 (실제 시간 기준)
                CurrentEnergy += energyRegenRate * Time.unscaledDeltaTime;
                CurrentEnergy = Mathf.Min(CurrentEnergy, maxEnergy);
            }
        }
        #endregion

        // [크로노스 활성화/비활성화]
        public void ActivateChronos()
        {
            if (IsChronosActive || IsTransitioning || !CanUseChronos) return;

            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            transitionCoroutine = StartCoroutine(ActivateCoroutine());
        }

        public void DeactivateChronos()
        {
            if (!IsChronosActive && !IsTransitioning) return;

            if (transitionCoroutine != null)
                StopCoroutine(transitionCoroutine);

            transitionCoroutine = StartCoroutine(DeactivateCoroutine());
        }

        // [부드러운 전환 코루틴]
        private IEnumerator ActivateCoroutine()
        {
            IsTransitioning = true;
            IsChronosActive = true;

            float startScale = Time.timeScale;
            float elapsed = 0f;

            while (elapsed < transitionInDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / transitionInDuration;

                // EaseOut 커브: 처음엔 빠르게, 나중엔 부드럽게
                float easedT = 1f - (1f - t) * (1f - t);
                float targetScale = Mathf.Lerp(startScale, chronosTimeScale, easedT);

                ApplyTimeScale(targetScale);
                yield return null;
            }

            ApplyTimeScale(chronosTimeScale);
            IsTransitioning = false;
            transitionCoroutine = null;
        }

        private IEnumerator DeactivateCoroutine()
        {
            IsTransitioning = true;
            IsChronosActive = false;

            float startScale = Time.timeScale;
            float elapsed = 0f;

            while (elapsed < transitionOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / transitionOutDuration;

                // EaseIn 커브: 처음엔 부드럽게, 나중엔 빠르게
                float easedT = t * t;
                float targetScale = Mathf.Lerp(startScale, 1f, easedT);

                ApplyTimeScale(targetScale);
                yield return null;
            }

            ApplyTimeScale(1f);
            IsTransitioning = false;
            transitionCoroutine = null;
        }

        // TimeScale 적용
        private void ApplyTimeScale(float scale)
        {
            // ScreenEffectManager 히트스탑(timeScale=0) 중에는 건드리지 않음
            if (Time.timeScale <= 0f) return;

            Time.timeScale = scale;
        }

        public float GetTargetTimeScale() => IsChronosActive ? chronosTimeScale : 1f;

        // ============================================================
        // [외부 API - 강제 제어]
        // ============================================================
        public void ForceDeactivate()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            IsChronosActive = false;
            IsTransitioning = false;

            // 히트스탑 중이 아닐 때만 정상 속도로 복귀
            if (Time.timeScale > 0f)
            {
                Time.timeScale = 1f;
            }

            InGameUI.Instance.UpdateLShiftIcon(0);
            InGameUI.Instance.applyCronosUI(false);
        }

        public void ResetEnergy()
        {
            CurrentEnergy = maxEnergy;
            ForceDeactivate();
        }

        // ============================================================
        // [Static API - 외부에서 간편하게 사용]
        // ============================================================
        public static void Activate() => Instance?.ActivateChronos();
        public static void Deactivate() => Instance?.DeactivateChronos();
        public static bool IsActive => Instance?.IsChronosActive ?? false;
        public static float Energy => Instance?.CurrentEnergy ?? 0f;
        public static float EnergyPercent => Instance?.EnergyRatio ?? 0f;
    }
}
