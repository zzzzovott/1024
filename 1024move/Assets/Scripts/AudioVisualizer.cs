using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 音频频谱（设计稿 V3.0 模块 6）：把音频频段映射到一排竖条的高度上。
    /// 用 AudioSource.GetSpectrumData 取 512 个采样，按对数分布分给每根柱子，
    /// 再做个衰减，看着才像老式音响上的电平表。
    /// </summary>
    public class AudioVisualizer : MonoBehaviour
    {
        [Header("数据源")]
        public AudioSource source;

        [Header("柱子")]
        public Image[] bars;
        public float minHeight = 4f;
        public float maxHeight = 110f;

        [Header("外观")]
        public CanvasGroup group;
        public Color barColor = new Color(1f, 0.82f, 0.55f, 0.9f);

        [Header("灵敏度")]
        [Tooltip("数值越大跳得越高")]
        public float sensitivity = 26f;
        [Tooltip("每帧回落速度")]
        public float falloff = 9f;

        float[] _samples = new float[512];
        float[] _levels;
        float[] _raw;
        float _hideAt = -1f;

        void Awake()
        {
            if (bars != null) _levels = new float[bars.Length];
            if (group != null) group.alpha = 0f;
            Apply(0f);
        }

        void Start()
        {
            // AudioDirector 的 AudioSource 是运行时建的，编辑器里拖不到，只能运行时找
            if (source == null && AudioDirector.Instance != null)
                source = AudioDirector.Instance.HumSource;
        }

        public void Show(float seconds)
        {
            _hideAt = Time.time + Mathf.Max(0.5f, seconds - 0.8f);
            if (group != null) group.alpha = 1f;
            if (source != null && !source.isPlaying) source.Play();
        }

        public void Hide()
        {
            _hideAt = -1f;
            if (group != null) group.alpha = 0f;
        }

        void Update()
        {
            if (_hideAt > 0f && Time.time > _hideAt)
            {
                if (group != null) group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.deltaTime * 2.5f);
                if (group != null && group.alpha <= 0.01f) _hideAt = -1f;
            }

            if (source == null || !source.isPlaying || _levels == null) return;

            source.GetSpectrumData(_samples, 0, FFTWindow.Blackman);

            int n = _levels.Length;
            if (_raw == null || _raw.Length != n) _raw = new float[n];

            // 只取低频到中频那一段（哼唱的能量都在这里）。
            // 全频谱会让所有柱子挤在左边第一二根上，看着像没反应。
            int hiBin = Mathf.Clamp(_samples.Length / 3, 8, _samples.Length - 2);

            float max = 1e-6f;
            for (int i = 0; i < n; i++)
            {
                float u0 = Mathf.Pow((float)i / n, 1.5f);
                float u1 = Mathf.Pow((float)(i + 1) / n, 1.5f);
                int a = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, hiBin, u0)), 1, _samples.Length - 2);
                int b = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, hiBin, u1)), a + 1, _samples.Length - 1);

                float sum = 0f;
                for (int k = a; k < b; k++) sum += _samples[k];
                float v = sum / Mathf.Max(1, b - a);

                _raw[i] = v;
                if (v > max) max = v;
            }

            // 先比出这一帧最响的那根，再按比例归一化 —— 直接乘灵敏度会全部削顶成一堵墙
            for (int i = 0; i < n; i++)
            {
                float norm = Mathf.Clamp01(_raw[i] / max);
                float target = Mathf.Pow(norm, 0.75f);

                // 上升要快，回落要慢，才像电平表
                _levels[i] = target > _levels[i]
                    ? Mathf.Lerp(_levels[i], target, 0.75f)
                    : Mathf.Max(target, _levels[i] - falloff * Time.deltaTime);
            }

            Apply(1f);
        }

        void Apply(float blend)
        {
            if (bars == null || _levels == null) return;
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i] == null) continue;
                bars[i].color = barColor;
                float h = Mathf.Lerp(minHeight, maxHeight, _levels[i] * blend);
                RectTransform rt = bars[i].rectTransform;
                Vector2 sz = rt.sizeDelta;
                sz.y = h;
                rt.sizeDelta = sz;
            }
        }
    }
}