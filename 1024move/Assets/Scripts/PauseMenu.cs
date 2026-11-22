using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 暂停 / 系统菜单。
    ///
    /// · Esc 呼出或收起，暂停时 Time.timeScale = 0，并解锁鼠标；
    /// · 面板里三个按钮：继续游戏 / 返回主界面（带小房子图标）/ 退出游戏；
    /// · 右上角还有一枚常驻的小房子，随时能回主界面（平时鼠标锁定，按住 Ctrl 才点得到）。
    ///
    /// 小房子图标是代码画的（一个三角屋顶 + 一个方块身子），不依赖切图。
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("暂停面板")]
        public CanvasGroup panel;
        [Tooltip("面板背景 Image：自定义毛玻璃着色器只认顶点色 alpha，所以这里要单独淡入淡出")]
        public Image panelImage;
        public Button resumeButton;
        public Button saveButton;
        public Button homeButton;
        public Button quitButton;
        public Image homeIcon;
        public Text hintText;
        public Text toastText;

        [Header("常驻小房子（右上角）")]
        public Image houseShortcutIcon;
        public Button houseShortcut;

        [Header("引用")]
        public FPSController player;
        public CrosshairUI crosshair;
        public AudioDirector audio;
        [Tooltip("用来判断是不是在桌面特写里：特写里 Esc 先退特写，不弹暂停")]
        public GameFlowDirector flow;

        [Header("按键与节奏")]
        public KeyCode pauseKey = KeyCode.Escape;
        public float fadeTime = 0.12f;

        [Header("文案")]
        public string title = "暂 停";
        public string hint = "按住 Ctrl 可以呼出鼠标来点按钮";

        [Header("存档提示")]
        public float toastSeconds = 1.6f;

        public bool IsOpen { get; private set; }

        float _targetAlpha;
        float _toastUntil = -1f;
        Sprite _houseSprite;

        void Awake()
        {
            _houseSprite = UiIcons.House;
            if (homeIcon != null) homeIcon.sprite = _houseSprite;
            if (houseShortcutIcon != null) houseShortcutIcon.sprite = _houseSprite;
        }

        void Start()
        {
            if (panel != null)
            {
                panel.alpha = 0f;
                panel.blocksRaycasts = false;
                panel.interactable = false;
            }
            SetPanelImageAlpha(0f);

            if (hintText != null) hintText.text = hint;
            if (toastText != null) toastText.text = "";

            if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
            if (saveButton != null) saveButton.onClick.AddListener(SaveGame);
            if (homeButton != null) homeButton.onClick.AddListener(GoHome);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
            if (houseShortcut != null) houseShortcut.onClick.AddListener(OpenMenu);

            IsOpen = false;
            _targetAlpha = 0f;
        }

        void Update()
        {
            // 暂停时 timeScale = 0，用 unscaledDeltaTime 让淡入淡出还能动
            if (panel != null)
            {
                panel.alpha = Mathf.MoveTowards(panel.alpha, _targetAlpha, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeTime));
                SetPanelImageAlpha(panel.alpha);
            }

            if (Input.GetKeyDown(pauseKey))
            {
                // 在桌面特写里，Esc 先退出特写（不然一按就暂停，玩家以为卡住）
                if (flow != null && flow.CurrentState == GameFlowDirector.State.Desk)
                {
                    flow.CancelFocus();
                    return;
                }
                Toggle();
            }

            if (toastText != null && _toastUntil > 0f && Time.unscaledTime > _toastUntil)
            {
                _toastUntil = -1f;
                toastText.text = "";
            }
        }

        /// <summary>小房子点开的入口（如果菜单已经开着就保持）。</summary>
        public void OpenMenu()
        {
            if (!IsOpen) Pause();
        }

        public void Toggle()
        {
            if (IsOpen) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsOpen) return;
            IsOpen = true;
            _targetAlpha = 1f;

            if (panel != null)
            {
                panel.blocksRaycasts = true;
                panel.interactable = true;
            }

            if (player != null) player.SetInputEnabled(false);   // 会顺便解锁鼠标
            if (crosshair != null) crosshair.SetOverrideLabel(null);
            Time.timeScale = 0f;

            if (audio != null) audio.PlayClick();
        }

        public void Resume()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _targetAlpha = 0f;

            if (panel != null)
            {
                panel.blocksRaycasts = false;
                panel.interactable = false;
            }

            Time.timeScale = 1f;
            if (player != null) player.SetInputEnabled(true);    // 会重新锁定鼠标

            if (audio != null) audio.PlayClick();
        }

        /// <summary>返回主界面：直接重载场景，主菜单会重新出现。</summary>
        public void GoHome()
        {
            Time.timeScale = 1f;
            if (audio != null) audio.PlayClick();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>存档：玩家位置朝向 + 已解开的谜题 + 密码槽，写进 PlayerPrefs 的 JSON。</summary>
        public void SaveGame()
        {
            if (flow != null) SaveSystem.Save(flow.CaptureSave());

            ShowToast("已保存  " + SaveSystem.SavedAt);

            if (audio != null) audio.PlayClick();
        }

        void ShowToast(string message)
        {
            if (toastText == null) return;
            toastText.text = message;
            _toastUntil = Time.unscaledTime + toastSeconds;
        }

        /// <summary>毛玻璃背景是自定义 UI 材质，只认顶点色 alpha，得单独设。</summary>
        void SetPanelImageAlpha(float a)
        {
            if (panelImage == null) return;
            Color c = panelImage.color;
            c.a = a;
            panelImage.color = c;
        }

        public void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>程序画的小房子：三角屋顶 + 方块身子。</summary>
        static Sprite MakeHouseSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[size * size];

            float roofTop = size * 0.92f;
            float roofBase = size * 0.55f;
            float roofHalf = size * 0.40f;
            float bodyLeft = size * 0.22f;
            float bodyRight = size * 0.78f;
            float bodyBottom = size * 0.14f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;

                    bool inRoof = fy >= roofBase && fy <= roofTop &&
                                  Mathf.Abs(fx - size * 0.5f) <= roofHalf * (roofTop - fy) / (roofTop - roofBase);

                    bool inBody = fx >= bodyLeft && fx <= bodyRight && fy >= bodyBottom && fy <= roofBase + 1f;

                    px[y * size + x] = (inRoof || inBody)
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
