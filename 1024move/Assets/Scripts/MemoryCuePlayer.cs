using System;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 记忆碎片播放器：环境音淡入 + 字幕打字机 + UI 特效标记 + 自动收尾。
    /// 策划只需要在 Inspector 里填：要播的音频、要打的字、停留时长，不用写代码。
    /// </summary>
    public class MemoryCuePlayer : MonoBehaviour
    {
        [Header("音频")]
        public AudioSource ambienceSource;
        public AudioClip ambienceClip;
        [Tooltip("淡入时长（秒）")]
        public float fadeIn = 0.8f;
        [Tooltip("淡出时长（秒）")]
        public float fadeOut = 0.9f;

        [Header("语音/人声（可选，比如妈妈喊吃饭）")]
        public AudioSource voiceSource;
        public AudioClip voiceClip;
        [Tooltip("环境音开始多少秒后播人声")]
        public float voiceDelay = 0.6f;

        [Header("字幕")]
        public Text subtitle;
        [Tooltip("逐字速度（字/秒）")]
        public float charsPerSecond = 18f;
        [TextArea(2, 6)] public string text = "";
        [Tooltip("字幕打完后的停留时间")]
        public float holdAfterText = 1.4f;

        [Header("UI 特效开关（挂对应的 CanvasGroup 即可，可留空）")]
        public CanvasGroup scanlineEffect;
        public CanvasGroup spectrumEffect;

        public event Action Finished;

        bool _playing;

        /// <summary>播一段记忆，播完触发 Finished。</summary>
        public void Play()
        {
            if (_playing) return;
            StopAllCoroutines();
            StartCoroutine(PlayRoutine());
        }

        public void Stop()
        {
            StopAllCoroutines();
            _playing = false;
            if (ambienceSource != null) ambienceSource.Stop();
            if (voiceSource != null) voiceSource.Stop();
            SetAlpha(scanlineEffect, 0f, true);
            SetAlpha(spectrumEffect, 0f, true);
            if (subtitle != null) subtitle.text = "";
        }

        System.Collections.IEnumerator PlayRoutine()
        {
            _playing = true;

            if (subtitle != null) subtitle.text = "";

            if (ambienceSource != null && ambienceClip != null)
            {
                ambienceSource.clip = ambienceClip;
                ambienceSource.loop = true;
                ambienceSource.volume = 0f;
                ambienceSource.Play();
                yield return Fade(ambienceSource, 0f, 1f, fadeIn);
            }

            SetAlpha(scanlineEffect, 1f, false);
            SetAlpha(spectrumEffect, 1f, false);

            if (voiceSource != null && voiceClip != null)
            {
                yield return new WaitForSeconds(voiceDelay);
                voiceSource.PlayOneShot(voiceClip);
            }

            if (subtitle != null && !string.IsNullOrEmpty(text))
            {
                subtitle.text = "";
                float acc = 0f;
                int shown = 0;
                while (shown < text.Length)
                {
                    acc += Time.deltaTime * charsPerSecond;
                    int want = Mathf.Min(text.Length, Mathf.FloorToInt(acc));
                    if (want != shown)
                    {
                        shown = want;
                        subtitle.text = text.Substring(0, shown);
                    }
                    yield return null;
                }
                subtitle.text = text;
            }

            yield return new WaitForSeconds(holdAfterText);

            SetAlpha(scanlineEffect, 0f, true);
            SetAlpha(spectrumEffect, 0f, true);

            if (ambienceSource != null)
                yield return Fade(ambienceSource, ambienceSource.volume, 0f, fadeOut);

            if (subtitle != null) subtitle.text = "";

            _playing = false;
            if (Finished != null) Finished();
        }

        System.Collections.IEnumerator Fade(AudioSource src, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            src.volume = to;
            if (to <= 0.001f) src.Stop();
        }

        void SetAlpha(CanvasGroup g, float target, bool instant)
        {
            if (g == null) return;
            if (instant) g.alpha = target;
            else StartCoroutine(FadeCanvas(g, g.alpha, target, 0.35f));
        }

        System.Collections.IEnumerator FadeCanvas(CanvasGroup g, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                g.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            g.alpha = to;
        }
    }
}