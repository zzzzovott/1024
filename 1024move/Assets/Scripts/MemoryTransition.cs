using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 记忆穿梭过渡：白光过曝 → 耳鸣 → 换场地 → 白光淡出。
    ///
    /// 设计稿里最关键的一条"原来是在记忆里"的表达方式。
    /// 实现上是纯 UI 白幕 + 一段合成的高频耳鸣，不需要任何美术资源。
    /// </summary>
    public class MemoryTransition : MonoBehaviour
    {
        [Header("白幕")]
        public CanvasGroup flash;
        public Image flashImage;

        [Header("节奏（秒）")]
        public float whiteIn = 0.16f;
        public float hold = 0.30f;
        public float whiteOut = 0.50f;

        [Header("颜色")]
        public Color color = Color.white;

        bool _playing;

        public bool IsPlaying { get { return _playing; } }

        void Awake()
        {
            if (flash != null) flash.alpha = 0f;
            if (flashImage != null) flashImage.color = color;
            if (flash != null) flash.blocksRaycasts = false;
        }

        /// <summary>
        /// 播一次穿梭。atWhite 会在最白的那一帧被调用 —— 换镜头、换场景都放这里。
        /// </summary>
        public void Play(Action atWhite)
        {
            if (_playing)
            {
                if (atWhite != null) atWhite();
                return;
            }
            StartCoroutine(Routine(atWhite));
        }

        IEnumerator Routine(Action atWhite)
        {
            _playing = true;

            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayTinnitus();

            float t = 0f;
            while (t < whiteIn)
            {
                t += Time.deltaTime;
                if (flash != null) flash.alpha = Mathf.Lerp(0f, 1f, t / whiteIn);
                yield return null;
            }
            if (flash != null) flash.alpha = 1f;

            // 全白的一瞬间做切换，玩家看不到接缝
            if (atWhite != null) atWhite();

            yield return new WaitForSeconds(hold);

            t = 0f;
            while (t < whiteOut)
            {
                t += Time.deltaTime;
                if (flash != null) flash.alpha = Mathf.Lerp(1f, 0f, t / whiteOut);
                yield return null;
            }
            if (flash != null) flash.alpha = 0f;

            _playing = false;
        }
    }
}