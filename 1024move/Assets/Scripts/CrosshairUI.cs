using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 准星（跟随鼠标）。
    ///
    /// 设计稿写的是"屏幕中心白点"，但本作是 2.5D 固定机位 + 鼠标点击，
    /// 屏幕中心的准星和鼠标会打架，所以改成**跟着鼠标走**：
    /// 平时是一枚小圆环，指到可交互的东西上变成小手 + 文字提示。
    /// 郑子涵做好手形图标后，直接拖进 handSprite 即可替换。
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("外观")]
        public Sprite handSprite;
        public Color idleColor = new Color(1f, 1f, 1f, 0.55f);
        public Color hoverColor = new Color(1f, 0.95f, 0.86f, 0.95f);
        public float idleSize = 9f;
        public float hoverSize = 26f;
        public float followLerp = 28f;

        [Header("提示文字")]
        public float labelFontSize = 20;
        public Vector2 labelOffset = new Vector2(0f, -34f);

        Image _ring;
        Image _hand;
        Text _label;
        RectTransform _rt;
        RectTransform _labelRt;
        float _size;
        bool _hover;
        string _hoverLabel = "";

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (_rt == null)
            {
                gameObject.AddComponent<RectTransform>();
                _rt = GetComponent<RectTransform>();
            }
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = Vector2.zero;

            // 圆环
            GameObject ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(transform, false);
            RectTransform ringRt = ringGo.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(idleSize, idleSize);
            _ring = ringGo.AddComponent<Image>();
            _ring.sprite = MakeRingSprite();
            _ring.color = idleColor;
            _ring.raycastTarget = false;
            _size = idleSize;

            // 手形图标（没有素材就先空着）
            GameObject handGo = new GameObject("Hand", typeof(RectTransform));
            handGo.transform.SetParent(transform, false);
            RectTransform handRt = handGo.GetComponent<RectTransform>();
            handRt.anchorMin = handRt.anchorMax = new Vector2(0.5f, 0.5f);
            handRt.sizeDelta = new Vector2(hoverSize, hoverSize);
            _hand = handGo.AddComponent<Image>();
            _hand.raycastTarget = false;
            _hand.enabled = false;
            if (handSprite != null) _hand.sprite = handSprite;

            // 提示文字
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            _labelRt = labelGo.GetComponent<RectTransform>();
            _labelRt.anchorMin = _labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            _labelRt.pivot = new Vector2(0.5f, 1f);
            _labelRt.anchoredPosition = labelOffset;
            _labelRt.sizeDelta = new Vector2(420f, 40f);

            _label = labelGo.AddComponent<Text>();
            _label.font = RuntimeFont.Font;
            _label.fontSize = (int)labelFontSize;
            _label.alignment = TextAnchor.UpperCenter;
            _label.color = hoverColor;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;

            Shadow sh = labelGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);

            SetHover(false, "");
        }

        void Start()
        {
            Cursor.visible = false;
        }

        void OnDestroy()
        {
            Cursor.visible = true;
        }

        void Update()
        {
            if (_rt == null) return;
            _rt.position = Vector3.Lerp(_rt.position, Input.mousePosition, 1f - Mathf.Exp(-followLerp * Time.deltaTime));

            float want = _hover ? hoverSize : idleSize;
            _size = Mathf.Lerp(_size, want, 1f - Mathf.Exp(-16f * Time.deltaTime));

            Color c = _hover ? hoverColor : idleColor;
            if (_ring != null)
            {
                _ring.rectTransform.sizeDelta = new Vector2(_size, _size);
                _ring.color = c;
            }

            bool showHand = _hover && _hand.sprite != null;
            if (_hand != null)
            {
                _hand.enabled = showHand;
                if (showHand)
                {
                    _hand.rectTransform.sizeDelta = new Vector2(hoverSize, hoverSize);
                    _hand.color = c;
                }
            }
            if (_ring != null) _ring.enabled = !showHand;

            if (_label != null)
            {
                bool show = _hover && !string.IsNullOrEmpty(_hoverLabel);
                _label.enabled = show;
                if (show) _label.text = _hoverLabel;
            }
        }

        public void SetHover(bool on, string label)
        {
            _hover = on;
            _hoverLabel = label;
        }

        /// <summary>用代码画一枚圆环，省掉一张切图。</summary>
        static Sprite MakeRingSprite()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01((0.96f - d) / 0.12f) * Mathf.Clamp01((d - 0.52f) / 0.12f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}