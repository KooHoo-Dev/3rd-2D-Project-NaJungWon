using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class ScreenEffectManager : MonoBehaviour
    {
        public static ScreenEffectManager Instance { get; private set; }

        // ============================================================
        // [인스펙터 설정]
        // ============================================================

        [Header("슬래시 이펙트 (오브젝트 풀링)")]
        [SerializeField] private GameObject slashPrefab;          // 슬래시 이미지 오브젝트
        [SerializeField] private int slashPoolSize = 8;          // 풀 크기
        [SerializeField] private float slashSpeed = 100f;         // 화면 가로지르는 속도
        [SerializeField] private float screenMargin = 2f;        // 화면 밖 여유 거리

        [Header("카메라 설정")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CameraController cameraController;
        [SerializeField] private float defaultShakeDuration = 0.15f;
        [SerializeField] private float defaultShakeMagnitude = 0.18f;

        [Header("히트스탑 설정")]
        [SerializeField] private float defaultHitStopDuration = 0.1f;

        [Header("잔상 이펙트")]
        [SerializeField] private SpriteRenderer afterimagePrefab;
        [SerializeField] private int afterimagePoolSize = 20;
        [SerializeField] private float afterimageInterval = 0.03f;
        [SerializeField] private float afterimageFadeDuration = 0.2f;
        [SerializeField] private Color afterimageColor = new Color(0.5f, 0.8f, 1f, 0.7f);

        // ============================================================
        // [내부 상태 관리]
        // ============================================================

        private Vector3 cameraOriginPos;
        private Coroutine shakeCoroutine;
        private Coroutine hitStopCoroutine;
        private Coroutine afterimageCoroutine;

        // 크로노스 시스템 호환성을 위한 TimeScale 스택
        private readonly Stack<float> timeScaleStack = new Stack<float>();

        // 오브젝트 풀
        private readonly Queue<GameObject> slashPool = new Queue<GameObject>();
        private readonly Queue<SpriteRenderer> afterimagePool = new Queue<SpriteRenderer>();

        // ============================================================
        // [초기화]
        // ============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            targetCamera = Camera.main;
            cameraController = targetCamera.GetComponent<CameraController>();

            InitializePools();
        }

        private void InitializePools()
        {
            // 슬래시 풀 초기화
            if (slashPrefab != null)
            {
                Transform slashParent = new GameObject("SlashPool").transform;
                slashParent.SetParent(transform);

                for (int i = 0; i < slashPoolSize; i++)
                {
                    GameObject slash = Instantiate(slashPrefab, slashParent);
                    slash.SetActive(false);
                    slashPool.Enqueue(slash);
                }
            }

            // 잔상 풀 초기화
            if (afterimagePrefab != null)
            {
                Transform afterimageParent = new GameObject("AfterimagePool").transform;
                afterimageParent.SetParent(transform);

                for (int i = 0; i < afterimagePoolSize; i++)
                {
                    SpriteRenderer sr = Instantiate(afterimagePrefab, afterimageParent);
                    sr.gameObject.SetActive(false);
                    afterimagePool.Enqueue(sr);
                }
            }
        }

        // ============================================================
        // [Static API - 외부 호출용]
        // ============================================================

        /// <summary>
        /// 화면을 가로지르는 슬래시 연출
        /// </summary>
        /// <param name="targetWorldPos">슬래시가 통과할 월드 좌표 (피격 대상 위치)</param>
        /// <param name="direction">슬래시 진행 방향 벡터</param>
        public static void PlaySlashEffect(Vector3 targetWorldPos, Vector2 direction)
        {
            if (Instance == null) return;
            if (Instance.IsPlaybackMode()) return;
            Instance.PlaySlashInternal(targetWorldPos, direction);
        }

        /// <summary>
        /// 히트스탑 연출 (크로노스 시스템 완벽 호환)
        /// </summary>
        public static void HitStop(float duration = -1f)
        {
            if (Instance == null) return;
            Instance.PlayHitStopInternal(
                duration < 0 ? Instance.defaultHitStopDuration : duration);
        }

        /// <summary>
        /// 카메라 쉐이크 연출
        /// </summary>
        public static void CameraShake(float duration = -1f, float magnitude = -1f)
        {
            if (Instance == null) return;
            Instance.PlayCameraShakeInternal(
                duration < 0 ? Instance.defaultShakeDuration : duration,
                magnitude < 0 ? Instance.defaultShakeMagnitude : magnitude);
        }

        /// <summary>
        /// 잔상 효과 시작
        /// </summary>
        public static void StartAfterimage()
        {
            if (Instance == null) return;
            Instance.StartAfterimageInternal();
        }

        /// <summary>
        /// 잔상 효과 중단
        /// </summary>
        public static void StopAfterimage()
        {
            if (Instance == null) return;
            Instance.StopAfterimageInternal();
        }

        /// <summary>
        /// 공격 히트 통합 연출 (타임라인 재생 중 자동 차단)
        /// </summary>
        public static void PlayAttackHitEffect(Vector3 targetWorldPos, Vector2 hitDirection)
        {
            if (Instance == null) return;
            if (Instance.IsPlaybackMode()) return;

            PlaySlashEffect(targetWorldPos, hitDirection);
            HitStop();
            CameraShake();
        }

        // ============================================================
        // [슬래시 이펙트 핵심 구현]
        // ============================================================

        private void PlaySlashInternal(Vector3 targetWorldPos, Vector2 direction)
        {
            if (slashPool.Count == 0 || targetCamera == null) return;

            // 방향 정규화
            Vector2 dir = direction.normalized;
            if (dir == Vector2.zero) dir = Vector2.right;

            // 화면 경계 계산 및 시작/끝 지점 결정
            Vector2 startPos, endPos;
            CalculateSlashPath(targetWorldPos, dir, out startPos, out endPos);

            // 풀에서 슬래시 오브젝트 가져오기
            GameObject slash = slashPool.Dequeue();
            slash.SetActive(true);

            // 방향에 맞게 회전 (Vector2.right 기준 스프라이트 가정)
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            slash.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            StartCoroutine(MoveSlashCoroutine(slash, startPos, endPos));
        }

        /// <summary>
        /// 타겟을 관통하는 화면 진입/퇴장 지점 계산
        /// 
        /// 핵심 원리:
        /// 1. 카메라 뷰포트 크기를 월드 단위로 계산
        /// 2. 화면 대각선 길이 + 여유분을 이동 거리로 사용
        /// 3. 타겟 기준으로 방향 반대편에서 시작해 방향쪽으로 끝남
        /// </summary>
        private void CalculateSlashPath(Vector3 targetWorldPos, Vector2 dir, out Vector2 startPos, out Vector2 endPos)
        {
            // 카메라 뷰포트 크기 (월드 단위)
            float camHeight = targetCamera.orthographicSize * 2f;
            float camWidth = camHeight * targetCamera.aspect;

            // 화면 대각선 길이 = 어떤 방향이든 화면을 완전히 벗어나기 충분한 거리
            float diagonal = Mathf.Sqrt(camWidth * camWidth + camHeight * camHeight);
            float travelDistance = diagonal * 0.5f + screenMargin;

            // 타겟 중심으로 양방향 확장
            startPos = (Vector2)targetWorldPos - dir * travelDistance;
            endPos = (Vector2)targetWorldPos + dir * travelDistance;
        }

        private IEnumerator MoveSlashCoroutine(GameObject slash, Vector2 startPos, Vector2 endPos)
        {
            slash.transform.position = startPos;

            float totalDistance = Vector2.Distance(startPos, endPos);
            float duration = totalDistance / slashSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // 히트스탑 중에도 슬래시는 계속 이동
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                slash.transform.position = Vector2.Lerp(startPos, endPos, t);
                yield return null;
            }

            // 이동 완료 후 풀에 반환
            slash.SetActive(false);
            slashPool.Enqueue(slash);
        }

        // ============================================================
        // [히트스탑 구현 - 크로노스 완벽 호환]
        // ============================================================

        private void PlayHitStopInternal(float duration)
        {
            if (hitStopCoroutine != null)
            {
                StopCoroutine(hitStopCoroutine);
                // 기존 히트스탑 중단 시 스택에서 복구
                if (timeScaleStack.Count > 0)
                    Time.timeScale = timeScaleStack.Pop();
            }

            hitStopCoroutine = StartCoroutine(HitStopCoroutine(duration));
        }

        private IEnumerator HitStopCoroutine(float duration)
        {
            timeScaleStack.Push(Time.timeScale);
            Time.timeScale = 0f;

            yield return new WaitForSecondsRealtime(duration);

            if (timeScaleStack.Count > 0) timeScaleStack.Pop(); // 예전 값은 버립니다.

            float restoreScale = (ChronosSystem.Instance != null)
                ? ChronosSystem.Instance.GetTargetTimeScale()
                : 1f;

            Time.timeScale = restoreScale;
            // fixedDeltaTime은 더 이상 어디서도 건드리지 않으므로 별도 복구가 필요 없습니다.

            hitStopCoroutine = null;
        }

        // ============================================================
        // [카메라 쉐이크 구현]
        // ============================================================

        private void PlayCameraShakeInternal(float duration, float magnitude)
        {
            if (targetCamera == null) return;

            if (shakeCoroutine != null)
                StopCoroutine(shakeCoroutine);

            shakeCoroutine = StartCoroutine(CameraShakeCoroutine(duration, magnitude));
        }

        private IEnumerator CameraShakeCoroutine(float duration, float magnitude)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float progress = elapsed / duration;
                float currentMagnitude = Mathf.Lerp(magnitude, 0f, progress);

                Vector2 randomOffset = Random.insideUnitCircle * currentMagnitude;

                // ✅ 절대 위치(origin + offset)로 덮어쓰지 않고,
                // "현재 위치에 더해질 오프셋 값"만 전달합니다.
                cameraController.SetShakeOffset(new Vector3(randomOffset.x, randomOffset.y, 0f));


                // 히트스탑 중에도 쉐이크 진행
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            cameraController.SetShakeOffset(Vector3.zero);
            shakeCoroutine = null;
        }

        // ============================================================
        // [잔상 이펙트 구현]
        // ============================================================

        private void StartAfterimageInternal()
        {
            if (afterimageCoroutine != null)
                StopCoroutine(afterimageCoroutine);

            afterimageCoroutine = StartCoroutine(PlayerAfterimageCoroutine());
        }

        private void StopAfterimageInternal()
        {
            if (afterimageCoroutine != null)
            {
                StopCoroutine(afterimageCoroutine);
                afterimageCoroutine = null;
            }
        }

        /// <summary>
        /// 핵심: Player.LocalPlayer에서 실시간으로 애니메이션 스프라이트를 캡처하는 코루틴
        /// 매 interval마다 현재 애니메이션 프레임을 새로 읽어서 잔상 생성
        /// </summary>
        private IEnumerator PlayerAfterimageCoroutine()
        {
            while (true)
            {
                // 2. 실제 애니메이션이 적용되는 SpriteRenderer 실시간 획득
                SpriteRenderer playerRenderer = Player.LocalPlayer.transform.GetChild(0).GetComponent<SpriteRenderer>();

                // 3. 현재 프레임의 스프라이트가 유효한지 확인
                if (playerRenderer.sprite != null && playerRenderer.gameObject.activeInHierarchy)
                {
                    SpawnRealtimeAfterimage(playerRenderer);
                }

                // 4. UnscaledTime으로 크로노스/히트스탑과 독립적으로 대기
                yield return new WaitForSecondsRealtime(afterimageInterval);
            }
        }

        /// <summary>
        /// 실시간 애니메이션 프레임을 캡처하여 잔상 생성
        /// </summary>
        private void SpawnRealtimeAfterimage(SpriteRenderer targetRenderer)
        {
            if (afterimagePool.Count == 0)
            {
                Debug.LogWarning("[ScreenEffect] 잔상 풀 고갈 - afterimagePoolSize 증가 권장");
                return;
            }

            // 현재 애니메이션 프레임의 스프라이트 실시간 캡처
            Sprite currentAnimationFrame = targetRenderer.sprite;

            SpriteRenderer ghost = afterimagePool.Dequeue();
            ghost.gameObject.SetActive(true);

            //  이 순간의 애니메이션 프레임을 잔상에 적용
            ghost.sprite = currentAnimationFrame;

            // 플레이어의 현재 상태를 완전히 복사
            ghost.transform.position = targetRenderer.transform.position;
            ghost.transform.rotation = targetRenderer.transform.rotation;
            ghost.transform.localScale = targetRenderer.transform.lossyScale;

            //  좌우 반전 상태 동기화
            ghost.flipX = targetRenderer.flipX;
            ghost.flipY = targetRenderer.flipY;

            // 런타임에 플레이어와 동일한 Sorting Layer 자동 적용
            ghost.sortingLayerID = targetRenderer.sortingLayerID;
            ghost.sortingOrder = targetRenderer.sortingOrder - 1; // 플레이어 바로 뒤

            // 잔상 색상 적용
            ghost.color = afterimageColor;

            StartCoroutine(FadeOutAfterimage(ghost));
        }

        private IEnumerator FadeOutAfterimage(SpriteRenderer ghost)
        {
            Color startColor = ghost.color;
            float elapsed = 0f;

            while (elapsed < afterimageFadeDuration)
            {
                if (!ghost.gameObject.activeSelf) yield break;

                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / afterimageFadeDuration);
                ghost.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                // UnscaledTime으로 슬로우모션 중에도 자연스럽게 페이드
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            ghost.gameObject.SetActive(false);
            afterimagePool.Enqueue(ghost);
        }

        // ============================================================
        // [유틸리티]
        // ============================================================

        private bool IsPlaybackMode()
        {
            return TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Replaying ||
                   TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Rewinding;
        }
    }
}
