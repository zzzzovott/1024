using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 终局演出：黑幕 → 铁盒打开 → 房间转暖 → 便签浮现 → 【打包带走】→ 生成记忆凭证。
    /// 全部用协程串，策划改时长直接改 Inspector。
    /// </summary>
    public class FinaleController : MonoBehaviour
    {
        [Header("引用")]
        public CanvasGroup fade;
        public CanvasGroup notePanel;
        public Text noteText;
        public CanvasGroup buttonGroup;
        public Button collectButton;
        public CanvasGroup posterPanel;
        public RawImage posterShot;
        public Text posterCaption;
        public MoodLightingController mood;
        public AudioSource sfx;

        [Header("便签原文")]
        [TextArea(3, 6)]
        public string noteContent =
            "哪怕在这个城市只有 1KB 的立足之地，\n我也要存下 1GB 的梦想。";

        [Header("节奏（秒）")]
        public float fadeOutRoom = 1.2f;
        [Tooltip("黑幕最终不透明度：留一点，让暖光房间还看得见")]
        [Range(0.4f, 1f)] public float fadeTo = 0.78f;
        public float boxOpenPause = 0.8f;
        public float charsPerSecond = 12f;
        public float holdBeforeButton = 1.8f;

        public event Action Finished;

        bool _playing;

        public void Play()
        {
            if (_playing) return;
            _playing = true;
            StartCoroutine(Routine());
        }

        System.Collections.IEnumerator Routine()
        {
            // 1. 淡出到黑，同时灯光转暖
            if (notePanel != null) notePanel.alpha = 0f;
            if (buttonGroup != null) buttonGroup.alpha = 0f;
            if (posterPanel != null) posterPanel.alpha = 0f;
            if (noteText != null) noteText.text = "";

            if (mood != null) mood.ToWarm();

            float t = 0f;
            while (t < fadeOutRoom)
            {
                t += Time.deltaTime;
                if (fade != null) fade.alpha = Mathf.Lerp(0f, fadeTo, t / fadeOutRoom);
                yield return null;
            }
            if (fade != null) fade.alpha = fadeTo;

            yield return new WaitForSeconds(boxOpenPause);

            // 2. 打字机便签
            if (notePanel != null) notePanel.alpha = 1f;
            if (noteText != null)
            {
                string full = noteContent;
                float acc = 0f;
                int shown = 0;
                while (shown < full.Length)
                {
                    acc += Time.deltaTime * charsPerSecond;
                    int want = Mathf.Min(full.Length, Mathf.FloorToInt(acc));
                    if (want != shown)
                    {
                        shown = want;
                        noteText.text = full.Substring(0, shown);
                    }
                    yield return null;
                }
                noteText.text = full;
            }

            // 3. 按钮
            yield return new WaitForSeconds(holdBeforeButton);
            float k = 0f;
            while (k < 0.8f)
            {
                k += Time.deltaTime;
                if (buttonGroup != null) buttonGroup.alpha = Mathf.Clamp01(k / 0.8f);
                yield return null;
            }
            if (buttonGroup != null) buttonGroup.alpha = 1f;

            if (collectButton != null) collectButton.onClick.AddListener(OnCollect);

            if (Finished != null) Finished();
        }

        void OnCollect()
        {
            if (collectButton != null) collectButton.onClick.RemoveListener(OnCollect);
            StartCoroutine(CollectRoutine());
        }

        System.Collections.IEnumerator CollectRoutine()
        {
            string stamp = DateTime.Now.ToString("yyyy-MM-dd");
            string fileName = "Memory1024_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png";
            string path = Path.Combine(Application.persistentDataPath, fileName);

            // 先隐藏按钮，截一张干净的图
            if (buttonGroup != null) buttonGroup.alpha = 0f;
            yield return new WaitForEndOfFrame();

            try
            {
                ScreenCapture.CaptureScreenshot(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[1024] 截图失败：" + e.Message);
            }

            yield return new WaitForSeconds(0.25f);

            if (posterShot != null && File.Exists(path))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                    tex.LoadImage(bytes);
                    posterShot.texture = tex;
                    posterShot.color = new Color(1f, 1f, 1f, 0.55f);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[1024] 截图读取失败：" + e.Message);
                }
            }

            if (posterPanel != null) posterPanel.alpha = 1f;
            if (posterCaption != null)
                posterCaption.text = "记忆凭证 · NO.1024\n" + stamp + "\n\n「" + noteContent.Replace("\n", "") + "」";

            Debug.Log("[1024] 记忆凭证已保存：" + path);
        }

        /// <summary>给自动化测试用。</summary>
        public void ForceCollect()
        {
            OnCollect();
        }
    }
}