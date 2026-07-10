using System.Collections;
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
        public TMP_Text   planStartText;
        public TMP_Text   resultText;

        [Header("타이머 게이지")]
        public Image gameTimerGauge; // Image Type = Filled, Fill Method = Horizontal

        [Header("크로노스 틱")]
        public GameObject cronosBattery;
        public Image[]    cronosTicks;
        public Image      lshiftImage;
        public Sprite[]   lShifsprites;
        [SerializeField] private Color tickChargedColor = Color.white;
        [SerializeField] private Color tickDepletedColor = Color.red;

        [Header("크로노스 UI 페이드")]
        [SerializeField] private float cronosFadeInDuration = 0.15f;
        [SerializeField] private float cronosFadeOutDuration = 0.2f;
        [SerializeField, Range(0f, 1f)] private float cronosMaxAlpha = 200f / 255f;
        private SpriteRenderer cronosSpriteRenderer;
        private Coroutine cronosFadeCoroutine;

        [Header("상태별 패널")]
        public GameObject cronosUI;
        public GameObject pauseUI;
        public GameObject replayUI;
        public GameObject rewindUI;
        public GameObject gameClearUI;

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

            if (cronosUI != null)
            {
                cronosSpriteRenderer = cronosUI.GetComponent<SpriteRenderer>();
                Color c = cronosSpriteRenderer.color;
                c.a = 0f;
                cronosSpriteRenderer.color = c;
            }
        }

        private void Start()
        {
            cronosFadeInDuration = ChronosSystem.Instance.transitionInDuration;
            cronosFadeOutDuration = ChronosSystem.Instance.transitionOutDuration;
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

        # region 입력/호출

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

        public void UpdateLShiftIcon(int num)
        {
            lshiftImage.sprite = lShifsprites[num];
        }

        public void applyCronosUI(bool active)
        {
            if (cronosSpriteRenderer == null) return;

            if (cronosFadeCoroutine != null)
                StopCoroutine(cronosFadeCoroutine);

            float targetAlpha = active ? cronosMaxAlpha : 0f;
            float duration = active ? cronosFadeInDuration : cronosFadeOutDuration;

            cronosFadeCoroutine = StartCoroutine(CronosFadeCoroutine(targetAlpha, duration));
        }

        #endregion

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

        private void SetSpriteAlpha(float alpha)
        {
            Color c = cronosSpriteRenderer.color;
            c.a = alpha;
            cronosSpriteRenderer.color = c;
        }

        private IEnumerator CronosFadeCoroutine(float targetAlpha, float duration)
        {
            float startAlpha = cronosSpriteRenderer.color.a;

            if (duration <= 0f)
            {
                SetSpriteAlpha(targetAlpha);
                cronosFadeCoroutine = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                // ✅ 크로노스 중에는 timeScale이 낮아지고, 히트스탑 중에는 0이 되므로
                // 반드시 unscaledDeltaTime을 써야 페이드 애니메이션 자체가 멈추지 않습니다.
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                SetSpriteAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
                yield return null;
            }

            SetSpriteAlpha(targetAlpha); // 최종값 확정
            cronosFadeCoroutine = null;
        }
    }
}
