using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 内心 OS：屏幕下方的打字机独白。
    ///
    /// 设计稿要求"无背景框、手写体、Alpha 0.8、逐字浮现 + 键盘音"。
    /// 字体这里用系统字体兜底（手写体会在打包时丢字形），
    /// 郑子涵给了字体文件后，把 font 换掉就行。
    /// </summary>
    public class InnerMonologue : MonoBehaviour
    {
        [Header("引用")]
        public Text target;
        public CanvasGroup group;
        public Font font;

        [Header("节奏")]
        [Tooltip("每秒打几个字")]
        public float charsPerSecond = 20f;
        [Tooltip("一句话打完停留多久")]
        public float holdSeconds = 2.4f;
        [Tooltip("淡入淡出时长")]
        public float fadeSeconds = 0.22f;
        [Tooltip("每几个字响一次键盘音")]
        public int clicksPerChars = 3;

        [Header("外观")]
        [Range(0f, 1f)] public float alpha = 0.8f;

        readonly Queue<string> _queue = new Queue<string>();
        bool _running;
        Coroutine _co;

        void Awake()
        {
            if (group == null && target != null) group = target.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 0f;
            if (target != null)
            {
                if (font != null) target.font = font;
                target.color = new Color(target.color.r, target.color.g, target.color.b, alpha);
                target.text = "";
            }
        }

        /// <summary>说一句。默认排队，不打断。</summary>
        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _queue.Enqueue(text);
            if (!_running) _co = StartCoroutine(Run());
        }

        /// <summary>打断当前这句，立刻说新的。</summary>
        public void SayNow(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _queue.Clear();
            _queue.Enqueue(text);
            if (_co != null) StopCoroutine(_co);
            _running = false;
            _co = StartCoroutine(Run());
        }

        public void Clear()
        {
            _queue.Clear();
            if (_co != null) StopCoroutine(_co);
            _running = false;
            if (target != null) target.text = "";
            if (group != null) group.alpha = 0f;
        }

        IEnumerator Run()
        {
            _running = true;
            while (_queue.Count > 0)
            {
                string line = _queue.Dequeue();
                if (target != null) target.text = "";

                // 淡入
                float t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.deltaTime;
                    if (group != null) group.alpha = Mathf.Lerp(0f, alpha, t / fadeSeconds);
                    yield return null;
                }
                if (group != null) group.alpha = alpha;

                // 逐字
                float acc = 0f;
                int shown = 0;
                while (shown < line.Length)
                {
                    acc += Time.deltaTime * charsPerSecond;
                    int want = Mathf.Min(line.Length, Mathf.FloorToInt(acc));
                    if (want != shown)
                    {
                        if (AudioDirector.Instance != null && clicksPerChars > 0 &&
                            want % clicksPerChars == 0)
                            AudioDirector.Instance.PlayType();

                        shown = want;
                        if (target != null) target.text = line.Substring(0, shown);
                    }
                    yield return null;
                }
                if (target != null) target.text = line;

                yield return new WaitForSeconds(holdSeconds);

                // 淡出
                t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.deltaTime;
                    if (group != null) group.alpha = Mathf.Lerp(alpha, 0f, t / fadeSeconds);
                    yield return null;
                }
                if (group != null) group.alpha = 0f;
                if (target != null) target.text = "";
            }
            _running = false;
        }
    }
}