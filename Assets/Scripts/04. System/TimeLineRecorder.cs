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
    ///
    /// [중요 변경점]
    /// - 기록은 여전히 "틱" 단위로 저장합니다.
    /// - 하지만 재생/역재생은 더 이상 FixedUpdate에서 한 틱씩 딱딱 끊어서 적용하지 않습니다.
    /// - Update + Time.unscaledDeltaTime 기반으로 playbackTickFloat를 움직이고,
    ///   두 틱 사이를 보간해서 매 렌더 프레임마다 샘플링합니다.
    /// - 이렇게 해야 루프 애니메이션(Idle/Run 등)이 틱 계단 현상 없이 부드럽게 보입니다.
    /// - Chronos처럼 Time.timeScale이 느려져도 재생/역재생은 unscaled time 기준으로 동작합니다.
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
            public List<EffectRecord> effects = new(); // 해당 틱의 이펙트 기록들
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
            public bool isDead;         // 적 사망 상태
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
        // [이펙트 기록]
        // ------------------------------------------------------------
        [System.Serializable]
        public struct EffectRecord
        {
            public int id;
            public bool isActive;
            public Vector3 position;
            public float rotationZ;
            public Vector3 localScale;
            public int animStateHash;
            public float animNormalizedTime;
        }

        // ------------------------------------------------------------
        // [애니메이션 기록 구조체]
        // ------------------------------------------------------------
        // 현재 슬림 구조에서는 직접 사용하지 않지만,
        // 나중에 상태 전이(시작/종료) 분석이 필요할 수 있어 주석과 구조체를 유지합니다.
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
        public static TimeLineRecorder recorder;
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

        [Header("재생 설정")]
        // 틱 기반으로 기록된 데이터를 몇 틱/초 속도로 재생할지 결정합니다.
        // 기록을 FixedUpdate(0.02)에서 했다면 기본 50이 가장 자연스럽습니다.
        [SerializeField] private float playbackTicksPerSecond = 50f;
        // 정방향 재생 배속
        [SerializeField] private float replaySpeed = 1f;

        [SerializeField] private float rewindPlaybackTicksPerSecond = 150f;
        // 역재생 배속 (카타나 제로처럼 더 빠르게 보고 싶으면 2 이상)
        [SerializeField] private float rewindSpeed = 3f;
        // 같은 상태의 루프 애니메이션은 두 틱 사이 normalizedTime을 보간해서 부드럽게 보여줍니다.
        [SerializeField] private bool interpolateLoopAnimation = true;
        // 위치도 두 틱 사이를 보간할지 여부
        [SerializeField] private bool interpolatePosition = true;

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

        // 재생 중 animator.updateMode를 원래 값으로 되돌리기 위한 캐시
        private readonly Dictionary<Animator, AnimatorUpdateMode> animatorUpdateModeCache = new();

        // 새로 등록되는 오브젝트에 줄 ID
        private int nextTrackId = 1;

        // 실제 기록용 현재 틱
        private int currentTick = 0;

        // 재생 / 역재생 시 읽어올 틱 인덱스
        private int playbackTick = 0;

        // ------------------------------------------------------------
        // [부드러운 재생용 실수 틱]
        // ------------------------------------------------------------
        // 기존 playbackTick은 정수 인덱스입니다.
        // playbackTickFloat는 10.25틱, 10.80틱처럼 틱 사이 어딘가를 가리키며,
        // 재생 시 두 틱 사이를 샘플링하는 데 사용합니다.
        private float playbackTickFloat = 0f;

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
                PauseRecording();
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

            // ------------------------------------------------------------
            // [재생/역재생은 Update + unscaled time에서 처리]
            // ------------------------------------------------------------
            // 이유:
            // 1) Time.timeScale(크로노스)의 영향을 받지 않게 하기 위함
            // 2) 렌더 프레임마다 두 틱 사이를 샘플링해서 루프 애니메이션을 부드럽게 하기 위함
            switch (recordMode)
            {
                case RecordMode.Replaying:
                    ReplayStageSmooth();
                    break;

                case RecordMode.Rewinding:
                    RewindStageSmooth();
                    break;
            }
        }

        private void FixedUpdate()
        {
            // ------------------------------------------------------------
            // [기록은 그대로 FixedUpdate 틱 기준으로 유지]
            // ------------------------------------------------------------
            // 현재 레코더는 "기록 단위"를 틱으로 쓰고 있으므로,
            // 실제 플레이 중 월드 물리 결과를 가장 자연스럽게 받으려면
            // 기록은 FixedUpdate에서 계속 수행하는 편이 안정적입니다.
            //
            // 단, 재생/역재생은 여기서 하지 않습니다.
            switch (recordMode)
            {
                case RecordMode.Recording:
                    RecordStage();
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
            objectId[id] = trackable.gameObject;

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
        }

        // ------------------------------------------------------------
        // [직전 상태 캐시 초기화]
        // ------------------------------------------------------------
        // 기록 시작 전에 "이전 값"을 잡아두어야
        // 첫 틱부터 속도/엣지 계산이 가능합니다.
        private void ResetPreviousCaches()
        {
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
        public void BeginRecording()
        {
            frames.Clear();
            currentTick = 0;
            playbackTick = 0;
            playbackTickFloat = 0f;
            CurrentFrame = 0;
            recordMode = RecordMode.Recording;

            // 재생 모드용 비활성화를 해제
            SetPlaybackMode(false);
            RestoreAnimatorUpdateModes();

            CachePlayer();
            ResetPreviousCaches();
        }

        // ------------------------------------------------------------
        // [기록 일시정지/재시작]
        // ------------------------------------------------------------
        public void PauseRecording()
        {
            if (recordMode == RecordMode.Recording) recordMode = RecordMode.Pause;
            else if (recordMode == RecordMode.Pause) recordMode = RecordMode.Recording;
        }

        // ------------------------------------------------------------
        // [정방향 재생 시작]
        // ------------------------------------------------------------
        public void BeginReplay()
        {
            if (frames.Count == 0) return;

            playbackTick = 0;
            playbackTickFloat = 0f;
            CurrentFrame = 0;
            recordMode = RecordMode.Replaying;

            // 재생 중에는 원래 이동/AI 스크립트를 끔
            SetPlaybackMode(true);
            CacheAndSetAnimatorsToUnscaledTime();

            // 시작 즉시 첫 프레임을 한 번 적용해 빈 화면 한 프레임을 막습니다.
            SampleFrame(playbackTickFloat, false);
        }

        // ------------------------------------------------------------
        // [역재생 시작]
        // ------------------------------------------------------------
        public void BeginRewind()
        {
            if (frames.Count == 0) return;

            playbackTick = frames.Count - 1;
            playbackTickFloat = frames.Count - 1;
            CurrentFrame = playbackTick;
            recordMode = RecordMode.Rewinding;

            SetPlaybackMode(true);
            CacheAndSetAnimatorsToUnscaledTime();

            SampleFrame(playbackTickFloat, true);
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
        // [Animator UpdateMode 캐시/복원]
        // ------------------------------------------------------------
        // Chronos로 timeScale을 낮췄을 때도 재생/역재생용 샘플링은
        // 실시간(unscaled) 기준으로 보여주기 위해 사용합니다.
        private void CacheAndSetAnimatorsToUnscaledTime()
        {
            animatorUpdateModeCache.Clear();

            CacheAnimatorUpdateMode(playerAnimator);

            foreach (var pair in trackedById)
            {
                if (pair.Value == null) continue;
                CacheAnimatorUpdateMode(pair.Value.CachedAnimator);
            }

            foreach (var pair in animatorUpdateModeCache)
            {
                if (pair.Key != null)
                    pair.Key.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
        }

        private void CacheAnimatorUpdateMode(Animator animator)
        {
            if (animator == null) return;
            if (animatorUpdateModeCache.ContainsKey(animator)) return;
            animatorUpdateModeCache.Add(animator, animator.updateMode);
        }

        private void RestoreAnimatorUpdateModes()
        {
            foreach (var pair in animatorUpdateModeCache)
            {
                if (pair.Key != null)
                    pair.Key.updateMode = pair.Value;
            }

            animatorUpdateModeCache.Clear();
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

                switch (trackable.TrackType)
                {
                    case TimelineTrackable.Type.Enemy:
                        frame.enemies.Add(CaptureEnemyRecord(trackable));
                        break;

                    case TimelineTrackable.Type.Effect:
                        frame.effects.Add(CaptureEffectRecord(trackable));
                        break;

                    default:
                        frame.objects.Add(CaptureObjectRecord(trackable));
                        break;
                }
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
                // 루프 애니메이션 보간을 쉽게 하려면 0~1 범위로 감아두는 쪽이 다루기 편합니다.
                animNormalizedTime = Mathf.Repeat(info.normalizedTime, 1f);
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
                isDead = trackable.GetEnemyDead(),
                position = currentPos,
                facing = trackable.GetFacing(),
                animStateHash = currentAnimHash,
                animNormalizedTime = Mathf.Repeat(trackable.GetCurrentAnimNormalizedTime(), 1f),
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
                animNormalizedTime = Mathf.Repeat(trackable.GetCurrentAnimNormalizedTime(), 1f),
            };

            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;

            return record;
        }

        // ------------------------------------------------------------
        // [이펙트 기록 캡처]
        // ------------------------------------------------------------
        private EffectRecord CaptureEffectRecord(TimelineTrackable trackable)
        {
            int id = trackable.TrackId;
            bool isActive = trackable.gameObject.activeSelf;
            Vector3 currentPos = trackable.transform.position;
            int currentAnimHash = trackable.GetCurrentAnimHash();

            EffectRecord record = new EffectRecord
            {
                id = id,
                isActive = isActive,
                position = currentPos,
                rotationZ = trackable.transform.eulerAngles.z,
                localScale = trackable.transform.localScale,
                animStateHash = currentAnimHash,
                animNormalizedTime = Mathf.Repeat(trackable.GetCurrentAnimNormalizedTime(), 1f),
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
        // 현재 구현에서는 직접 사용하지 않지만,
        // 나중에 특정 전이 구간만 별도 처리할 때 다시 활용할 수 있어 남겨둡니다.
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
        // [정방향 재생 - 부드러운 샘플링 버전]
        // ------------------------------------------------------------
        private void ReplayStageSmooth()
        {
            if (frames.Count == 0)
            {
                StopPlayback();
                return;
            }

            // unscaledDeltaTime를 사용하므로 Time.timeScale의 영향을 받지 않습니다.
            playbackTickFloat += playbackTicksPerSecond * replaySpeed * Time.unscaledDeltaTime;

            if (playbackTickFloat >= frames.Count - 1)
            {
                playbackTickFloat = frames.Count - 1;
                SampleFrame(playbackTickFloat, false);
                CurrentFrame = Mathf.Clamp(Mathf.FloorToInt(playbackTickFloat), 0, frames.Count - 1);
                StopPlayback();
                return;
            }

            SampleFrame(playbackTickFloat, false);
            CurrentFrame = Mathf.Clamp(Mathf.FloorToInt(playbackTickFloat), 0, frames.Count - 1);
        }


        // ------------------------------------------------------------
        // [역재생 - 부드러운 샘플링 버전]
        // ------------------------------------------------------------
        private void RewindStageSmooth()
        {
            if (frames.Count == 0)
            {
                StopPlayback();
                return;
            }

            playbackTickFloat -= rewindPlaybackTicksPerSecond * rewindSpeed * Time.unscaledDeltaTime;

            if (playbackTickFloat <= 0f)
            {
                playbackTickFloat = 0f;
                SampleFrame(playbackTickFloat, true);
                CurrentFrame = 0;
                StopPlayback();
                return;
            }

            SampleFrame(playbackTickFloat, true);
            CurrentFrame = Mathf.Clamp(Mathf.FloorToInt(playbackTickFloat), 0, frames.Count - 1);
        }

        // ------------------------------------------------------------
        // [재생 종료 공통 처리]
        // ------------------------------------------------------------
        private void StopPlayback()
        {
            recordMode = RecordMode.Fin;
            SetPlaybackMode(false);
            RestoreAnimatorUpdateModes();
        }

        // ------------------------------------------------------------
        // [두 틱 사이 샘플링]
        // ------------------------------------------------------------
        // 예: playbackTickFloat = 10.25 라면
        // frame 10과 frame 11 사이를 t=0.25로 보간해서 보여줍니다.
        // 이렇게 해야 루프 애니메이션과 이동이 끊기지 않고 부드럽게 보입니다.
        private void SampleFrame(float tickFloat, bool reverse)
        {
            if (frames.Count == 0)
                return;

            int fromIndex = Mathf.Clamp(Mathf.FloorToInt(tickFloat), 0, frames.Count - 1);
            int toIndex = Mathf.Clamp(fromIndex + 1, 0, frames.Count - 1);
            float t = Mathf.Clamp01(tickFloat - fromIndex);

            TimelineFrame fromFrame = frames[fromIndex];
            TimelineFrame toFrame = frames[toIndex];

            SamplePlayer(fromFrame.player, toFrame.player, t, reverse);
            SampleEnemies(fromFrame.enemies, toFrame.enemies, t, reverse);
            SampleObjects(fromFrame.objects, toFrame.objects, t, reverse);
            SampleEffects(fromFrame.effects, toFrame.effects, t, reverse);
        }

        // ------------------------------------------------------------
        // [플레이어 샘플링]
        // ------------------------------------------------------------
        private void SamplePlayer(PlayerRecord from, PlayerRecord to, float t, bool reverse)
        {
            if (playerObject == null) return;

            // 재생 중 활성화 상태는 from/to 중 현재 구간에서 더 자연스러운 쪽을 선택합니다.
            // 일반적으로 틱 경계 이전에는 from, 이후에는 to를 따르는 편이 덜 튑니다.
            bool active = (t < 0.5f) ? from.isActive : to.isActive;
            if (playerObject.activeSelf != active)
                playerObject.SetActive(active);

            if (!active) return;

            // 위치 보간
            Vector3 sampledPosition = interpolatePosition
                ? Vector3.LerpUnclamped(from.position, to.position, t)
                : ((t < 0.5f) ? from.position : to.position);

            playerObject.transform.position = sampledPosition;

            // 방향 보간은 의미가 없으므로 더 가까운 틱의 방향을 사용
            int sampledFacing = (t < 0.5f) ? from.facing : to.facing;
            Transform visual = (playerAnimator != null) ? playerAnimator.transform : playerObject.transform;
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (sampledFacing == 0 ? 1 : sampledFacing);
            visual.localScale = scale;

            // 애니메이션 샘플링
            SampleAnimator(playerAnimator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        // ------------------------------------------------------------
        // [적 목록 샘플링]
        // ------------------------------------------------------------
        private void SampleEnemies(List<EnemyRecord> fromList, List<EnemyRecord> toList, float t, bool reverse)
        {
            Dictionary<int, EnemyRecord> toLookup = BuildEnemyLookup(toList);

            foreach (EnemyRecord from in fromList)
            {
                if (!trackedById.TryGetValue(from.id, out TimelineTrackable trackable))
                    continue;

                EnemyRecord to = toLookup.TryGetValue(from.id, out EnemyRecord found) ? found : from;
                SampleEnemy(trackable, from, to, t, reverse);
            }
        }

        // ------------------------------------------------------------
        // [오브젝트 목록 샘플링]
        // ------------------------------------------------------------
        private void SampleObjects(List<ObjectRecord> fromList, List<ObjectRecord> toList, float t, bool reverse)
        {
            Dictionary<int, ObjectRecord> toLookup = BuildObjectLookup(toList);

            foreach (ObjectRecord from in fromList)
            {
                if (!trackedById.TryGetValue(from.id, out TimelineTrackable trackable))
                    continue;

                ObjectRecord to = toLookup.TryGetValue(from.id, out ObjectRecord found) ? found : from;
                SampleObject(trackable, from, to, t, reverse);
            }
        }

        private Dictionary<int, EnemyRecord> BuildEnemyLookup(List<EnemyRecord> list)
        {
            Dictionary<int, EnemyRecord> lookup = new();
            foreach (EnemyRecord record in list)
                lookup[record.id] = record;
            return lookup;
        }

        private Dictionary<int, ObjectRecord> BuildObjectLookup(List<ObjectRecord> list)
        {
            Dictionary<int, ObjectRecord> lookup = new();
            foreach (ObjectRecord record in list)
                lookup[record.id] = record;
            return lookup;
        }

        private void SampleEffects(List<EffectRecord> fromList, List<EffectRecord> toList, float t, bool reverse)
        {
            Dictionary<int, EffectRecord> toLookup = BuildEffectLookup(toList);

            foreach (EffectRecord from in fromList)
            {
                if (!trackedById.TryGetValue(from.id, out TimelineTrackable trackable))
                    continue;

                EffectRecord to = toLookup.TryGetValue(from.id, out EffectRecord found) ? found : from;
                SampleEffect(trackable, from, to, t, reverse);
            }
        }

        private Dictionary<int, EffectRecord> BuildEffectLookup(List<EffectRecord> list)
        {
            Dictionary<int, EffectRecord> lookup = new();
            foreach (EffectRecord record in list)
                lookup[record.id] = record;
            return lookup;
        }

        // ------------------------------------------------------------
        // [적 샘플링]
        // ------------------------------------------------------------
        private void SampleEnemy(TimelineTrackable trackable, EnemyRecord from, EnemyRecord to, float t, bool reverse)
        {
            if (trackable == null) return;

            bool active = (t < 0.5f) ? from.isActive : to.isActive;
            if (trackable.gameObject.activeSelf != active)
                trackable.gameObject.SetActive(active);

            if (!active) return;

            trackable.transform.position = interpolatePosition
                ? Vector3.LerpUnclamped(from.position, to.position, t)
                : ((t < 0.5f) ? from.position : to.position);

            trackable.SetFacing((t < 0.5f) ? from.facing : to.facing);
            trackable.ApplyEnemyDead((t < 0.5f) ? from.isDead : to.isDead);

            Animator animator = trackable.CachedAnimator;
            SampleAnimator(animator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        // ------------------------------------------------------------
        // [동적 오브젝트 샘플링]
        // ------------------------------------------------------------
        private void SampleObject(TimelineTrackable trackable, ObjectRecord from, ObjectRecord to, float t, bool reverse)
        {
            if (trackable == null) return;

            bool active = (t < 0.5f) ? from.isActive : to.isActive;
            if (trackable.gameObject.activeSelf != active)
                trackable.gameObject.SetActive(active);

            if (!active) return;

            trackable.transform.position = interpolatePosition
                ? Vector3.LerpUnclamped(from.position, to.position, t)
                : ((t < 0.5f) ? from.position : to.position);

            float sampledRotation = Mathf.LerpAngle(from.rotationZ, to.rotationZ, t);
            trackable.transform.rotation = Quaternion.Euler(0f, 0f, sampledRotation);
            trackable.SetFacing((t < 0.5f) ? from.flipX : to.flipX);

            Animator animator = trackable.CachedAnimator;
            SampleAnimator(animator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        private void SampleEffect(TimelineTrackable trackable, EffectRecord from, EffectRecord to, float t, bool reverse)
        {
            if (trackable == null) return;

            bool active = (t < 0.5f) ? from.isActive : to.isActive;
            if (trackable.gameObject.activeSelf != active)
                trackable.gameObject.SetActive(active);

            if (!active) return;

            trackable.transform.position = interpolatePosition
                ? Vector3.LerpUnclamped(from.position, to.position, t)
                : ((t < 0.5f) ? from.position : to.position);

            float sampledRotation = Mathf.LerpAngle(from.rotationZ, to.rotationZ, t);
            trackable.transform.rotation = Quaternion.Euler(0f, 0f, sampledRotation);
            trackable.transform.localScale = Vector3.LerpUnclamped(from.localScale, to.localScale, t);

            Animator animator = trackable.CachedAnimator;
            SampleAnimator(animator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        // ------------------------------------------------------------
        // [애니메이터 샘플링]
        // ------------------------------------------------------------
        // 같은 상태(hashA == hashB)인 경우에만 normalizedTime을 보간합니다.
        // 상태가 바뀌는 틱에서는 억지로 보간하면 Attack -> Run 사이가 오히려 더 부자연스러우므로
        // 더 가까운 쪽의 프레임을 그대로 사용합니다.
        private void SampleAnimator(
            Animator animator,
            int hashA, float timeA,
            int hashB, float timeB,
            float t,
            bool reverse)
        {
            if (animator == null)
                return;

            int sampledHash;
            float sampledTime;

            // 둘 다 비어 있으면 아무 것도 하지 않음
            if (hashA == 0 && hashB == 0)
                return;

            // 현재와 다음 틱의 상태가 같으면 루프 애니메이션 보간 가능
            if (hashA != 0 && hashA == hashB && interpolateLoopAnimation)
            {
                sampledHash = hashA;
                sampledTime = reverse
                    ? LerpLoop01Reverse(timeA, timeB, t)
                    : LerpLoop01(timeA, timeB, t);
            }
            else
            {
                // 상태가 바뀌는 경계에서는 더 가까운 쪽 상태를 사용
                bool useTo = t >= 0.5f;
                sampledHash = useTo ? hashB : hashA;
                sampledTime = useTo ? timeB : timeA;

                // from이 비고 to만 있거나, 그 반대인 경우를 보정
                if (sampledHash == 0)
                {
                    sampledHash = (hashA != 0) ? hashA : hashB;
                    sampledTime = (hashA != 0) ? timeA : timeB;
                }
            }

            animator.Play(sampledHash, 0, sampledTime);
            animator.Update(0f);
        }

        // ------------------------------------------------------------
        // [루프 애니메이션 진행도 보간]
        // ------------------------------------------------------------
        // 0.95 -> 0.05는 사실 앞으로 조금 더 간 상태입니다.
        // 일반 Lerp를 쓰면 중간에 0.5로 튀어서 애니메이션이 거꾸로 점프하는 것처럼 보일 수 있습니다.
        private float LerpLoop01(float from, float to, float t)
        {
            from = Mathf.Repeat(from, 1f);
            to = Mathf.Repeat(to, 1f);

            if (to < from)
                to += 1f;

            return Mathf.Repeat(Mathf.LerpUnclamped(from, to, t), 1f);
        }

        // ------------------------------------------------------------
        // [루프 애니메이션 진행도 역방향 보간]
        // ------------------------------------------------------------
        // 역재생일 때는 반대로 0.05 -> 0.95가 사실 뒤로 조금 간 상태일 수 있습니다.
        private float LerpLoop01Reverse(float from, float to, float t)
        {
            from = Mathf.Repeat(from, 1f);
            to = Mathf.Repeat(to, 1f);

            if (to > from)
                from += 1f;

            return Mathf.Repeat(Mathf.LerpUnclamped(from, to, t), 1f);
        }

        // ------------------------------------------------------------
        // [기존 한 프레임 즉시 적용 함수]
        // ------------------------------------------------------------
        // 예전 방식(틱마다 즉시 스냅샷 적용)을 유지하고 싶을 때 참고할 수 있도록 남겨둡니다.
        // 현재 루프 애니메이션 버벅임 문제 때문에 실제 재생 경로에서는 사용하지 않습니다.
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

            foreach (EffectRecord effect in frame.effects)
            {
                if (trackedById.TryGetValue(effect.id, out TimelineTrackable trackable) == false)
                    continue;

                ApplyEffect(trackable, effect);
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
            trackable.ApplyEnemyDead(record.isDead);

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

        private void ApplyEffect(TimelineTrackable trackable, EffectRecord record)
        {
            if (trackable == null) return;

            if (trackable.gameObject.activeSelf != record.isActive)
                trackable.gameObject.SetActive(record.isActive);

            if (!record.isActive) return;

            trackable.transform.position = record.position;
            trackable.transform.rotation = Quaternion.Euler(0f, 0f, record.rotationZ);
            trackable.transform.localScale = record.localScale;

            Animator animator = trackable.CachedAnimator;
            if (animator != null && record.animStateHash != 0)
            {
                animator.Play(record.animStateHash, 0, record.animNormalizedTime);
                animator.Update(0f);
            }
        }
    }
}
