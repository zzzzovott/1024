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
        public FPSController player;
        public CrosshairUI crosshair;
        [Tooltip("菜单淡出时长（秒）")]
        public float panelFadeOut = 0.55f;

        [Header("继续游戏 / 存档")]
        public Button continueButton;
        public Text continueLabel;
        public Text saveInfoText;
        public GameFlowDirector flow;

        [Header("设置")]
        public Button settingsButton;
        public SettingsPanel settings;

        [Header("游戏内工具条（主菜单时隐藏）")]
        public GameObject topBar;

        [Header("文案")]
        public string title = "第 1024 次 搬 家";
        public string subtitle = "一 个 关 于 搬 家 的 记 忆";
        public string startLabel = "开 始 游 戏";
        public string continueText = "继 续 游 戏";
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

            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);

            ShowMenu();
            RefreshContinue();
        }

        /// <summary>有存档时"继续游戏"可点，否则灰掉。</summary>
        void RefreshContinue()
        {
            bool has = SaveSystem.HasSave;

            if (continueButton != null)
            {
                continueButton.interactable = has;
                Image bg = continueButton.targetGraphic as Image;
                if (bg != null)
                    bg.color = has ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.05f);
            }

            if (continueLabel != null)
            {
                continueLabel.color = has ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.22f);
                continueLabel.text = has ? continueText : continueText + "（无存档）";
            }

            if (saveInfoText != null)
                saveInfoText.text = has ? "存档：" + SaveSystem.SavedAt : "";
        }

        void OnSettingsClicked()
        {
            if (settings != null) settings.Open();
        }

        /// <summary>读档直接进游戏：跳过开场，把位置/进度还原。</summary>
        public void OnContinueClicked()
        {
            if (_started) return;

            SaveData data = SaveSystem.Load();
            if (data == null) return;

            _started = true;

            if (panel != null)
            {
                panel.blocksRaycasts = false;
                panel.interactable = false;
                panel.gameObject.SetActive(false);
            }

            if (settings != null) settings.Close();
            if (intro != null) intro.Skip();
            if (flow != null) flow.ApplySave(data);
            if (player != null) player.SetInputEnabled(true);
            if (topBar != null) topBar.SetActive(true);
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
            // 准星不再隐藏：它现在排在黑幕下面，会被面板自然盖住
            if (topBar != null) topBar.SetActive(false);
        }

        public void OnStartClicked()
        {
            if (_started) return;
            _started = true;

            if (panel != null) panel.blocksRaycasts = false;
            if (startButton != null) startButton.interactable = false;
            if (continueButton != null) continueButton.interactable = false;
            if (topBar != null) topBar.SetActive(true);

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
        }

        /// <summary>给自动化测试用：直接开始。</summary>
        public void ForceStart()
        {
            OnStartClicked();
        }
    }
}
