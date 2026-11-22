using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 模块 3：老电视高频点击（设计稿 V3.0 第三部分）。
    ///
    /// 准星瞄着电视连点：1.5 秒内点满 6 下算"拍好了"。
    /// 每一下都有位移抖动（协程手写，不用 DoTween）；
    /// 拍好之后聚光灯从 0 拉到 lightOnIntensity，维持 4 秒再熄灭——
    /// 这 4 秒就是留给玩家去转青蛙、把影子对到墙上数字的窗口。
    ///
    /// 判定用**屏幕中心射线**，和准星保持一致：准星指着电视时点击才算数。
    /// </summary>
    public class TVInteraction : MonoBehaviour
    {
        [Header("连点判定")]
        [Tooltip("统计窗口（秒）：设计稿 1.5 秒")]
        public float clickWindow = 1.5f;
        [Tooltip("窗口内需要几下：设计稿 6 下")]
        public int clicksRequired = 6;
        [Tooltip("中心射线最远距离（米）")]
        public float maxRayDistance = 9f;

        [Header("拍打手感")]
        [Tooltip("每下抖动的位移幅度（米）")]
        public float shakeAmplitude = 0.02f;
        [Tooltip("单次抖动时长（秒）")]
        public float shakeDuration = 0.09f;

        [Header("灯光")]
        [Tooltip("照亮墙壁的聚光灯")]
        public Light spotLight;
        [Tooltip("点亮后的目标强度：设计稿 0 → 10")]
        public float lightOnIntensity = 10f;
        [Tooltip("从 0 拉到最亮的时间")]
        public float lightRampUp = 0.25f;
        [Tooltip("亮着多久：设计稿 4 秒")]
        public float lightHoldSeconds = 4f;
        [Tooltip("熄灭用的时间")]
        public float lightFadeOut = 0.6f;

        [Header("屏幕")]
        [Tooltip("雪花屏（可留空）")]
        public Renderer screenRenderer;
        public Color screenOnEmissive = new Color(0.75f, 0.82f, 1f) * 1.6f;

        [Header("声音")]
        public AudioDirector audio;

        [Header("距离")]
        [Tooltip("玩家（用位置算距离）。留空则不限制")]
        public Transform player;
        [Tooltip("要离多近才能拍：设计稿要求走到电视前，这里给 2 米")]
        public float maxPlayerDistance = 2.0f;

        [Header("提示")]
        public string hoverHint = "点击拍打";
        public string farHint = "走近一点再拍";

        /// <summary>灯现在是不是亮着（外部可以用来做"趁亮光赶紧转"的提示）。</summary>
        public bool IsLightOn { get; private set; }
        public int ClickCount { get { return _clicks.Count; } }
        public bool IsInRange
        {
            get
            {
                if (player == null) return true;
                return Vector3.Distance(player.position, transform.position) <= maxPlayerDistance;
            }
        }

        /// <summary>当前窗口内的点击数变化（当前, 需要）。</summary>
        public event Action<int, int> ClickCounted;
        /// <summary>拍成功、灯亮起。</summary>
        public event Action Fixed;
        /// <summary>4 秒到、灯灭了（没解开就要重新拍）。</summary>
        public event Action LightExpired;

        readonly List<float> _clicks = new List<float>(8);
        Vector3 _baseLocalPos;
        bool _shaking;
        Color _screenBaseEmissive;
        bool _screenBaseHasEmission;
        Material _screenMat;

        void Awake()
        {
            _baseLocalPos = transform.localPosition;

            if (screenRenderer != null)
            {
                _screenMat = screenRenderer.material;
                if (_screenMat != null && _screenMat.HasProperty("_EmissionColor"))
                {
                    _screenBaseHasEmission = true;
                    _screenBaseEmissive = _screenMat.GetColor("_EmissionColor");
                }
            }

            if (spotLight != null) spotLight.intensity = 0f;
        }

        void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (!IsInRange) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxRayDistance)) return;
            if (!hit.collider.transform.IsChildOf(transform)) return;

            RegisterClick();
        }

        /// <summary>记一次拍打（也便于自动化测试直接调）。</summary>
        public void RegisterClick()
        {
            float now = Time.time;

            // 丢掉窗口外的旧点击
            for (int i = _clicks.Count - 1; i >= 0; i--)
                if (now - _clicks[i] > clickWindow) _clicks.RemoveAt(i);

            _clicks.Add(now);

            if (audio != null) audio.PlayKnock();
            if (!_shaking) StartCoroutine(ShakeRoutine());

            if (ClickCounted != null) ClickCounted(_clicks.Count, clicksRequired);

            if (_clicks.Count >= clicksRequired && !IsLightOn) TriggerFixed();
        }

        IEnumerator ShakeRoutine()
        {
            _shaking = true;

            float t = 0f;
            while (t < shakeDuration)
            {
                t += Time.deltaTime;
                float k = 1f - t / shakeDuration;
                Vector3 shake = new Vector3(
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f),
                    UnityEngine.Random.Range(-1f, 1f)) * shakeAmplitude * k;
                transform.localPosition = _baseLocalPos + shake;
                yield return null;
            }

            transform.localPosition = _baseLocalPos;
            _shaking = false;
        }

        /// <summary>拍成功：点亮。</summary>
        public void TriggerFixed()
        {
            if (IsLightOn) return;
            IsLightOn = true;
            _clicks.Clear();

            if (Fixed != null) Fixed();
            StartCoroutine(LightRoutine());
        }

        IEnumerator LightRoutine()
        {
            float from = spotLight != null ? spotLight.intensity : 0f;

            if (screenMatValid) SetScreenEmission(screenOnEmissive);

            float t = 0f;
            while (t < lightRampUp && spotLight != null)
            {
                t += Time.deltaTime;
                spotLight.intensity = Mathf.Lerp(from, lightOnIntensity, t / lightRampUp);
                yield return null;
            }
            if (spotLight != null) spotLight.intensity = lightOnIntensity;

            yield return new WaitForSeconds(lightHoldSeconds);

            t = 0f;
            float start = spotLight != null ? spotLight.intensity : 0f;
            while (t < lightFadeOut && spotLight != null)
            {
                t += Time.deltaTime;
                spotLight.intensity = Mathf.Lerp(start, 0f, t / lightFadeOut);
                yield return null;
            }
            if (spotLight != null) spotLight.intensity = 0f;

            if (screenMatValid) SetScreenEmission(_screenBaseEmissive);

            IsLightOn = false;
            if (LightExpired != null) LightExpired();
        }

        bool screenMatValid { get { return _screenMat != null && _screenBaseHasEmission; } }

        void SetScreenEmission(Color color)
        {
            if (_screenMat == null) return;
            _screenMat.SetColor("_EmissionColor", color);
            if (color.maxColorComponent > 0.01f) _screenMat.EnableKeyword("_EMISSION");
            else _screenMat.DisableKeyword("_EMISSION");
        }

        /// <summary>给自动化测试用：直接拍好。</summary>
        public void ForceFix()
        {
            TriggerFixed();
        }

        /// <summary>重置电视（重新拍）。</summary>
        public void ResetTv()
        {
            StopAllCoroutines();
            _clicks.Clear();
            _shaking = false;
            IsLightOn = false;
            transform.localPosition = _baseLocalPos;
            if (spotLight != null) spotLight.intensity = 0f;
            if (screenMatValid) SetScreenEmission(_screenBaseEmissive);
        }
    }
}
