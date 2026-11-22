using System.Collections;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 开场序列（设计稿 V3.0 序章第 2 步）：
    /// 黑屏 → 撕胶带声 → 打字机"毕业五年，第1024次打包。" → 黑幕淡出 → 交还操作。
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        public CanvasGroup black;
        public InnerMonologue monologue;
        public Player25DController player;

        [Header("文本")]
        [TextArea(2, 4)]
        public string line = "毕业五年，第1024次打包。";

        [Header("节奏（秒）")]
        public float blackHold = 1.4f;
        public float fadeOut = 1.6f;
        [Tooltip("淡出后等多久再交还操作")]
        public float settle = 0.3f;

        [Header("启动方式")]
        [Tooltip("勾上=进场景立刻播；关掉=等主菜单调用 Begin()（配 MainMenu 用）")]
        public bool autoStart = true;

        public bool Enabled = true;

        bool _done;
        bool _started;

        void Start()
        {
            // 自动截图 / 冒烟测试时跳过开场，免得截到黑屏
            if (!Enabled || HasArg("-shot") || HasArg("-smoke"))
            {
                if (black != null)
                {
                    black.alpha = 0f;
                    black.blocksRaycasts = false;
                }
                _done = true;
                return;
            }

            if (autoStart)
            {
                Begin();
            }
            else
            {
                // 主菜单会盖在上面；先把黑幕拉满，等 Begin() 再演，避免菜单淡出时露出游戏画面
                if (black != null)
                {
                    black.alpha = 1f;
                    black.blocksRaycasts = true;
                }
            }
        }

        /// <summary>开始播开场（主菜单点击后调用）。重复调用无效。</summary>
        public void Begin()
        {
            if (_started || _done) return;
            _started = true;
            StartCoroutine(Routine());
        }

        static bool HasArg(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        IEnumerator Routine()
        {
            if (black != null)
            {
                black.alpha = 1f;
                black.blocksRaycasts = true;
            }
            if (player != null) player.SetInputEnabled(false);

            yield return new WaitForSeconds(0.35f);

            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayTapeRip();

            if (monologue != null) monologue.Say(line);

            yield return new WaitForSeconds(blackHold);

            float t = 0f;
            while (t < fadeOut)
            {
                t += Time.deltaTime;
                if (black != null) black.alpha = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t / fadeOut));
                yield return null;
            }
            if (black != null)
            {
                black.alpha = 0f;
                black.blocksRaycasts = false;
            }

            yield return new WaitForSeconds(settle);

            if (player != null) player.SetInputEnabled(true);
            _done = true;
        }

        public bool IsDone { get { return _done; } }
    }
}