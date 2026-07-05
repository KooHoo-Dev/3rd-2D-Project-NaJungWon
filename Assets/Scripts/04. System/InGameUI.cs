using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Study_ActionPlatformer
{
    public class InGameUI : MonoBehaviour
    {
        public static InGameUI Instance;

        [Header("기본 UI")]
        public GameObject inGameInterface;
        public TMP_Text planStartText;
        public TMP_Text resultText;

        [Header("타이머 게이지")]
        public Image gameTimerGauge; // Image Type = Filled, Fill Method = Horizontal

        [Header("크로노스 틱")]
        public GameObject cronosBattery;
        public Image[] cronosTicks;
        [SerializeField] private Color tickChargedColor = Color.white;
        [SerializeField] private Color tickDepletedColor = Color.red;

        [Header("상태별 패널")]
        public GameObject pauseUI;
        public GameObject replayUI;
        public GameObject rewindUI;

        private readonly string[] resultMessage =
        {
            "아니...\n통하지 않을 거야.\n(왼쪽 클릭으로 재시작)",
            "전부 기억할 수 없어.\n더 효율적으로 움직여야 해.\n(왼쪽 클릭으로 재시작)"
        };

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;

            cronosTicks = cronosBattery.GetComponentsInChildren<Image>();

            // 씬 최초 진입 시 모든 UI끄기
            inGameInterface.SetActive(false);
            planStartText.transform.parent.gameObject.SetActive(false);
            resultText.transform.parent.gameObject.SetActive(false);
            pauseUI.SetActive(false);
            replayUI.SetActive(false);
            rewindUI.SetActive(false);
        }

        private void Update()
        {
            // 실시간 게이지 갱신.
            if (GameManager.Instance != null &&
                GameManager.Instance.CurrentGameState == GameManager.GameState.Playing)
            {
                UpdateGameTimerGauge();
                UpdateCronosTicks();
            }
        }

        // ============================================================
        // GameManager가 WaitUntil 전/후로 직접 호출하는 On/Off 메서드
        // (참고: 각 메서드는 GameManager 안에서 정확히 한 곳에서만 호출되어야 합니다.
        //  ChangeGameState()의 switch문 내부에는 이 메서드들을 다시 넣지 마세요.
        //  같은 토글을 두 곳에서 호출하면 중복 실행되어 코드 흐름을 추적하기 어려워집니다.)
        // ============================================================

        public void ShowReady()
        {
            inGameInterface.SetActive(false);
            planStartText.transform.parent.gameObject.SetActive(true);
        }

        public void HideReady()
        {
            planStartText.transform.parent.gameObject.SetActive(false);
        }

        public void ShowPlaying()
        {
            inGameInterface.SetActive(true);
            HideResult();
        }

        public void ShowDead(bool isTimeout)
        {
            if (resultText == null) return;
            resultText.text = isTimeout ? resultMessage[1] : resultMessage[0];
            resultText.transform.parent.gameObject.SetActive(true);
        }

        public void HideResult()
        {
            resultText.transform.parent.gameObject.SetActive(false);
        }

        public void ShowPause()
        {
            pauseUI.SetActive(true);
        }

        public void HidePause()
        {
            pauseUI.SetActive(false);
        }

        public void ShowReplay()
        {
            inGameInterface.SetActive(false);
            replayUI.SetActive(true);
            //ScreenEffectManager.SetGrayscale(true);
        }

        public void HideReplay()
        {
            replayUI.SetActive(false);
            //ScreenEffectManager.SetGrayscale(false);
        }

        public void ShowRewind()
        {
            inGameInterface.SetActive(false);
            HideResult();
            rewindUI.SetActive(true);
            //ScreenEffectManager.SetRewindGlitch(true);
        }

        public void HideRewind()
        {
            rewindUI.SetActive(false);
            //ScreenEffectManager.SetRewindGlitch(false);
        }

        // ============================================================
        // 실시간 게이지 (Playing 중에만 매 프레임 갱신)
        // ============================================================
        private void UpdateGameTimerGauge()
        {
            if (gameTimerGauge == null || TimeLineRecorder.recorder == null) return;

            int max = TimeLineRecorder.recorder.maxRecordedTicks;
            int current = TimeLineRecorder.CurrentFrame;

            // 1 = 가득 찬 시작 상태, 0 = 시간초과
            gameTimerGauge.fillAmount = Mathf.Clamp01(1f - (float)current / max);
        }

        private void UpdateCronosTicks()
        {
            if (cronosTicks == null || cronosTicks.Length == 0) return;

            float percent = ChronosSystem.EnergyPercent; // 0~1
            int chargedCount = Mathf.CeilToInt(percent * cronosTicks.Length);

            for (int i = 0; i < cronosTicks.Length; i++)
            {
                if (cronosTicks[i] == null) continue;
                // 앞쪽(i < chargedCount)은 충전색, 뒤쪽(마지막 인덱스부터)은 소모색
                cronosTicks[i].color = (i < chargedCount) ? tickChargedColor : tickDepletedColor;
            }
        }
    }
}
