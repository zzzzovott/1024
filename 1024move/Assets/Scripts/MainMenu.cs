using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 主菜单（设计稿 V3.0 序章第 1 步：点击【开始游戏】→ 黑屏 → 开场苏醒）。
    ///
    /// 开机先显示标题 + 开始按钮，玩家点击后：
    /// 菜单淡出 → 调用 IntroSequence.Begin()（黑屏 + 撕胶带声 + 打字机）→ 黑幕淡出 → 交还操作。
    /// 开场期间准星是隐藏的，等开场结束再出现。
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("界面")]
        public CanvasGroup panel;
        public Button startButton;
        public Text titleText;
        public Text subtitleText;
        public Text hintText;

        [Header("进入游戏")]
        public IntroSequence intro;
        public Player25DController player;
        public CrosshairUI crosshair;
        [Tooltip("菜单淡出时长（秒）")]
        public float panelFadeOut = 0.55f;

        [Header("文案")]
        public string title = "第 1024 次 搬 家";
        public string subtitle = "一 个 关 于 搬 家 的 记 忆";
        public string startLabel = "开 始 游 戏";
        public string hint = "戴上耳机，点击开始";

        bool _started;

        void Awake()
        {
            if (panel == null) panel = GetComponent<CanvasGroup>();
        }

        void Start()
        {
            if (titleText != null) titleText.text = title;
            if (subtitleText != null) subtitleText.text = subtitle;
            if (hintText != null) hintText.text = hint;

            if (startButton != null)
            {
                Text label = startButton.GetComponentInChildren<Text>();
                if (label != null) label.text = startLabel;
                startButton.onClick.AddListener(OnStartClicked);
            }

            ShowMenu();
        }

        public void ShowMenu()
        {
            if (panel != null)
            {
                panel.alpha = 1f;
                panel.blocksRaycasts = true;
                panel.interactable = true;
            }

            if (player != null) player.SetInputEnabled(false);
            if (crosshair != null) crosshair.gameObject.SetActive(false);
        }

        public void OnStartClicked()
        {
            if (_started) return;
            _started = true;

            if (panel != null) panel.blocksRaycasts = false;
            if (startButton != null) startButton.interactable = false;

            // 先让开场黑幕盖住（IntroSequence 自己会把黑幕拉满），菜单再慢慢淡出，看不到接缝
            if (intro != null) intro.Begin();

            StartCoroutine(StartRoutine());
        }

        IEnumerator StartRoutine()
        {
            float t = 0f;
            while (t < panelFadeOut && panel != null)
            {
                t += Time.deltaTime;
                panel.alpha = Mathf.Lerp(1f, 0f, t / panelFadeOut);
                yield return null;
            }

            if (panel != null)
            {
                panel.alpha = 0f;
                panel.interactable = false;
            }
            if (panel != null) panel.gameObject.SetActive(false);

            // 等开场播完再把准星放出来，免得黑屏上飘着一个白点
            float guard = 0f;
            while (intro != null && !intro.IsDone && guard < 15f)
            {
                guard += Time.deltaTime;
                yield return null;
            }

            if (crosshair != null) crosshair.gameObject.SetActive(true);
        }

        /// <summary>给自动化测试用：直接开始。</summary>
        public void ForceStart()
        {
            OnStartClicked();
        }
    }
}
