using UnityEngine;

namespace Study_ActionPlatformer
{
    /// <summary>
    /// 데드존 추적 + 맵 경계 클램프 + 히트스탑 쉐이크 + 리플레이 호환 통합 카메라 컨트롤러.
    ///
    /// [설계 원칙]
    /// - 이 스크립트가 붙은 GameObject의 transform.position은 항상
    ///   "흔들림이 섞이지 않은 순수한 논리적 카메라 위치"를 유지합니다.
    ///   이 값을 TimelineTrackable로 기록/재생합니다.
    /// - 실제로 화면을 그리는 Camera 컴포넌트의 transform은 이 논리적 위치에
    ///   흔들림 오프셋만 더해 매 프레임 대입받는 "출력 전용" 대상입니다.
    ///   이렇게 분리하지 않으면, 쉐이크와 팔로우 로직이 같은 transform을
    ///   서로 다른 시점(코루틴의 Update 단계 vs LateUpdate)에 덮어써서
    ///   쉐이크가 팔로우 계산에 잔차로 섞여 들어가는 문제가 생깁니다.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("추적 대상")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

        [Header("기본 설정")]
        [SerializeField] private float   followLerpSpeed = 8f; //추적 러프
        [SerializeField] private Vector2 deadZoneSize    = new Vector2(3f, 2f); //데드존 (카메라 중심 기준 박스)

        [SerializeField] private Camera cam; //실제 렌더링 카메라
        [SerializeField] private Collider2D mapBounds; // 맵 경계

        // 데드존 로직이 추적하는 "논리적 카메라 목표점" (흔들림 미포함)
        private Vector3 anchor;

        // ScreenEffectManager가 매 프레임 갱신해주는 흔들림 오프셋
        private Vector3 shakeOffset = Vector3.zero;

        private void Awake()
        {
            if (cam == null) cam = Camera.main;

            // 시작 시 즉시 타겟 위치로 스냅해서 첫 프레임에 큰 이동이 생기지 않게 합니다.
            if (target != null)
            {
                anchor = target.position + offset;
                anchor = ClampToBounds(anchor);
                transform.position = anchor;
            }
        }

        private void Start()
        {
            target = Player.LocalPlayer.transform;
        }

        private void LateUpdate()
        {
            bool isPlayback =
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Replaying ||
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Rewinding;

            if (isPlayback)
            {
                //    재생/역재생 중에는 TimeLineRecorder.SampleObject()가
                //    이번 프레임의 Update() 단계에서 이 오브젝트의 transform.position을
                //    기록된 값으로 이미 지정해 두었습니다(LateUpdate는 Update보다 항상 나중에 실행).
                //    여기서는 그 값을 그대로 렌더링 카메라에 반영만 하고, 데드존/클램프는 완전히 건너뜁니다.
                if (cam != null)
                {
                    Vector3 pos = transform.position + shakeOffset;
                    pos.z = cam.transform.position.z;
                    cam.transform.position = pos;
                }
                return;
            }

            if (target == null || cam == null) return;

            UpdateDeadZoneAnchor();

            Vector3 desired = anchor;
            desired.z = transform.position.z;

            // Time.unscaledDeltaTime 사용: 크로노스(timeScale<1)나
            // 히트스탑/일시정지(timeScale=0) 중에도 카메라 추적이 절대 멈추지 않습니다.
            transform.position = Vector3.Lerp(transform.position, desired, followLerpSpeed * Time.unscaledDeltaTime);

            Vector3 camPos = transform.position + shakeOffset;
            camPos.z = cam.transform.position.z;
            cam.transform.position = camPos;
        }

        private void UpdateDeadZoneAnchor()
        {
            float halfW = deadZoneSize.x * 0.5f;
            float halfH = deadZoneSize.y * 0.5f;

            Vector3 targetPos = target.position + offset;
            Vector2 delta = (Vector2)targetPos - (Vector2)anchor;

            if (delta.x > halfW) anchor.x += delta.x - halfW;
            else if (delta.x < -halfW) anchor.x += delta.x + halfW;

            if (delta.y > halfH) anchor.y += delta.y - halfH;
            else if (delta.y < -halfH) anchor.y += delta.y + halfH;

            // 핵심: anchor 자체를 "즉시" 클램프합니다.
            // 계산 결과만 별도 변수로 클램프하고 anchor 자신은 그대로 두면,
            // 플레이어가 맵 끝에 오래 붙어있는 동안 anchor가 맵 밖으로 한계 없이
            // 계속 누적됩니다. 그 상태에서 플레이어가 반대로 이동하면 delta가
            // 갑자기 거대해져서 카메라가 순간이동하듯 튀는 버그가 생깁니다.
            anchor = ClampToBounds(anchor);
        }

        private Vector3 ClampToBounds(Vector3 pos)
        {
            if (mapBounds == null || cam == null) return pos;

            float camHalfHeight = cam.orthographicSize;
            float camHalfWidth = camHalfHeight * cam.aspect;

            Bounds b = mapBounds.bounds; // 형태 무관, 항상 AABB

            float minX = b.min.x + camHalfWidth;
            float maxX = b.max.x - camHalfWidth;
            float minY = b.min.y + camHalfHeight;
            float maxY = b.max.y - camHalfHeight;

            // 맵이 카메라 화면보다 작은 경우(작은 방 등)는 맵 중심에 고정
            pos.x = (minX <= maxX) ? Mathf.Clamp(pos.x, minX, maxX) : b.center.x;
            pos.y = (minY <= maxY) ? Mathf.Clamp(pos.y, minY, maxY) : b.center.y;

            return pos;
        }

        // ============================================================
        // ScreenEffectManager가 호출하는 흔들림 API
        // ============================================================
        public void SetShakeOffset(Vector3 offsetValue) => shakeOffset = offsetValue;
    }
}
