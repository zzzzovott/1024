using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 设置面板：音量 + 亮度。主界面和游戏里都能打开（同一个面板）。
    /// 亮度用一层全屏黑幕的反向透明度模拟，不依赖后处理。
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        public CanvasGroup panel;
        public Slider volumeSlider;
        public Slider brightnessSlider;
        public Text volumeValue;
        public Text brightnessValue;
        public Button closeButton;
        public Image brightnessOverlay;

        public bool IsOpen { get { return panel != null && panel.alpha > 0.5f; } }

        float _volume = 0.8f;
        float _brightness = 0.85f;

        void Awake()
        {
            _volume = PlayerPrefs.GetFloat("move1024_volume", 0.8f);
            _brightness = PlayerPrefs.GetFloat("move1024_brightness", 0.85f);
        }

        void Start()
        {
            if (panel != null)
            {
                panel.alpha = 0f;
                panel.blocksRaycasts = false;
                panel.interactable = false;
            }

            if (volumeSlider != null)
            {
                volumeSlider.minValue = 0f;
                volumeSlider.maxValue = 1f;
                volumeSlider.value = _volume;
                volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            }

            if (brightnessSlider != null)
            {
                brightnessSlider.minValue = 0f;
                brightnessSlider.maxValue = 1f;
                brightnessSlider.value = _brightness;
                brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
            }

            if (closeButton != null) closeButton.onClick.AddListener(Close);

            ApplyVolume(_volume);
            ApplyBrightness(_brightness);
        }

        public void Open()
        {
            if (panel == null) return;
            panel.alpha = 1f;
            panel.blocksRaycasts = true;
            panel.interactable = true;
        }

        public void Close()
        {
            if (panel == null) return;
            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
            PlayerPrefs.Save();
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        void OnVolumeChanged(float v)
        {
            _volume = v;
            ApplyVolume(v);
        }

        void OnBrightnessChanged(float v)
        {
            _brightness = v;
            ApplyBrightness(v);
        }

        void ApplyVolume(float v)
        {
            AudioListener.volume = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat("move1024_volume", _volume);
            if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        void ApplyBrightness(float v)
        {
            // v=1 全亮（黑幕透明），v=0 最暗（黑幕浓）
            if (brightnessOverlay != null)
            {
                Color c = brightnessOverlay.color;
                c.a = Mathf.Lerp(0.72f, 0f, Mathf.Clamp01(v));
                brightnessOverlay.color = c;
            }

            PlayerPrefs.SetFloat("move1024_brightness", _brightness);
            if (brightnessValue != null) brightnessValue.text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }
}
