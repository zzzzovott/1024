using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 冷暖灯光反转：把"冰冷现实"和"温馨回忆"做成两套可插值的 Preset。
    /// 只用灯光 + 环境光 + 相机背景，不依赖后处理，2020.3 Built-in 直接可用。
    /// </summary>
    public class MoodLightingController : MonoBehaviour
    {
        [Serializable]
        public class MoodPreset
        {
            public Color ambient = Color.gray;
            public Color mainLight = Color.white;
            public float mainIntensity = 1f;
            public Color fillLight = Color.black;
            public float fillIntensity = 0.2f;
            public Color cameraBackground = Color.black;
            public float fogDensity = 0.01f;
        }

        [Header("灯光引用")]
        public Light mainLight;
        public Light fillLight;

        [Header("两套氛围")]
        public MoodPreset coldMood = new MoodPreset
        {
            ambient = new Color(0.16f, 0.17f, 0.19f),
            mainLight = new Color(0.72f, 0.78f, 0.90f),
            mainIntensity = 0.85f,
            fillLight = new Color(0.05f, 0.06f, 0.08f),
            fillIntensity = 0.0f,
            cameraBackground = new Color(0.05f, 0.05f, 0.06f),
            fogDensity = 0.02f
        };

        public MoodPreset warmMood = new MoodPreset
        {
            ambient = new Color(0.35f, 0.26f, 0.17f),
            mainLight = new Color(1f, 0.78f, 0.48f),
            mainIntensity = 1.45f,
            fillLight = new Color(1f, 0.62f, 0.35f),
            fillIntensity = 0.55f,
            cameraBackground = new Color(0.12f, 0.09f, 0.07f),
            fogDensity = 0.008f
        };

        [Header("过渡")]
        public float transitionDuration = 2.5f;

        [Header("状态")]
        [Range(0f, 1f)] public float blend;  // 0 = 冰冷，1 = 温馨

        Camera _cam;
        float _target;

        void Awake()
        {
            _cam = Camera.main;
        }

        void Start()
        {
            Apply(blend);
        }

        /// <summary>渐变到指定氛围（0 = 冰冷现实，1 = 温馨回忆）。</summary>
        public void BlendTo(float target, float duration = -1f)
        {
            _target = Mathf.Clamp01(target);
            StopAllCoroutines();
            StartCoroutine(BlendRoutine(_target, duration < 0f ? transitionDuration : duration));
        }

        public void ToCold() { BlendTo(0f); }
        public void ToWarm() { BlendTo(1f); }

        System.Collections.IEnumerator BlendRoutine(float target, float duration)
        {
            float from = blend;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                blend = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                Apply(blend);
                yield return null;
            }
            blend = target;
            Apply(blend);
        }

        void Apply(float k)
        {
            RenderSettings.ambientLight = Color.Lerp(coldMood.ambient, warmMood.ambient, k);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = Mathf.Lerp(coldMood.fogDensity, warmMood.fogDensity, k);
            RenderSettings.fogColor = Color.Lerp(coldMood.cameraBackground, warmMood.cameraBackground, k);

            if (mainLight != null)
            {
                mainLight.color = Color.Lerp(coldMood.mainLight, warmMood.mainLight, k);
                mainLight.intensity = Mathf.Lerp(coldMood.mainIntensity, warmMood.mainIntensity, k);
            }
            if (fillLight != null)
            {
                fillLight.enabled = true;
                fillLight.color = Color.Lerp(coldMood.fillLight, warmMood.fillLight, k);
                fillLight.intensity = Mathf.Lerp(coldMood.fillIntensity, warmMood.fillIntensity, k);
            }
            if (_cam != null && _cam.clearFlags == CameraClearFlags.SolidColor)
                _cam.backgroundColor = Color.Lerp(coldMood.cameraBackground, warmMood.cameraBackground, k);
        }
    }
}