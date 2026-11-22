using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 准星系统（设计稿 V3.0 第一部分）。
    ///
    /// 固定钉在屏幕正中心：默认是一枚极小白点；主相机从屏幕中心发 Physics.Raycast，
    /// 命中交互物时白点变成【小手】（有手形切图就用手形，没有则放大圆环代替）并显示操作提示；
    /// 移开立刻恢复白点。
    ///
    /// 和旧版的区别：旧版准星跟着鼠标跑，那是为了让 2.5D 点击好点中；
    /// 但本作是固定机位，鼠标真正需要负责的是"滑动擦灰 / 拖拽旋转"这类手势，
    /// 瞄准交给屏幕中心反而更稳、更像第一人称的"看"。
    /// </summary>
    public class CrosshairUI : MonoBehaviour
    {
        [Header("外观")]
        [Tooltip("手形图标（郑子涵的切图）。留空时就只放大圆环")]
        public Sprite handSprite;
        public Color idleColor = new Color(1f, 1f, 1f, 0.8f);
        public Color hoverColor = new Color(1f, 0.95f, 0.86f, 1f);
        [Tooltip("描边色：画在圆环外面一圈，浅色背景也能看清")]
        public Color outlineColor = new Color(0f, 0f, 0f, 0.55f);
        public float idleSize = 30f;
        public float hoverSize = 62f;
        [Tooltip("中心小点的直径比例（相对 idleSize）")]
        [Range(0f, 0.6f)] public float dotRatio = 0.30f;
        [Tooltip("悬停时圆环的呼吸幅度（0 = 不呼吸）")]
        public float pulseAmount = 0.05f;
        public float followLerp = 18f;

        [Header("射线")]
        [Tooltip("中心射线最远检测距离（米）")]
        public float maxDistance = 12f;
        [Tooltip("球扫描半径：固定机位下物件在屏幕上很小，纯射线太难瞄，给一点宽容度")]
        public float castRadius = 0.25f;
        public LayerMask rayMask = ~0;
        [Tooltip("可选：给交互物打上这个标签，即使没有专用脚本也能识别；留空则只认脚本组件")]
        public string interactableTag = "Interactable";

        [Header("提示文字")]
        public float labelFontSize = 20;
        public Vector2 labelOffset = new Vector2(0f, -38f);
        public float labelWidth = 520f;

        [Header("系统光标")]
        public bool hideSystemCursor = true;

        /// <summary>当前中心射线命中的、可实现"点击查看"的旧物（Explore 阶段用）。</summary>
        public DeskFocusInteractable HoveredInteractable { get; private set; }
        /// <summary>当前中心射线命中的交互碰撞体：旧物 / 灰尘 / 电视 / 合同 Hitbox 都可能。</summary>
        public Collider HoveredCollider { get; private set; }
        public bool IsHovering { get { return HoveredCollider != null; } }

        /// <summary>当前场景里的准星（第一人称只有一个），给"全局提示"用。</summary>
        public static CrosshairUI Active { get; private set; }

        /// <summary>
        /// 全局提示：没有悬停物体时也会显示（比如抱着箱子时的 [点击 放下箱子]）。
        /// 每帧由对应系统覆盖写，不需要时传空串即可。
        /// </summary>
        public void SetGlobalPrompt(string text)
        {
            _globalPrompt = text;
        }

        string _globalPrompt;

        Image _ring;
        Image _halo;
        Image _dot;
        Image _hand;
        Text _label;
        RectTransform _rt;
        float _size;
        bool _hover;
        string _labelText = "";
        string _overrideLabel;
        bool _hasOverride;

        void Awake()
        {
            Active = this;
            _rt = GetComponent<RectTransform>();
            if (_rt == null)
            {
                gameObject.AddComponent<RectTransform>();
                _rt = GetComponent<RectTransform>();
            }

            // 钉死在画布中心：锚点、轴心、偏移全部归零，不跟随鼠标
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.anchoredPosition = Vector2.zero;
            _rt.sizeDelta = Vector2.zero;
            _rt.localScale = Vector3.one;

            // 描边环（黑色，压在白色圆环下面一圈，浅色背景也能看清）
            GameObject haloGo = new GameObject("Halo", typeof(RectTransform));
            haloGo.transform.SetParent(transform, false);
            RectTransform haloRt = haloGo.GetComponent<RectTransform>();
            haloRt.anchorMin = haloRt.anchorMax = new Vector2(0.5f, 0.5f);
            haloRt.anchoredPosition = Vector2.zero;
            haloRt.sizeDelta = new Vector2(idleSize * 1.35f, idleSize * 1.35f);
            _halo = haloGo.AddComponent<Image>();
            _halo.sprite = MakeRingSprite(0.50f, 1.00f, 0.10f);
            _halo.color = outlineColor;
            _halo.raycastTarget = false;

            // 主圆环（白）
            GameObject ringGo = new GameObject("Ring", typeof(RectTransform));
            ringGo.transform.SetParent(transform, false);
            RectTransform ringRt = ringGo.GetComponent<RectTransform>();
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.anchoredPosition = Vector2.zero;
            ringRt.sizeDelta = new Vector2(idleSize, idleSize);
            _ring = ringGo.AddComponent<Image>();
            _ring.sprite = MakeRingSprite(0.58f, 0.98f, 0.07f);
            _ring.color = idleColor;
            _ring.raycastTarget = false;
            _size = idleSize;

            // 中心小点：远看是一个明确的"瞄准点"
            GameObject dotGo = new GameObject("Dot", typeof(RectTransform));
            dotGo.transform.SetParent(transform, false);
            RectTransform dotRt = dotGo.GetComponent<RectTransform>();
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.anchoredPosition = Vector2.zero;
            dotRt.sizeDelta = new Vector2(idleSize * dotRatio, idleSize * dotRatio);
            _dot = dotGo.AddComponent<Image>();
            _dot.sprite = MakeCircleSprite();
            _dot.color = idleColor;
            _dot.raycastTarget = false;

            // 手形图标（没有素材就先空着，悬停时退化成"放大圆环"）
            GameObject handGo = new GameObject("Hand", typeof(RectTransform));
            handGo.transform.SetParent(transform, false);
            RectTransform handRt = handGo.GetComponent<RectTransform>();
            handRt.anchorMin = handRt.anchorMax = new Vector2(0.5f, 0.5f);
            handRt.anchoredPosition = Vector2.zero;
            handRt.sizeDelta = new Vector2(hoverSize, hoverSize);
            _hand = handGo.AddComponent<Image>();
            _hand.raycastTarget = false;
            _hand.enabled = false;
            if (handSprite != null) _hand.sprite = handSprite;

            // 提示文字（挂在中心偏下）
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(transform, false);
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = labelOffset;
            labelRt.sizeDelta = new Vector2(labelWidth, 40f);

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

            SetLook(false, "");
        }

        void Start()
        {
            if (hideSystemCursor) Cursor.visible = false;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            Cursor.visible = true;
        }

        void Update()
        {
            if (_rt == null) return;

            // 再钉一次，防止别的脚本或布局把准星挪走
            _rt.anchoredPosition = Vector2.zero;

            Scan();

            bool hover = _hover;
            float want = hover ? hoverSize : idleSize;
            if (hover && pulseAmount > 0f) want += Mathf.Sin(Time.time * 6f) * idleSize * pulseAmount;

            _size = Mathf.Lerp(_size, want, 1f - Mathf.Exp(-followLerp * Time.deltaTime));

            Color c = hover ? hoverColor : idleColor;
            if (_ring != null)
            {
                _ring.rectTransform.sizeDelta = new Vector2(_size, _size);
                _ring.color = c;
            }

            bool showHand = hover && _hand.sprite != null;

            if (_halo != null)
            {
                _halo.rectTransform.sizeDelta = new Vector2(_size * 1.35f, _size * 1.35f);
                _halo.color = hover ? new Color(0f, 0f, 0f, 0.7f) : outlineColor;
                _halo.enabled = !showHand;
            }

            if (_dot != null)
            {
                float d = _size * dotRatio;
                _dot.rectTransform.sizeDelta = new Vector2(d, d);
                _dot.color = c;
                _dot.enabled = !showHand;
            }
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
                bool show = !string.IsNullOrEmpty(_labelText);
                _label.enabled = show;
                if (show) _label.text = _labelText;
            }
        }

        /// <summary>从屏幕中心发一条射线，判定当前瞄准的东西。</summary>
        void Scan()
        {
            HoveredInteractable = null;
            HoveredCollider = null;
            string hint = null;

            Camera cam = Camera.main;
            if (cam == null)
            {
                SetLook(false, "");
                return;
            }

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            bool got = castRadius > 0f
                ? Physics.SphereCast(ray, castRadius, out hit, maxDistance, rayMask, QueryTriggerInteraction.Collide)
                : Physics.Raycast(ray, out hit, maxDistance, rayMask, QueryTriggerInteraction.Collide);
            if (got)
            {
                Collider col = hit.collider;

                DeskFocusInteractable it = col.GetComponentInParent<DeskFocusInteractable>();
                if (it != null)
                {
                    HoveredInteractable = it;
                    // 参考传统单机：按键提示写在准星下方
                    hint = "[F] 拾取";
                    HoveredCollider = col;
                }
                else
                {
                    DustErase dust = col.GetComponentInParent<DustErase>();
                    if (dust != null && dust.IsActive)
                    {
                        hint = !dust.HasRag
                            ? "需要一块抹布"
                            : (dust.IsInRange ? dust.hoverHint : dust.farHint);
                        HoveredCollider = col;
                    }
                    else
                    {
                        TVInteraction tv = col.GetComponentInParent<TVInteraction>();
                        if (tv != null)
                        {
                            hint = tv.IsInRange ? tv.hoverHint : tv.farHint;
                            HoveredCollider = col;
                        }
                        else
                        {
                            RagPickup rag = col.GetComponentInParent<RagPickup>();
                            if (rag != null)
                            {
                                hint = rag.IsHeld ? rag.dropHint : rag.hoverHint;
                                HoveredCollider = col;
                            }
                            else
                            {
                                // 通用提示源：杂物 / 旧衣服 / 铁盒 等都挂这个
                                PromptSource source = col.GetComponentInParent<PromptSource>();
                                if (source != null && !string.IsNullOrEmpty(source.hint))
                                {
                                    hint = source.hint;
                                    HoveredCollider = col;
                                }
                                else if (HasInteractableTag(col))
                                {
                                    hint = "点击查看";
                                    HoveredCollider = col;
                                }
                            }
                        }
                    }
                }
            }

            if (_hasOverride && !string.IsNullOrEmpty(_overrideLabel))
            {
                SetLook(true, _overrideLabel);
                return;
            }

            if (string.IsNullOrEmpty(hint) && !string.IsNullOrEmpty(_globalPrompt))
            {
                SetLook(false, _globalPrompt);
                return;
            }

            SetLook(HoveredCollider != null, hint ?? "");
        }

        bool HasInteractableTag(Collider col)
        {
            if (string.IsNullOrEmpty(interactableTag)) return false;
            // 用字符串比较而不是 CompareTag：项目里没定义这个标签时 CompareTag 会抛异常并刷错误
            return col.tag == interactableTag;
        }

        void SetLook(bool hover, string text)
        {
            _hover = hover;
            _labelText = text;
        }

        /// <summary>
        /// 临时覆盖提示文字（比如桌面特写里显示"按住左键画圈"）。
        /// 传 null 或空串即取消覆盖，回到自动判定的文字。
        /// </summary>
        public void SetOverrideLabel(string text)
        {
            _hasOverride = !string.IsNullOrEmpty(text);
            _overrideLabel = text;
        }

        /// <summary>用代码画一枚圆环，省掉一张切图。inner/outer 是半径比例，soft 是边缘柔和度。</summary>
        static Sprite MakeRingSprite(float inner, float outer, float soft)
        {
            const int size = 128;
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

                    float a = Mathf.Clamp01((outer - d) / soft) * Mathf.Clamp01((d - inner) / soft);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>中心小点用的实心圆。</summary>
        static Sprite MakeCircleSprite()
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
                    float a = Mathf.Clamp01((0.92f - d) / 0.18f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
