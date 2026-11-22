using System;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 第四个数字：擦掉"退租合同"上的灰，露出合同编号尾数 4。
    ///
    /// 实现方式：拿一张程序生成的"灰尘"贴图铺满面板，按住左键拖动时把笔刷区域的 alpha 抹掉，
    /// 抹掉面积超过阈值就判定"看清了"。比"点击一下弹出数字"有仪式感，也不增加多少代码量。
    /// </summary>
    public class ContractScratchUI : MonoBehaviour
    {
        [Header("面板")]
        public CanvasGroup panel;
        public RawImage dustLayer;
        public Text revealedText;
        public Text hintText;

        [Header("擦除参数")]
        [Tooltip("笔刷半径（像素，贴图坐标系）")]
        public float brushRadius = 22f;
        [Tooltip("擦掉多少比例算看清")]
        [Range(0.1f, 0.95f)] public float revealThreshold = 0.42f;
        [Tooltip("灰尘贴图分辨率，256 足够")]
        public int textureSize = 256;

        public event Action Revealed;

        Texture2D _tex;
        Color32[] _pixels;
        bool _open;
        bool _revealed;
        int _erased;
        int _totalPixels;
        float _checkTimer;

        void Awake()
        {
            if (panel != null) panel.alpha = 0f;
        }

        public void Open()
        {
            if (_revealed) return;
            _open = true;
            if (panel != null) panel.alpha = 1f;

            if (_tex == null) BuildDust();

            if (hintText != null) hintText.text = "按住左键，擦掉上面的灰";
            if (revealedText != null) revealedText.text = "";
        }

        public void Close()
        {
            _open = false;
            if (panel != null) panel.alpha = 0f;
        }

        void BuildDust()
        {
            _tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.filterMode = FilterMode.Bilinear;

            _pixels = new Color32[textureSize * textureSize];
            System.Random rng = new System.Random(20260101);

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    // 灰底 + 噪点 + 几道横向纤维，看着像落了很久的灰
                    float n = (float)rng.NextDouble();
                    float fiber = Mathf.PerlinNoise(x * 0.06f, y * 0.6f);
                    float v = 0.62f + n * 0.16f + fiber * 0.14f;
                    byte c = (byte)Mathf.Clamp(v * 255f, 0f, 255f);
                    _pixels[y * textureSize + x] = new Color32((byte)(c * 0.96f), (byte)(c * 0.93f), (byte)(c * 0.88f), 255);
                }
            }

            _totalPixels = _pixels.Length;
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);

            if (dustLayer != null) dustLayer.texture = _tex;
        }

        void Update()
        {
            if (!_open || _revealed) return;

            if (Input.GetMouseButton(0))
                TryErase(Input.mousePosition);

            _checkTimer += Time.deltaTime;
            if (_checkTimer > 0.15f)
            {
                _checkTimer = 0f;
                if ((float)_erased / _totalPixels >= revealThreshold) Complete();
            }
        }

        void TryErase(Vector2 screenPos)
        {
            if (dustLayer == null || _pixels == null) return;

            RectTransform rt = dustLayer.rectTransform;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, screenPos, null, out local))
                return;

            Rect r = rt.rect;
            float u = (local.x - r.xMin) / r.width;
            float v = (local.y - r.yMin) / r.height;
            if (u < 0f || u > 1f || v < 0f || v > 1f) return;

            int cx = Mathf.RoundToInt(u * textureSize);
            int cy = Mathf.RoundToInt(v * textureSize);
            int rad = Mathf.Max(2, Mathf.RoundToInt(brushRadius));
            int radSq = rad * rad;

            int minX = Mathf.Max(0, cx - rad);
            int maxX = Mathf.Min(textureSize - 1, cx + rad);
            int minY = Mathf.Max(0, cy - rad);
            int maxY = Mathf.Min(textureSize - 1, cy + rad);

            bool dirty = false;
            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - cy;
                int row = y * textureSize;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy * dy > radSq) continue;
                    if (_pixels[row + x].a == 0) continue;
                    Color32 c = _pixels[row + x];
                    c.a = 0;
                    _pixels[row + x] = c;
                    _erased++;
                    dirty = true;
                }
            }

            if (dirty)
            {
                _tex.SetPixels32(_pixels);
                _tex.Apply(false);
            }
        }

        void Complete()
        {
            if (_revealed) return;
            _revealed = true;

            if (dustLayer != null) dustLayer.color = new Color(1f, 1f, 1f, 0.08f);
            if (revealedText != null) revealedText.text = "合同编号：HR-2019-0731-XXX4";
            if (hintText != null) hintText.text = "……最后一位是 4。";

            StartCoroutine(FinishLater());
        }

        System.Collections.IEnumerator FinishLater()
        {
            yield return new WaitForSeconds(1.6f);
            Close();
            if (Revealed != null) Revealed();
        }

        /// <summary>给自动化测试用：直接擦完。</summary>
        public void ForceReveal()
        {
            if (_tex == null) BuildDust();
            _erased = _totalPixels;
            Complete();
        }
    }
}