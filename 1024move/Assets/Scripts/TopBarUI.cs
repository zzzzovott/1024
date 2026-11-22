using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 游戏内右上角工具条：存档标志 + 设置齿轮 + 小房子。
    /// · 存档标志在小房子左边，有存档时点亮并显示时间，没存档时变灰；
    /// · 小房子点击 -> 呼出毛玻璃暂停菜单（继续 / 保存 / 回主界面 / 退出）；
    /// · 齿轮 -> 设置面板（音量 / 亮度）。
    /// </summary>
    public class TopBarUI : MonoBehaviour
    {
        [Header("图标")]
        public Image saveIcon;
        public Text saveLabel;
        public Image houseIcon;
        public Image gearIcon;
        public Button houseButton;
        public Button gearButton;

        [Header("引用")]
        public PauseMenu pauseMenu;
        public SettingsPanel settings;

        [Header("颜色")]
        public Color saveOnColor = new Color(0.85f, 0.95f, 0.85f, 0.95f);
        public Color saveOffColor = new Color(1f, 1f, 1f, 0.22f);

        bool _lastHasSave;
        string _lastTime = "";

        void Start()
        {
            if (saveIcon != null) saveIcon.sprite = UiIcons.Save;
            if (houseIcon != null) houseIcon.sprite = UiIcons.House;
            if (gearIcon != null) gearIcon.sprite = UiIcons.Gear;

            if (houseButton != null) houseButton.onClick.AddListener(OnHouseClicked);
            if (gearButton != null) gearButton.onClick.AddListener(OnGearClicked);

            Refresh(true);
        }

        void Update()
        {
            Refresh(false);
        }

        void Refresh(bool force)
        {
            bool has = SaveSystem.HasSave;
            string time = has ? SaveSystem.SavedAt : "";
            if (!force && has == _lastHasSave && time == _lastTime) return;

            _lastHasSave = has;
            _lastTime = time;

            if (saveIcon != null) saveIcon.color = has ? saveOnColor : saveOffColor;
            if (saveLabel != null)
            {
                saveLabel.text = has ? "已存档 " + time : "未存档";
                saveLabel.color = has ? new Color(1f, 1f, 1f, 0.55f) : new Color(1f, 1f, 1f, 0.25f);
            }
        }

        void OnHouseClicked()
        {
            if (pauseMenu != null) pauseMenu.OpenMenu();
        }

        void OnGearClicked()
        {
            if (settings != null) settings.Open();
        }
    }
}
