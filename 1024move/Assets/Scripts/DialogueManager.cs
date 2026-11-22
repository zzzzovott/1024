using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 全局唯一对话管理器 —— 所有打字机文本都必须经过它。
    ///
    /// 规则:
    /// 1. 严格队列:同一时刻只有一条文本在播,后来的全部排队,绝不插队/叠加;
    /// 2. 状态锁:播放期间再 Say 只会入队,不会打断;
    /// 3. 自动收尾:每句播完(逐字 + 停留 + 淡出)才播下一句,间隔 0.25s 防止贴在一起;
    /// 4. 强制清屏:状态机推进时调用 ClearNow(),当前句立刻消失、队列直接丢弃。
    /// </summary>
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("引用")]
        public InnerMonologue monologue;

        [Header("节奏")]
        [Tooltip("两句之间额外的间隔（秒）")]
        public float gapBetweenLines = 0.25f;

        readonly Queue<string> _queue = new Queue<string>();
        bool _playing;
        Coroutine _routine;

        public bool IsBusy { get { return _playing; } }
        public int PendingCount { get { return _queue.Count; } }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>全局入口：排队说一句。</summary>
        public static void Say(string text)
        {
            if (Instance == null) return;
            Instance.Enqueue(text);
        }

        public void Enqueue(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            _queue.Enqueue(text);
            if (!_playing) _routine = StartCoroutine(Run());
        }

        /// <summary>状态推进时强制清屏：停掉当前句 + 丢弃排队。</summary>
        public void ClearNow()
        {
            _queue.Clear();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _playing = false;

            if (monologue != null) monologue.Clear();
        }

        IEnumerator Run()
        {
            _playing = true;

            while (_queue.Count > 0)
            {
                string line = _queue.Dequeue();
                if (monologue == null) break;

                monologue.SayNow(line);

                // 等这句彻底播完（InnerMonologue 自己负责逐字 + 停留 + 淡出）
                while (monologue.IsPlaying) yield return null;

                if (_queue.Count > 0 && gapBetweenLines > 0f)
                    yield return new WaitForSeconds(gapBetweenLines);
            }

            _playing = false;
            _routine = null;
        }
    }
}
