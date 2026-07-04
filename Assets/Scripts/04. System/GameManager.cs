using Jay;
using Study.Utilities;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Study_ActionPlatformer
{
    public class GameManager : SingletonBase<GameManager>
    {
        [Header("씬 마다 갱신")]
        private TimeLineRecorder recorder;
        private UnitManagement unitManagement;
        private GameObject clearTrigger;

        [Header("게임 상태")]
        public GameState CurrentGameState { get; private set; } = GameState.Ready;

        // [핵심 추가] 일시정지 직전 상태를 기억하여 정확한 복귀 보장
        private GameState stateBeforePause = GameState.Playing;

        public int currentStageNum = 1;
        public const int MAX_STAGE_NUM = 3;

        [Header("씬 이름 설정")]
        [SerializeField] private string[] stageSceneNames = { "Stage_1", "Stage_2", "Stage_3" };

        [Header("재생 스킵 설정")]
        public bool recordSkeep = false;

        [Header("시작 / 재시작 연출 대기")]
        [SerializeField] private float stageEntryDelay = 0.75f;
        [SerializeField] private float restartPromptDelay = 0.15f;

        private Coroutine flowCoroutine;

        public bool CanAcceptGameplayInput => CurrentGameState == GameState.Playing;

        public enum GameState
        {
            Ready,
            Playing,
            Pause,
            Dead,
            Rewind,
            RePlay,
            Fin,
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[GameManager] 초기화 완료 - DontDestroyOnLoad 적용");
        }

        protected override void OnDispose()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        #region 씬 로드
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"[GameManager] 씬 로드: {scene.name}");

            if (flowCoroutine != null)
            {
                StopCoroutine(flowCoroutine);
                flowCoroutine = null;
            }

            RefreshSceneReferences();
            StartManagedFlow(StageEntryCoroutine());
        }

        private void RefreshSceneReferences()
        {
            recorder = TimeLineRecorder.recorder;
            unitManagement = UnitManagement.Instance;
            clearTrigger = GameObject.FindWithTag("ClearTrigger");

            if (clearTrigger != null)
                clearTrigger.SetActive(false);

            Debug.Log($"[GameManager] 참조 갱신 완료 - " +
                      $"Recorder: {recorder != null}, " +
                      $"UnitManagement: {unitManagement != null}, " +
                      $"ClearTrigger: {clearTrigger != null}");
        }
        #endregion

        // ============================================================
        // [입력 처리]
        // ============================================================
        private void Update()
        {
            // ESC: Ready/Fin 제외한 모든 상태에서 일시정지 토글
            if (SimpleInput.GetKeyDown(Key.Escape))
            {
                if (CurrentGameState == GameState.Pause)
                {
                    // 일시정지 해제 → 이전 상태로 정확히 복귀
                    ResumeFromPause();
                }
                else if (CurrentGameState != GameState.Ready &&
                         CurrentGameState != GameState.Fin)
                {
                    // 일시정지 진입 → 현재 상태 기억
                    stateBeforePause = CurrentGameState;
                    ChangeGameState(GameState.Pause);
                }
            }

            // R키: Playing 상태에서만 수동 재시작
            if (SimpleInput.GetKeyDown(Key.R))
            {
                if (CurrentGameState == GameState.Playing)
                    OnManualRestart();
            }

            // 좌클릭: RePlay 스킵
            if (SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                if (CurrentGameState == GameState.RePlay)
                    recordSkeep = true;
            }
        }

        // ============================================================
        // [핵심] 일시정지 해제 전용 메서드
        // ============================================================
        // ChangeGameState를 우회하여 역재생/재생이 처음부터 다시 시작되는 것을 방지
        private void ResumeFromPause()
        {
            if (CurrentGameState != GameState.Pause) return;

            Debug.Log($"[GameManager] Pause 해제 → {stateBeforePause} 복귀");

            // GameState 직접 설정 (ChangeGameState 우회)
            CurrentGameState = stateBeforePause;

            switch (stateBeforePause)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    recorder?.ResumeAll(); // Recording 모드 복귀
                    break;

                case GameState.Dead:
                    Time.timeScale = 1f;
                    // Dead 상태는 레코더 Pause 유지 (사망 연출 대기)
                    break;

                case GameState.Rewind:
                    Time.timeScale = 0f;
                    recorder?.ResumeAll(); // Rewinding 모드 복귀, 중단 지점부터 이어서 진행
                    break;

                case GameState.RePlay:
                    Time.timeScale = 0f;
                    recorder?.ResumeAll(); // Replaying 모드 복귀, 중단 지점부터 이어서 진행
                    break;
            }
        }

        public void ChangeGameState(GameState newState)
        {
            if (CurrentGameState == newState) return;

            GameState prevState = CurrentGameState;
            Debug.Log($"[GameManager] 상태 변경: {CurrentGameState} → {newState}");
            CurrentGameState = newState;

            switch (newState)
            {
                case GameState.Ready:
                    Time.timeScale = 0f;
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    // Pause → Playing 복귀는 ResumeFromPause()에서만 처리
                    break;

                case GameState.Pause:
                    Time.timeScale = 0f;
                    // [핵심] 모든 레코더 작업 일시정지 + 애니메이터 Normal 모드 전환
                    recorder?.PauseAll();
                    break;

                case GameState.Dead:
                    Time.timeScale = 1f;
                    recorder?.PauseAll();
                    break;

                case GameState.Rewind:
                    Time.timeScale = 0f;
                    // 항상 새로운 역재생 (Pause 복귀는 ResumeFromPause에서 처리)
                    recorder?.BeginRewind();
                    break;

                case GameState.RePlay:
                    Time.timeScale = 0f;
                    // 항상 새로운 정방향 재생
                    recorder?.BeginReplay();
                    break;

                case GameState.Fin:
                    StartCoroutine(LoadNextStageCoroutine());
                    break;
            }
        }

        #region 외부 호출

        public void OnAllEnemiesDead()
        {
            if (CurrentGameState != GameState.Playing) return;

            Debug.Log("[GameManager] 모든 적 처치 완료!");

            if (clearTrigger != null)
                clearTrigger.SetActive(true);
            else
                ClearStage();
        }

        public void ClearStage()
        {
            if (flowCoroutine != null) return;
            StartManagedFlow(ClearStageCoroutine());
        }

        public void OnPlayerDead()
        {
            if (flowCoroutine != null) return;
            if (CurrentGameState != GameState.Playing) return;
            Player.LocalPlayer.GetComponent<PlayerController>().PlayerDead(Vector2.zero, 0f);
            StartManagedFlow(DeadRestartCoroutine());
        }

        public void OnManualRestart()
        {
            if (flowCoroutine != null) return;
            StartManagedFlow(ManualRestartCoroutine());
        }

        private void StartManagedFlow(IEnumerator routine)
        {
            if (flowCoroutine != null)
                StopCoroutine(flowCoroutine);

            flowCoroutine = StartCoroutine(ManagedFlowCoroutine(routine));
        }

        #endregion

        #region 코루틴

        private IEnumerator ManagedFlowCoroutine(IEnumerator routine)
        {
            yield return StartCoroutine(routine);
            flowCoroutine = null;
        }

        private IEnumerator StageEntryCoroutine()
        {
            ChangeGameState(GameState.Ready);
            recordSkeep = false;

            yield return new WaitForSecondsRealtime(stageEntryDelay);
            yield return WaitForLeftClick();

            StartFreshRound();
        }

        private IEnumerator ClearStageCoroutine()
        {
            ChangeGameState(GameState.RePlay);

            yield return new WaitUntil(() =>
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Fin ||
                recordSkeep);

            recordSkeep = false;
            ChangeGameState(GameState.Fin);
        }

        private IEnumerator DeadRestartCoroutine()
        {
            ChangeGameState(GameState.Dead);
            yield return new WaitForSecondsRealtime(restartPromptDelay);
            yield return WaitForLeftClick();

            ChangeGameState(GameState.Rewind);
            yield return new WaitUntil(() =>
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Fin);

            StartFreshRound();
        }

        private IEnumerator ManualRestartCoroutine()
        {
            ChangeGameState(GameState.Ready);
            yield return null;
            ChangeGameState(GameState.Rewind);
            yield return new WaitUntil(() =>
                TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Fin);

            StartFreshRound();
        }

        // Pause 상태에서는 좌클릭 무시
        private IEnumerator WaitForLeftClick()
        {
            yield return null; // debounce
            while (true)
            {
                if (CurrentGameState != GameState.Pause &&
                    SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
                    break;
                yield return null;
            }
        }

        private void StartFreshRound()
        {
            recordSkeep = false;
            unitManagement?.ResetUnit();
            recorder?.StartNewRecording();

            if (clearTrigger != null)
                clearTrigger.SetActive(false);

            ChangeGameState(GameState.Playing);
        }

        private IEnumerator LoadNextStageCoroutine()
        {
            yield return new WaitForSecondsRealtime(1.0f);

            currentStageNum++;

            if (currentStageNum > MAX_STAGE_NUM)
            {
                Debug.Log("[GameManager] 모든 스테이지 클리어!");
            }
            else
            {
                int sceneIndex = currentStageNum - 1;
                if (sceneIndex < stageSceneNames.Length)
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(stageSceneNames[sceneIndex]);
                }
            }
        }

        #endregion
    }
}

