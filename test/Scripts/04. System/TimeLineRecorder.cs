using Study.Utilities;
using Study_ActionPlatformer;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_System
{
    /// <summary>
    /// RewindableObject가 OnEnable 시 static 리스트에 등록되면,
    /// FixedUpdate마다 프레임별 변화량을 기록하고 재생·되감기를 수행합니다.
    /// </summary>
    public class TimeLineRecorder : MonoBehaviour
    {
        [System.Serializable]
        public class TimelineFrame
        {
            public int tick; //기록 시간
            public PlayerRecord player;
            public List<EnemyRecord> enemies = new(); // 해당 틱의 적 기록들
            public List<ObjectRecord> objects = new(); // 해당 틱의 총알/무기 등 기록들
        }

        #region 기록 구조체
        // ------------------------------------------------------------
        // [플레이어 기록]
        // ------------------------------------------------------------
        [System.Serializable]
        public struct PlayerRecord //플레이어 기록 구조체
        {
            public bool isActive;       // 플레이어 활성화 여부

            // ----- 입력 -----
            public int moveX;           // -1, 0, 1
            public bool jumpPressed;        // 점프 입력
            public bool dashPressed;        // 대시 입력
            public bool attackPressed;      // 공격 입력

            public Vector2 aimDirection; // 마우스 벡터(에임 방향)

            // ----- 상태 -----
            public Vector3 position;        // 정확한 재생/역재생용
            public int facing;              // 바라보는 방향 (-1, 1)

            // ----- 애니메이션 -----
            public int animStateHash;       // 현재 애니메이션 상태
            public float animNormalizedTime;// 현재 애니메이션 진행도
        }

        // ------------------------------------------------------------
        // [적 기록]
        // ------------------------------------------------------------
        [System.Serializable]
        public struct EnemyRecord
        {
            public int id;              // 고유 ID
            public bool isActive;       // 현재 활성화 상태
            public Vector3 position;        // 적 위치
            public int facing;              // 정면 방향

            public int animStateHash;       // 적 애니메이션 상태
            public float animNormalizedTime;// 적 애니메이션 진행도
        }

        // ------------------------------------------------------------
        // [동적 오브젝트 기록] // 총알, 던지는 무기, 움직이는 소품 등
        // ------------------------------------------------------------
        [System.Serializable]
        public struct ObjectRecord
        {
            public int id; // 인덱스 번호
            public bool isActive;           // 활성/비활성 상태

            public Vector3 position;        // 오브젝트 위치
            public float rotationZ;         // 회전값
            public int flipX; // -1, 1로 받음

            // 애니메이션이 있는 오브젝트만 사용
            public int animStateHash;
            public float animNormalizedTime;
        }

        // ------------------------------------------------------------
        // [애니메이션 기록 구조체]
        // ------------------------------------------------------------
        [System.Serializable]
        public struct AnimRecord
        {
            public int prevStateHash; // 직전 틱의 애니메이션 상태 해시
            public int currentStateHash; // 현재 틱의 애니메이션 상태 해시
            public float normalizedTime; // 현재 애니메이션 진행도 (0~1 이상 가능)

            public bool started;   // 이전 틱과 달라져서 새 애니메이션 시작
            public bool ended;     // 이전 애니메이션 종료
        }
        #endregion

        public enum RecordMode
        {
            Idle,       // 아무 작업 안 함
            Recording,  // 기록 중
            Pause,      // 기록 일시정지
            Fin,        // 종료 상태
            Rewinding,  // 역재생 중
            Replaying,  // 정방향 재생 중
        }

        // 싱글톤처럼 하나만 존재하도록 하기 위한 static 참조
        private static TimeLineRecorder recorder;
        public static RecordMode recordMode;

        [Header("기본 대상")]
        [SerializeField] private GameObject playerObject;
        // 처음부터 씬에 존재하는 기록 대상들 예: 플레이어, 적, 특수 오브젝트
        [SerializeField] private List<GameObject> needRecordObject = new();

        [Header("재생/역재생 중 꺼둘 스크립트")]
        // 재생 중에 기존 이동/AI/입력 스크립트가 같이 돌면
        // 레코더가 위치를 덮어쓰는 것과 충돌하므로 비활성화할 목록
        [SerializeField] private List<Behaviour> disableWhilePlayback = new();

        [Header("기록 설정")]
        // 저장 가능한 최대 틱 수 // fixedDeltaTime=0.02 이면 50틱 = 1초
        [SerializeField] private int maxRecordedTicks = 15000; // 약 5분

        // 전체 타임라인 기록 데이터
        public List<TimelineFrame> frames = new();

        public Dictionary<int, GameObject> objectId = new(); // 인덱스에 대입된 게임오브젝트

        // 현재 몇 번째 프레임을 가리키는지
        public static int CurrentFrame { get; private set; }

        // ID -> Trackable 컴포넌트
        private readonly Dictionary<int, TimelineTrackable> trackedById = new();
        // GameObject -> ID
        private readonly Dictionary<GameObject, int> idByObject = new();

        // 직전 틱의 값들을 저장해두는 캐시들
        // velocity 계산, active 상태 변화 감지, 애니메이션 시작/종료 감지에 사용
        private readonly Dictionary<int, Vector3> prevTrackedPos = new();
        private readonly Dictionary<int, bool> prevTrackedActive = new();
        private readonly Dictionary<int, int> prevTrackedAnimHash = new();

        // 새로 등록되는 오브젝트에 줄 ID
        private int nextTrackId = 1;

        // 실제 기록용 현재 틱
        private int currentTick = 0;

        // 재생 / 역재생 시 읽어올 틱 인덱스
        private int playbackTick = 0;

        // ------------------------------------------------------------
        // [플레이어 직전 상태 캐시]
        // ------------------------------------------------------------
        private Vector3 prevPlayerPos;
        private int prevPlayerAnimHash;
        private int prevMoveX;
        private bool prevJumpHeld;
        private bool prevDashHeld;
        private bool prevAttackHeld;

        private Animator playerAnimator;
        private PlayerController playerController;

        private void Awake()
        {
            if (recorder != null && recorder != this)
            {
                Destroy(gameObject);
                return;
            }

            recorder = this;
        }


        private void Start()
        {
            // 플레이어 캐시 확보
            CachePlayer();
            // 씬 시작 시점에 이미 존재하는 기본 오브젝트 등록
            RegisterInitialObjects();
            // 비교 기준이 되는 직전 상태 캐시 초기화
            ResetPreviousCaches();
        }

        private void Update()
        {
            // R : 기록 시작
            if (SimpleInput.GetKeyDown(Key.R))
            {
                BeginRecording();
            }
            // ESC : 기록 일시정지/재개
            if (SimpleInput.GetKeyDown(Key.Escape))
            {
                if (recordMode == RecordMode.Recording) recordMode = RecordMode.Pause;
                else if (recordMode == RecordMode.Pause) recordMode = RecordMode.Recording;
            }
            // T : 정방향 재생
            if (SimpleInput.GetKeyDown(Key.T))
            {
                BeginReplay();
            }
            // Y : 역재생
            if (SimpleInput.GetKeyDown(Key.Y))
            {
                BeginRewind();
            }
        }

        private void FixedUpdate()
        {
            switch (recordMode)
            {
                case RecordMode.Recording:
                    RecordStage();
                    break;

                case RecordMode.Replaying:
                    ReplayStage();
                    break;

                case RecordMode.Rewinding:
                    RewindStage();
                    break;
            }
        }

        // ------------------------------------------------------------
        // [Trackable 자동 등록용 static 진입점]
        // ------------------------------------------------------------
        // TimelineTrackable.OnEnable()에서 이 함수를 호출해
        // 레코더에 자신을 등록합니다.
        public static void RegisterTrackable(TimelineTrackable trackable)
        {
            if (recorder == null || trackable == null) return;
            recorder.RegisterInternal(trackable);
        }

        // ------------------------------------------------------------
        // [처음부터 씬에 있던 오브젝트 등록]
        // ------------------------------------------------------------
        private void RegisterInitialObjects()
        {
            foreach (GameObject go in needRecordObject)
            {
                if (go == null) continue;

                TimelineTrackable trackable = go.GetComponent<TimelineTrackable>();
                if (trackable == null) continue;

                RegisterInternal(trackable);
            }
        }

        // ------------------------------------------------------------
        // [실제 등록 처리]
        // ------------------------------------------------------------
        // 새로운 오브젝트가 들어오면:
        // 1) 고유 ID 부여
        // 2) 사전들에 등록
        // 3) 직전 상태 캐시 초기화
        private void RegisterInternal(TimelineTrackable trackable)
        {
            if (trackable == null) return;

            // 이미 등록된 오브젝트면 중복 등록 방지
            if (idByObject.ContainsKey(trackable.gameObject)) return;

            int id = nextTrackId++;
            trackable.SetTrackId(id);

            trackedById[id] = trackable;
            idByObject[trackable.gameObject] = id;

            // needRecordObject에도 자동 추가
            if (!needRecordObject.Contains(trackable.gameObject))
                needRecordObject.Add(trackable.gameObject);

            // 직전 상태 캐시도 같이 초기화
            prevTrackedPos[id] = trackable.transform.position;
            prevTrackedActive[id] = trackable.gameObject.activeSelf;
            prevTrackedAnimHash[id] = trackable.GetCurrentAnimHash();
        }

        // ------------------------------------------------------------
        // [플레이어 캐시 확보]
        // ------------------------------------------------------------
        private void CachePlayer()
        {
            if (playerObject == null) return;

            playerController = playerObject.GetComponent<PlayerController>();
            playerAnimator = playerObject.GetComponentInChildren<Animator>();

            prevPlayerPos = playerObject.transform.position;
            prevPlayerAnimHash = (playerAnimator != null)
                ? playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash
                : 0;

            disableWhilePlayback.Add(playerController);
        }

        // ------------------------------------------------------------
        // [직전 상태 캐시 초기화]
        // ------------------------------------------------------------
        // 기록 시작 전에 "이전 값"을 잡아두어야
        // 첫 틱부터 속도/엣지 계산이 가능합니다.
        private void ResetPreviousCaches()
        {
            disableWhilePlayback.Clear();
            CachePlayer();

            prevMoveX = 0;
            prevJumpHeld = false;
            prevDashHeld = false;
            prevAttackHeld = false;

            foreach (var pair in trackedById)
            {
                TimelineTrackable tr = pair.Value;
                if (tr == null) continue;

                prevTrackedPos[pair.Key] = tr.transform.position;
                prevTrackedActive[pair.Key] = tr.gameObject.activeSelf;
                prevTrackedAnimHash[pair.Key] = tr.GetCurrentAnimHash();
            }
        }

        // ------------------------------------------------------------
        // [기록 시작]
        // ------------------------------------------------------------
        private void BeginRecording()
        {
            frames.Clear();
            disableWhilePlayback.Clear();

            currentTick = 0;
            CurrentFrame = 0;
            recordMode = RecordMode.Recording;

            // 재생 모드용 비활성화를 해제
            SetPlaybackMode(false);

            CachePlayer();
            ResetPreviousCaches();
        }

        // ------------------------------------------------------------
        // [정방향 재생 시작]
        // ------------------------------------------------------------
        private void BeginReplay()
        {
            if (frames.Count == 0) return;

            playbackTick = 0;
            CurrentFrame = 0;
            recordMode = RecordMode.Replaying;

            // 재생 중에는 원래 이동/AI 스크립트를 끔
            SetPlaybackMode(true);
        }

        // ------------------------------------------------------------
        // [역재생 시작]
        // ------------------------------------------------------------
        private void BeginRewind()
        {
            if (frames.Count == 0) return;

            playbackTick = frames.Count - 1;
            CurrentFrame = playbackTick;
            recordMode = RecordMode.Rewinding;

            SetPlaybackMode(true);
        }

        // ------------------------------------------------------------
        // [재생/역재생 중 스크립트 ON/OFF]
        // ------------------------------------------------------------
        private void SetPlaybackMode(bool playback)
        {
            foreach (Behaviour behaviour in disableWhilePlayback)
            {
                if (behaviour != null)
                    behaviour.enabled = !playback;
            }
        }

        // ------------------------------------------------------------
        // [한 틱 기록]
        // ------------------------------------------------------------
        private void RecordStage()
        {
            // 기록 한도 도달 시 종료
            if (currentTick >= maxRecordedTicks)
            {
                recordMode = RecordMode.Fin;
                return;
            }

            TimelineFrame frame = new();
            frame.tick = currentTick;

            // 플레이어 기록
            frame.player = CapturePlayerRecord();

            // 등록된 모든 트래커를 돌면서
            // 적 / 오브젝트를 타입별로 분리 기록
            foreach (var pair in trackedById)
            {
                TimelineTrackable trackable = pair.Value;
                if (trackable == null) continue;

                if (trackable.TrackType == TimelineTrackable.Type.Enemy)
                    frame.enemies.Add(CaptureEnemyRecord(trackable));
                else
                    frame.objects.Add(CaptureObjectRecord(trackable));
            }

            frames.Add(frame);
            CurrentFrame = currentTick;
            currentTick++;
        }

        // ------------------------------------------------------------
        // [플레이어 기록 캡처]
        // ------------------------------------------------------------
        private PlayerRecord CapturePlayerRecord()
        {
            PlayerRecord record = new();

            if (playerObject == null)
                return record;

            Vector3 currentPos = playerObject.transform.position;

            // 좌우 이동 입력 (-1, 0, 1)
            int moveX = Mathf.RoundToInt(SimpleInput.GetAxisHorizontalRaw());

            // 현재 구조에서는 점프/대시/공격이 모두 단발 입력이므로
            // GetKeyDown / GetMouseButtonDown 값만 기록합니다.
            bool jumpPressed = SimpleInput.GetKeyDown(Key.Space);
            bool dashPressed = SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left);

            // 공격은 좌/우클릭 둘 다 포함해서 기록
            bool attackPressed =
                SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left) ||
                SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right);

            // 현재 플레이어 기준 마우스 에임 방향 계산
            Vector2 aimDirection = GetAimDirection(currentPos);

            // 플레이어가 바라보는 방향
            int facing = 1;
            if (playerController != null) facing = playerController.FacingDirection;
            else if (playerObject.transform.localScale.x < 0f) facing = -1;

            // 애니메이션 기록
            int animStateHash = 0;
            float animNormalizedTime = 0f;
            if (playerAnimator != null)
            {
                AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
                animStateHash = info.fullPathHash;
                animNormalizedTime = info.normalizedTime;
                prevPlayerAnimHash = info.fullPathHash;
            }

            record.isActive = playerObject.activeSelf;
            record.moveX = moveX;
            record.jumpPressed = jumpPressed;
            record.dashPressed = dashPressed;
            record.attackPressed = attackPressed;
            record.aimDirection = aimDirection;
            record.position = currentPos;
            record.facing = facing;
            record.animStateHash = animStateHash;
            record.animNormalizedTime = animNormalizedTime;

            // 다음 틱 비교를 위해 현재 값을 "이전 값"으로 저장
            prevPlayerPos = currentPos;
            prevMoveX = moveX;

            return record;
        }

        // ------------------------------------------------------------
        // [적 기록 캡처]
        // ------------------------------------------------------------
        private EnemyRecord CaptureEnemyRecord(TimelineTrackable trackable)
        {
            int id = trackable.TrackId;
            bool isActive = trackable.gameObject.activeSelf;
            Vector3 currentPos = trackable.transform.position;
            int currentAnimHash = trackable.GetCurrentAnimHash();

            EnemyRecord record = new EnemyRecord
            {
                id = id,
                isActive = isActive,
                position = currentPos,
                facing = trackable.GetFacing(),
                animStateHash = currentAnimHash,
                animNormalizedTime = trackable.GetCurrentAnimNormalizedTime(),
            };

            // 다음 틱 비교용 캐시는 필요 시 확장할 수 있도록 유지
            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;

            return record;
        }

        // ------------------------------------------------------------
        // [동적 오브젝트 기록 캡처]
        // ------------------------------------------------------------
        private ObjectRecord CaptureObjectRecord(TimelineTrackable trackable)
        {
            int id = trackable.TrackId;
            bool isActive = trackable.gameObject.activeSelf;
            Vector3 currentPos = trackable.transform.position;
            int currentAnimHash = trackable.GetCurrentAnimHash();

            ObjectRecord record = new ObjectRecord
            {
                id = id,
                isActive = isActive,
                position = currentPos,
                rotationZ = trackable.transform.eulerAngles.z,
                flipX = trackable.GetFacing(),
                animStateHash = currentAnimHash,
                animNormalizedTime = trackable.GetCurrentAnimNormalizedTime(),
            };

            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;

            return record;
        }

        // ------------------------------------------------------------
        // [애니메이션 기록 생성]
        // ------------------------------------------------------------
        // 이전 해시와 현재 해시를 비교해
        // 이번 틱에 애니메이션이 바뀌었는지 판별합니다.
        private AnimRecord BuildAnimRecord(int prevHash, int currentHash, float normalizedTime)
        {
            return new AnimRecord
            {
                prevStateHash = prevHash,
                currentStateHash = currentHash,
                normalizedTime = normalizedTime,

                // 이전과 현재 상태가 다르면 새로운 애니메이션 시작
                started = (prevHash != currentHash && currentHash != 0),

                // 이전과 현재 상태가 다르면 이전 애니메이션 종료
                ended = (prevHash != currentHash && prevHash != 0),
            };
        }

        // ------------------------------------------------------------
        // [플레이어 기준 에임 방향 계산]
        // ------------------------------------------------------------
        private Vector2 GetAimDirection(Vector3 origin)
        {
            if (Camera.main == null) return Vector2.right;

            Vector3 mouseWorld = Camera.main.ScreenToWorldPoint(SimpleInput.GetMousePosition());
            mouseWorld.z = origin.z;

            Vector2 dir = mouseWorld - origin;
            if (dir.sqrMagnitude > 0.0001f)
                dir.Normalize();

            return dir;
        }

        // ------------------------------------------------------------
        // [정방향 재생]
        // ------------------------------------------------------------
        private void ReplayStage()
        {
            if (playbackTick >= frames.Count)
            {
                recordMode = RecordMode.Fin;
                SetPlaybackMode(false);
                return;
            }

            ApplyFrame(frames[playbackTick]);
            CurrentFrame = playbackTick;
            playbackTick++;
        }


        // ------------------------------------------------------------
        // [역재생]
        // ------------------------------------------------------------
        private void RewindStage()
        {
            if (playbackTick < 0)
            {
                recordMode = RecordMode.Fin;
                SetPlaybackMode(false);
                return;
            }

            ApplyFrame(frames[playbackTick]);
            CurrentFrame = playbackTick;
            playbackTick--;
        }

        // ------------------------------------------------------------
        // [한 프레임 적용]
        // ------------------------------------------------------------
        // 기록해 둔 한 틱의 상태를 실제 게임 오브젝트들에 반영
        private void ApplyFrame(TimelineFrame frame)
        {
            ApplyPlayer(frame.player);

            foreach (EnemyRecord enemy in frame.enemies)
            {
                if (trackedById.TryGetValue(enemy.id, out TimelineTrackable trackable) == false)
                    continue;

                ApplyEnemy(trackable, enemy);
            }

            foreach (ObjectRecord obj in frame.objects)
            {
                if (trackedById.TryGetValue(obj.id, out TimelineTrackable trackable) == false)
                    continue;

                ApplyObject(trackable, obj);
            }
        }

        // ------------------------------------------------------------
        // [플레이어 프레임 적용]
        // ------------------------------------------------------------
        private void ApplyPlayer(PlayerRecord record)
        {
            if (playerObject == null) return;

            // 활성화 상태 맞추기
            if (playerObject.activeSelf != record.isActive)
                playerObject.SetActive(record.isActive);

            if (!record.isActive) return;

            // 위치 복원
            playerObject.transform.position = record.position;

            // 바라보는 방향 복원
            Transform visual = (playerAnimator != null) ? playerAnimator.transform : playerObject.transform;
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * record.facing;
            visual.localScale = scale;

            // 애니메이션 복원
            if (playerAnimator != null && record.animStateHash != 0)
            {
                playerAnimator.Play(record.animStateHash, 0, record.animNormalizedTime);
                playerAnimator.Update(0f);
            }
        }

        // ------------------------------------------------------------
        // [적 프레임 적용]
        // ------------------------------------------------------------
        private void ApplyEnemy(TimelineTrackable trackable, EnemyRecord record)
        {
            if (trackable == null) return;

            if (trackable.gameObject.activeSelf != record.isActive)
                trackable.gameObject.SetActive(record.isActive);

            if (!record.isActive) return;

            trackable.transform.position = record.position;
            trackable.SetFacing(record.facing);

            Animator animator = trackable.CachedAnimator;
            if (animator != null && record.animStateHash != 0)
            {
                animator.Play(record.animStateHash, 0, record.animNormalizedTime);
                animator.Update(0f);
            }
        }

        // ------------------------------------------------------------
        // [동적 오브젝트 프레임 적용]
        // ------------------------------------------------------------
        private void ApplyObject(TimelineTrackable trackable, ObjectRecord record)
        {
            if (trackable == null) return;

            if (trackable.gameObject.activeSelf != record.isActive)
                trackable.gameObject.SetActive(record.isActive);

            if (!record.isActive) return;

            trackable.transform.position = record.position;
            trackable.transform.rotation = Quaternion.Euler(0f, 0f, record.rotationZ);
            trackable.SetFacing(record.flipX);

            Animator animator = trackable.CachedAnimator;
            if (animator != null && record.animStateHash != 0)
            {
                animator.Play(record.animStateHash, 0, record.animNormalizedTime);
                animator.Update(0f);
            }
        }
    }
}
