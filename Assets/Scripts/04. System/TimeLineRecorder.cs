using Study.Utilities;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Study_ActionPlatformer
{
    public class TimeLineRecorder : MonoBehaviour
    {
        [System.Serializable]
        public class TimelineFrame
        {
            public int tick;
            public PlayerRecord player;
            public List<EnemyRecord> enemies = new();
            public List<ObjectRecord> objects = new();
            public List<EffectRecord> effects = new();
        }

        #region 기록 구조체
        [System.Serializable]
        public struct PlayerRecord
        {
            public bool isActive;
            public int moveX;
            public bool jumpPressed;
            public bool dashPressed;
            public bool attackPressed;
            public Vector2 aimDirection;
            public Vector3 position;
            public int facing;
            public int animStateHash;
            public float animNormalizedTime;
        }

        [System.Serializable]
        public struct EnemyRecord
        {
            public int id;
            public bool isActive;
            public bool isDead;
            public Vector3 position;
            public int facing;
            public int animStateHash;
            public float animNormalizedTime;
        }

        [System.Serializable]
        public struct ObjectRecord
        {
            public int id;
            public bool isActive;
            public Vector3 position;
            public float rotationZ;
            public int flipX;
            public int animStateHash;
            public float animNormalizedTime;
        }

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

        [System.Serializable]
        public struct AnimRecord
        {
            public int prevStateHash;
            public int currentStateHash;
            public float normalizedTime;
            public bool started;
            public bool ended;
        }
        #endregion

        public enum RecordMode
        {
            Idle,
            Recording,
            Pause,
            Fin,
            Rewinding,
            Replaying,
        }

        public static TimeLineRecorder recorder;
        public static RecordMode recordMode;
        private static RecordMode prevRecordMode = RecordMode.Idle;

        [Header("기본 대상")]
        [SerializeField] private GameObject playerObject;
        [SerializeField] private List<GameObject> needRecordObject = new();

        [Header("재생/역재생 중 꺼둘 스크립트")]
        [SerializeField] private List<Behaviour> disableWhilePlayback = new();

        [Header("디버그 입력 설정")]
        [SerializeField] private bool enableDebugHotkeys = false;

        [Header("기록 설정")]
        [field: SerializeField] public int maxRecordedTicks { get; private set; } = 15000;
        public static bool WasTimeoutStop { get; private set; } = false;


        [Header("재생 설정")]
        [SerializeField] private float playbackTicksPerSecond = 50f;
        [SerializeField] private float replaySpeed = 1f;
        [SerializeField] private float rewindPlaybackTicksPerSecond = 150f;
        [SerializeField] private float rewindSpeed = 3f;
        [SerializeField] private bool interpolateLoopAnimation = true;
        [SerializeField] private bool interpolatePosition = true;

        public List<TimelineFrame> frames = new();
        public Dictionary<int, GameObject> objectId = new();
        public static int CurrentFrame { get; private set; }

        private readonly Dictionary<int, TimelineTrackable> trackedById = new();
        private readonly Dictionary<GameObject, int> idByObject = new();
        private readonly Dictionary<int, Vector3> prevTrackedPos = new();
        private readonly Dictionary<int, bool> prevTrackedActive = new();
        private readonly Dictionary<int, int> prevTrackedAnimHash = new();
        private readonly Dictionary<Animator, AnimatorUpdateMode> animatorUpdateModeCache = new();

        private int nextTrackId = 1;
        private int currentTick = 0;
        private int playbackTick = 0;
        private float playbackTickFloat = 0f;

        private Vector3 prevPlayerPos;
        private int prevPlayerAnimHash;
        private int prevMoveX;
        private bool prevJumpHeld;
        private bool prevDashHeld;
        private bool prevAttackHeld;

        private Animator playerAnimator;
        private CharacterController2D playerController;

        private void Awake()
        {
            if (recorder != null && recorder != this)
            {
                Destroy(gameObject);
                return;
            }
            recorder = this;

            if (playerObject == null) return;
            playerController = playerObject.GetComponent<CharacterController2D>();
            disableWhilePlayback.Add(playerController);
            playerAnimator = playerObject.GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            CachePlayer();
            RegisterInitialObjects();
            ResetPreviousCaches();
        }

        private void Update()
        {
            if (enableDebugHotkeys)
            {
                if (SimpleInput.GetKeyDown(Key.R)) StartNewRecording();
                if (SimpleInput.GetKeyDown(Key.Escape)) TogglePauseRecording();
                if (SimpleInput.GetKeyDown(Key.T)) BeginReplay();
                if (SimpleInput.GetKeyDown(Key.Y)) BeginRewind();
            }

            switch (recordMode)
            {
                case RecordMode.Replaying:
                    ReplayStageSmooth();
                    break;
                case RecordMode.Rewinding:
                    RewindStageSmooth();
                    break;
                case RecordMode.Pause:
                    break;
            }
        }

        private void FixedUpdate()
        {
            switch (recordMode)
            {
                case RecordMode.Recording:
                    RecordStage();
                    break;
                case RecordMode.Pause:
                    break;
            }
        }

        // ============================================================
        // GameManager 전용 통합 일시정지/재개 API
        // ============================================================

        public void PauseAll()
        {
            if (recordMode == RecordMode.Pause ||
                recordMode == RecordMode.Idle ||
                recordMode == RecordMode.Fin)
                return;

            prevRecordMode = recordMode;

            if (prevRecordMode == RecordMode.Rewinding ||
                prevRecordMode == RecordMode.Replaying)
            {
                RestoreAnimatorUpdateModes();
            }

            recordMode = RecordMode.Pause;
            Debug.Log($"[TimeLineRecorder] PauseAll: {prevRecordMode} → Pause");
        }

        public void ResumeAll()
        {
            if (recordMode != RecordMode.Pause) return;

            recordMode = prevRecordMode;
            Debug.Log($"[TimeLineRecorder] ResumeAll: Pause → {recordMode}");

            if (recordMode == RecordMode.Rewinding ||
                recordMode == RecordMode.Replaying)
            {
                CacheAndSetAnimatorsToUnscaledTime();
            }
        }

        // ============================================================
        // Trackable 등록
        // ============================================================

        public static void RegisterTrackable(TimelineTrackable trackable)
        {
            if (recorder == null || trackable == null) return;
            recorder.RegisterInternal(trackable);
        }

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

        private void RegisterInternal(TimelineTrackable trackable)
        {
            if (trackable == null) return;
            if (idByObject.ContainsKey(trackable.gameObject)) return;

            int id = nextTrackId++;
            trackable.SetTrackId(id);

            trackedById[id] = trackable;
            idByObject[trackable.gameObject] = id;
            objectId[id] = trackable.gameObject;

            if (!needRecordObject.Contains(trackable.gameObject))
                needRecordObject.Add(trackable.gameObject);

            prevTrackedPos[id] = trackable.transform.position;
            prevTrackedActive[id] = trackable.gameObject.activeSelf;
            prevTrackedAnimHash[id] = trackable.GetCurrentAnimHash();
        }

        private void CachePlayer()
        {
            if (playerObject == null) return;
            prevPlayerPos = playerObject.transform.position;
            prevPlayerAnimHash = (playerAnimator != null)
                ? playerAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash : 0;
        }

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

        // ============================================================
        // 기록 제어
        // ============================================================

        public void BeginRecording() => StartNewRecording();

        public void StartNewRecording()
        {
            WasTimeoutStop = false;
            frames.Clear();
            currentTick = 0;
            playbackTick = 0;
            playbackTickFloat = 0f;
            CurrentFrame = 0;
            recordMode = RecordMode.Recording;

            SetPlaybackMode(false);
            RestoreAnimatorUpdateModes();
            CachePlayer();
            ResetPreviousCaches();
        }

        public void PauseRecording()
        {
            if (recordMode == RecordMode.Recording)
                recordMode = RecordMode.Pause;
        }

        public void ResumeRecording()
        {
            if (recordMode == RecordMode.Pause)
                recordMode = RecordMode.Recording;
        }

        public void TogglePauseRecording()
        {
            if (recordMode == RecordMode.Recording) recordMode = RecordMode.Pause;
            else if (recordMode == RecordMode.Pause) recordMode = RecordMode.Recording;
        }

        public void BeginReplay()
        {
            if (frames.Count == 0) return;

            playbackTick = 0;
            playbackTickFloat = 0f;
            CurrentFrame = 0;
            recordMode = RecordMode.Replaying;

            SetPlaybackMode(true);
            CacheAndSetAnimatorsToUnscaledTime();
            SampleFrame(playbackTickFloat, false);
        }

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

        private void SetPlaybackMode(bool playback)
        {
            foreach (Behaviour behaviour in disableWhilePlayback)
            {
                if (behaviour != null)
                    behaviour.enabled = !playback;
            }
        }

        // ============================================================
        // 애니메이터 모드 전환
        // ============================================================

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

        // ============================================================
        // 기록 로직 - [핵심 수정] Mathf.Repeat 제거
        // ============================================================

        private void RecordStage()
        {
            if (currentTick >= maxRecordedTicks)
            {
                WasTimeoutStop = true;
                recordMode = RecordMode.Fin;
                GameManager.Instance?.OnPlayerDead();
                return;
            }

            TimelineFrame frame = new();
            frame.tick = currentTick;
            frame.player = CapturePlayerRecord();

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

        private PlayerRecord CapturePlayerRecord()
        {
            PlayerRecord record = new();
            if (playerObject == null) return record;

            Vector3 currentPos = playerObject.transform.position;
            int moveX = Mathf.RoundToInt(SimpleInput.GetAxisHorizontalRaw());
            bool jumpPressed = SimpleInput.GetKeyDown(Key.W);
            bool dashPressed = SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left);
            bool attackPressed = dashPressed || SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Right);
            Vector2 aimDirection = GetAimDirection(currentPos);

            int facing = 1;
            if (playerController != null) facing = playerController.FacingDirection;
            else if (playerObject.transform.localScale.x < 0f) facing = -1;

            int animStateHash = 0;
            float animNormalizedTime = 0f;
            if (playerAnimator != null)
            {
                AnimatorStateInfo info = playerAnimator.GetCurrentAnimatorStateInfo(0);
                animStateHash = info.fullPathHash;
                // ✅ 핵심 수정: Mathf.Repeat 제거 → 실제 normalizedTime 그대로 기록
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

            prevPlayerPos = currentPos;
            prevMoveX = moveX;
            return record;
        }

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
                // ✅ 핵심 수정: Mathf.Repeat 제거
                animNormalizedTime = trackable.GetCurrentAnimNormalizedTime(),
            };

            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;
            return record;
        }

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
                // ✅ 핵심 수정: Mathf.Repeat 제거
                animNormalizedTime = trackable.GetCurrentAnimNormalizedTime(),
            };

            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;
            return record;
        }

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
                // ✅ 핵심 수정: Mathf.Repeat 제거 → 단발성 애니메이션 실제 시간 그대로 기록
                animNormalizedTime = trackable.GetCurrentAnimNormalizedTime(),
            };

            prevTrackedPos[id] = currentPos;
            prevTrackedActive[id] = isActive;
            prevTrackedAnimHash[id] = currentAnimHash;
            return record;
        }

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

        // ============================================================
        // 재생 루프
        // ============================================================

        private void ReplayStageSmooth()
        {
            if (frames.Count == 0)
            {
                StopPlayback();
                return;
            }

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

        private void StopPlayback()
        {
            recordMode = RecordMode.Fin;
            SetPlaybackMode(false);
            RestoreAnimatorUpdateModes();
        }

        // ============================================================
        // 샘플링
        // ============================================================

        private void SampleFrame(float tickFloat, bool reverse)
        {
            if (frames.Count == 0) return;

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

        private void SamplePlayer(PlayerRecord from, PlayerRecord to, float t, bool reverse)
        {
            if (playerObject == null) return;

            bool active = (t < 0.5f) ? from.isActive : to.isActive;
            if (playerObject.activeSelf != active)
                playerObject.SetActive(active);

            if (!active) return;

            Vector3 sampledPosition = interpolatePosition
                ? Vector3.LerpUnclamped(from.position, to.position, t)
                : ((t < 0.5f) ? from.position : to.position);

            playerObject.transform.position = sampledPosition;

            int sampledFacing = (t < 0.5f) ? from.facing : to.facing;
            Transform visual = (playerAnimator != null) ? playerAnimator.transform : playerObject.transform;
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * (sampledFacing == 0 ? 1 : sampledFacing);
            visual.localScale = scale;

            SampleAnimator(playerAnimator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        private void SampleEnemies(List<EnemyRecord> fromList, List<EnemyRecord> toList, float t, bool reverse)
        {
            Dictionary<int, EnemyRecord> fromLookup = BuildEnemyLookup(fromList);
            Dictionary<int, EnemyRecord> toLookup = BuildEnemyLookup(toList);

            foreach (var pair in trackedById)
            {
                if (pair.Value.TrackType != TimelineTrackable.Type.Enemy) continue;

                int id = pair.Key;
                TimelineTrackable trackable = pair.Value;

                bool hasFrom = fromLookup.TryGetValue(id, out EnemyRecord from);
                bool hasTo = toLookup.TryGetValue(id, out EnemyRecord to);

                // 이 구간 어디에도 기록이 없다면 = 아직 생성되기 전 → 강제 비활성화
                if (!hasFrom && !hasTo)
                {
                    if (trackable.gameObject.activeSelf)
                        trackable.gameObject.SetActive(false);
                    continue;
                }

                if (!hasFrom) from = to;
                if (!hasTo) to = from;

                SampleEnemy(trackable, from, to, t, reverse);
            }
        }

        private void SampleObjects(List<ObjectRecord> fromList, List<ObjectRecord> toList, float t, bool reverse)
        {
            Dictionary<int, ObjectRecord> fromLookup = BuildObjectLookup(fromList);
            Dictionary<int, ObjectRecord> toLookup = BuildObjectLookup(toList);

            foreach (var pair in trackedById)
            {
                if (pair.Value.TrackType != TimelineTrackable.Type.Object) continue;

                int id = pair.Key;
                TimelineTrackable trackable = pair.Value;

                bool hasFrom = fromLookup.TryGetValue(id, out ObjectRecord from);
                bool hasTo = toLookup.TryGetValue(id, out ObjectRecord to);

                if (!hasFrom && !hasTo)
                {
                    if (trackable.gameObject.activeSelf)
                        trackable.gameObject.SetActive(false);
                    continue;
                }

                if (!hasFrom) from = to;
                if (!hasTo) to = from;

                SampleObject(trackable, from, to, t, reverse);
            }
        }

        private void SampleEffects(List<EffectRecord> fromList, List<EffectRecord> toList, float t, bool reverse)
        {
            Dictionary<int, EffectRecord> fromLookup = BuildEffectLookup(fromList);
            Dictionary<int, EffectRecord> toLookup = BuildEffectLookup(toList);

            foreach (var pair in trackedById)
            {
                if (pair.Value.TrackType != TimelineTrackable.Type.Effect) continue;

                int id = pair.Key;
                TimelineTrackable trackable = pair.Value;

                bool hasFrom = fromLookup.TryGetValue(id, out EffectRecord from);
                bool hasTo = toLookup.TryGetValue(id, out EffectRecord to);

                if (!hasFrom && !hasTo)
                {
                    if (trackable.gameObject.activeSelf)
                        trackable.gameObject.SetActive(false);
                    continue;
                }

                if (!hasFrom) from = to;
                if (!hasTo) to = from;

                SampleEffect(trackable, from, to, t, reverse);
            }
        }

        private Dictionary<int, EnemyRecord> BuildEnemyLookup(List<EnemyRecord> list)
        {
            Dictionary<int, EnemyRecord> lookup = new();
            foreach (EnemyRecord record in list) lookup[record.id] = record;
            return lookup;
        }

        private Dictionary<int, ObjectRecord> BuildObjectLookup(List<ObjectRecord> list)
        {
            Dictionary<int, ObjectRecord> lookup = new();
            foreach (ObjectRecord record in list) lookup[record.id] = record;
            return lookup;
        }

        private Dictionary<int, EffectRecord> BuildEffectLookup(List<EffectRecord> list)
        {
            Dictionary<int, EffectRecord> lookup = new();
            foreach (EffectRecord record in list) lookup[record.id] = record;
            return lookup;
        }

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

            SampleAnimator(trackable.CachedAnimator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

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

            SampleAnimator(trackable.CachedAnimator,
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

            SampleAnimator(trackable.CachedAnimator,
                from.animStateHash, from.animNormalizedTime,
                to.animStateHash, to.animNormalizedTime,
                t, reverse);
        }

        // ============================================================
        // [핵심 수정] 애니메이터 샘플링 - 루프/단발 애니메이션 구분 처리
        // ============================================================
        private void SampleAnimator(
            Animator animator,
            int hashA, float timeA,
            int hashB, float timeB,
            float t,
            bool reverse)
        {
            if (animator == null) return;
            if (hashA == 0 && hashB == 0) return;

            int sampledHash;
            float sampledTime;

            if (hashA != 0 && hashA == hashB)
            {
                sampledHash = hashA;

                //    normalizedTime이 1.0을 넘었는지로 "단발성 여부"를 추측하지 않습니다.
                //    루프 애니메이션도 한 사이클만 지나면 똑같이 1.0을 넘어 계속 증가하므로
                //    그 값만으로는 둘을 구분할 방법이 없습니다.
                //    대신 각 State의 "Loop Time" 설정에 판단을 맡기면,
                //    루프 상태는 큰 값이 들어와도 내부적으로 올바르게 순환 재생되고,
                //    단발성 상태는 클립 길이를 넘는 순간 자동으로 마지막 프레임에 고정됩니다.
                sampledTime = interpolateLoopAnimation
                    ? Mathf.LerpUnclamped(timeA, timeB, t)
                    : ((t < 0.5f) ? timeA : timeB);
            }
            else
            {
                bool useTo = t >= 0.5f;
                sampledHash = useTo ? hashB : hashA;
                sampledTime = useTo ? timeB : timeA;

                if (sampledHash == 0)
                {
                    sampledHash = (hashA != 0) ? hashA : hashB;
                    sampledTime = (hashA != 0) ? timeA : timeB;
                }
                // ⚠️ 여기 있던 Mathf.Min(sampledTime, 1.0f) 클램프도 함께 제거합니다.
                // 이유는 위와 동일합니다.
            }

            animator.Play(sampledHash, 0, sampledTime);
            animator.Update(0f);
        }

        private float LerpLoop01(float from, float to, float t)
        {
            from = Mathf.Repeat(from, 1f);
            to = Mathf.Repeat(to, 1f);

            if (to < from)
                to += 1f;

            return Mathf.Repeat(Mathf.LerpUnclamped(from, to, t), 1f);
        }

        private float LerpLoop01Reverse(float from, float to, float t)
        {
            from = Mathf.Repeat(from, 1f);
            to = Mathf.Repeat(to, 1f);

            if (to > from)
                from += 1f;

            return Mathf.Repeat(Mathf.LerpUnclamped(from, to, t), 1f);
        }

        // ============================================================
        // ApplyFrame (레거시 - 참고용으로 유지)
        // ============================================================

        private void ApplyFrame(TimelineFrame frame)
        {
            ApplyPlayer(frame.player);

            foreach (EnemyRecord enemy in frame.enemies)
            {
                if (trackedById.TryGetValue(enemy.id, out TimelineTrackable trackable))
                    ApplyEnemy(trackable, enemy);
            }

            foreach (ObjectRecord obj in frame.objects)
            {
                if (trackedById.TryGetValue(obj.id, out TimelineTrackable trackable))
                    ApplyObject(trackable, obj);
            }

            foreach (EffectRecord effect in frame.effects)
            {
                if (trackedById.TryGetValue(effect.id, out TimelineTrackable trackable))
                    ApplyEffect(trackable, effect);
            }
        }

        private void ApplyPlayer(PlayerRecord record)
        {
            if (playerObject == null) return;

            if (playerObject.activeSelf != record.isActive)
                playerObject.SetActive(record.isActive);

            if (!record.isActive) return;

            playerObject.transform.position = record.position;

            Transform visual = (playerAnimator != null) ? playerAnimator.transform : playerObject.transform;
            Vector3 scale = visual.localScale;
            scale.x = Mathf.Abs(scale.x) * record.facing;
            visual.localScale = scale;

            if (playerAnimator != null && record.animStateHash != 0)
            {
                playerAnimator.Play(record.animStateHash, 0, record.animNormalizedTime);
                playerAnimator.Update(0f);
            }
        }

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
