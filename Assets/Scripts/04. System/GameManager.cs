using Jay;
using Study.Utilities;
using Study_System;
using System.Collections;
using Unity.VisualScripting;
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
        public int currentStageNum = 1;
        public const int MAX_STAGE_NUM = 3;

        [Header("씬 이름 설정")]
        [SerializeField] private string[] stageSceneNames = { "Stage_1", "Stage_2", "Stage_3" };

        [Header("재생 스킵 설정")]
        public bool recordSkeep = false;


        public enum GameState
        {
            Ready,
            Playing,
            Pause,
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
            RefreshSceneReferences();
            ChangeGameState(GameState.Playing); // 이걸 나중에 버튼 클릭으로 빼버리기
        }

        private void RefreshSceneReferences()
        {
            // 씬마다 새로 배치된 컴포넌트들 찾기
            recorder = TimeLineRecorder.recorder;
            unitManagement = UnitManagement.Instance;
            clearTrigger = GameObject.FindWithTag("ClearTrigger");

            if (clearTrigger != null)
                clearTrigger.SetActive(false); // 초기에는 비활성화

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
            if (SimpleInput.GetKeyDown(Key.Escape))
            {
                if (CurrentGameState == GameState.Playing)
                    ChangeGameState(GameState.Pause);
                else if (CurrentGameState == GameState.Pause)
                    ChangeGameState(GameState.Playing);
            }
            if(SimpleInput.GetMouseButtonDown(SimpleInput.MouseButton.Left))
            {
                if (CurrentGameState == GameState.RePlay) recordSkeep = true;
            }
        }

        public void ChangeGameState(GameState newState)
        {
            if (CurrentGameState == newState) return;

            Debug.Log($"[GameManager] 상태 변경: {CurrentGameState} → {newState}");
            CurrentGameState = newState;

            switch (newState)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    recorder?.BeginRecording();
                    break;

                case GameState.Pause:
                    Time.timeScale = 0f;
                    recorder?.PauseRecording();
                    // TODO: 설정창 UI 열기
                    break;

                case GameState.Rewind:
                    unitManagement?.ResetUnit();
                    recorder?.BeginRewind();
                    break;

                case GameState.RePlay:
                    recorder?.BeginReplay();
                    break;

                case GameState.Fin:
                    StartCoroutine(LoadNextStageCoroutine());
                    break;
            }
        }

        #region 외부 호출

        // 모든 적이 죽었을 때 직접 호출
        public void OnAllEnemiesDead()
        {
            Debug.Log("[GameManager] 모든 적 처치 완료!");

            if (clearTrigger != null)
            {
                clearTrigger.SetActive(true); // 클리어 포탈 활성화
            }
            else
            {
                // 클리어 트리거가 없으면 바로 클리어
                ClearStage();
            }
        }

        /// StageClearTrigger에서 플레이어가 ClearTrigger에 닿았을 때 호출
        public void ClearStage()
        {
            StartCoroutine(ClearStageCoroutine());
        }

        /// 플레이어 사망 시 호출
        public void RestartStage()
        {
            StartCoroutine(RestartStageCoroutine());
        }

        #endregion

        #region 코루틴
        private IEnumerator ClearStageCoroutine()
        {
            ChangeGameState(GameState.RePlay);

            // 녹화 재생 완료 또는 스킵 입력까지 대기
            yield return new WaitUntil(() => 
            TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Fin ||
            recordSkeep);

            recordSkeep = false;
            // 다음 스테이지로
            ChangeGameState(GameState.Fin);
        }

        private IEnumerator RestartStageCoroutine()
        {
            ChangeGameState(GameState.Rewind);

            // 역재생 완료까지 대기
            yield return new WaitUntil(() => TimeLineRecorder.recordMode == TimeLineRecorder.RecordMode.Fin);

            // 게임 재시작
            unitManagement.ResetUnit();

            ChangeGameState(GameState.Ready);
        }

        private IEnumerator LoadNextStageCoroutine()
        {
            yield return new WaitForSecondsRealtime(1.0f);

            currentStageNum++;

            if (currentStageNum > MAX_STAGE_NUM)
            {
                Debug.Log("[GameManager] 모든 스테이지 클리어!");
                //여기 클리어 UI띄워야함
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
