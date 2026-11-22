using System;
using System.Collections;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 模块 2：桌面擦灰（设计稿 V3.0 序章第 5 步）。
    ///
    /// 做法：给桌面铺一块"灰"Quad（MeshCollider 才能取到 UV），
    /// 按住左键在桌面上滑动 → 从鼠标发 Physics.Raycast → hit.textureCoord 换算成贴图像素 →
    /// 用 Texture2D.SetPixels32 把笔刷范围内的 Alpha 抹成 0。
    /// 擦掉 clearThreshold（默认 80%）后：播"灰尘消散"音效、灰尘淡出、露出下面的旧物。
    ///
    /// 注意：第一人称下擦除用的是**屏幕中心（准星）**的射线：按住左键、转动视角，
    /// 准星扫过哪里就擦到哪里。站在桌前（2 米内）才生效。
    /// 想改回"鼠标位置擦"，把 aimFromCenter 关掉即可。
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    public class DustErase : MonoBehaviour
    {
        [Header("灰尘贴图")]
        [Tooltip("可选：美术给的灰尘贴图（需 Read/Write）。留空则运行时程序生成")]
        public Texture2D dustTexture;
        [Tooltip("运行时生成灰尘贴图的分辨率")]
        public int textureSize = 256;

        [Header("擦除参数")]
        [Tooltip("笔刷半径（贴图像素）")]
        public float brushRadius = 26f;
        [Tooltip("擦掉多少比例算擦干净：设计稿要求 80%")]
        [Range(0.1f, 0.99f)] public float clearThreshold = 0.8f;
        [Tooltip("擦干净后灰尘淡出的时长")]
        public float fadeDuration = 0.9f;

        [Header("擦除条件")]
        [Tooltip("玩家（用它的位置算距离）。留空则不限制")]
        public Transform player;
        [Tooltip("必须先拿到抹布才能擦灰（设计稿：不再凭空擦）")]
        public RagPickup rag;
        [Tooltip("玩家要离多近才能擦（米）：设计稿要求走到桌前，这里给 2 米")]
        public float maxPlayerDistance = 2.0f;
        [Tooltip("射线的最远距离：站在桌前时 2-3 米足够")]
        public float maxRayDistance = 3.5f;
        [Tooltip("用屏幕中心（准星）发射擦除射线；关掉则用鼠标位置")]
        public bool aimFromCenter = true;

        [Header("擦完之后")]
        [Tooltip("灰尘下面要露出来的东西（会把它们的碰撞体打开，从而变成可点）")]
        public GameObject[] revealObjects;
        [Tooltip("擦净前先把这些物体藏起来：灰尘贴着桌面时，旧物会戳出灰面")]
        public bool hideRevealRenderers = true;
        [Tooltip("擦干净后才出现的半透明东西（桌面光斑等）。透明物体不吃深度，不关掉会透过灰尘显示出来")]
        public Renderer[] showAfterClear;
        public AudioDirector audio;

        [Header("笔刷指示")]
        [Tooltip("跟着鼠标走的小光斑，让玩家看得见自己在擦哪（可留空）")]
        public GameObject brushMarker;

        [Header("提示")]
        public string hoverHint = "按住左键滑动，擦掉桌上的灰";
        public string farHint = "走近一点再擦";

        /// <summary>0~1，已擦除的比例。</summary>
        public event Action<float> ProgressChanged;
        /// <summary>擦到阈值时触发一次。</summary>
        public event Action Cleared;

        /// <summary>没拿抹布就想擦时触发（用来播"太脏了，得先找块抹布擦擦"）。</summary>
        public event Action NoRagAttempt;

        public bool IsActive { get { return !_cleared; } }
        public bool IsFading { get { return _fading; } }
        public bool HasRag { get { return rag == null || rag.IsHeld; } }
        public bool IsInRange
        {
            get
            {
                if (player == null) return true;
                return Vector3.Distance(player.position, transform.position) <= maxPlayerDistance;
            }
        }
        public float Progress { get { return _total <= 0 ? 0f : (float)_erased / _total; } }

        Texture2D _tex;
        Color32[] _pixels;
        MeshCollider _col;
        Renderer _renderer;
        int _total;
        int _erased;
        bool _cleared;
        bool _fading;
        float _lastReported = -1f;

        void Awake()
        {
            _col = GetComponent<MeshCollider>();
            _renderer = GetComponent<Renderer>();

            BuildTexture();

            if (_renderer != null)
            {
                // material（不是 sharedMaterial）：运行时只改这一块灰尘的贴图，不动项目资源。
                // 注意不要再改 color —— 颜色是美术在材质上定的"灰"，代码只负责改 alpha。
                Material mat = _renderer.material;
                mat.mainTexture = _tex;
            }

            if (brushMarker != null) brushMarker.SetActive(false);

            SetRevealVisible(false);

            if (showAfterClear != null)
            {
                for (int i = 0; i < showAfterClear.Length; i++)
                    if (showAfterClear[i] != null) showAfterClear[i].enabled = false;
            }
        }

        /// <summary>灰尘下面的旧物：擦净前藏起来，擦净后显示。</summary>
        void SetRevealVisible(bool visible)
        {
            if (!hideRevealRenderers || revealObjects == null) return;
            for (int i = 0; i < revealObjects.Length; i++)
            {
                GameObject go = revealObjects[i];
                if (go == null) continue;
                Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                    if (rends[r] != null) rends[r].enabled = visible;
            }
        }

        void BuildTexture()
        {
            Color32[] src = null;
            int w = textureSize;
            int h = textureSize;

            if (dustTexture != null)
            {
                try
                {
                    src = dustTexture.GetPixels32();
                    w = dustTexture.width;
                    h = dustTexture.height;
                }
                catch (Exception)
                {
                    src = null;   // 贴图没开 Read/Write，退回程序生成
                }
            }

            if (src == null)
            {
                src = GenerateDust(w, h);
            }

            _pixels = new Color32[src.Length];
            Array.Copy(src, _pixels, src.Length);

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.filterMode = FilterMode.Bilinear;
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);

            _total = _pixels.Length;
            _erased = 0;
        }

        static Color32[] GenerateDust(int w, int h)
        {
            var px = new Color32[w * h];
            System.Random rng = new System.Random(20260101);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 灰底 + 噪点 + 几道横向纤维，看着像落了很久的灰
                    float n = (float)rng.NextDouble();
                    float fiber = Mathf.PerlinNoise(x * 0.05f, y * 0.55f);
                    float v = 0.60f + n * 0.14f + fiber * 0.16f;
                    byte c = (byte)Mathf.Clamp(v * 255f, 0f, 255f);
                    px[y * w + x] = new Color32((byte)(c * 0.97f), (byte)(c * 0.94f), (byte)(c * 0.89f), 255);
                }
            }
            return px;
        }

        void Update()
        {
            if (_cleared) return;

            // 没有抹布就擦不动，并给一次"得先找块抹布"的提示
            if (!HasRag)
            {
                TryWarnNoRag();
                return;
            }

            bool holding = Input.GetMouseButton(0);
            if (!holding)
            {
                if (brushMarker != null && brushMarker.activeSelf) brushMarker.SetActive(false);
                return;
            }

            if (!IsInRange) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = aimFromCenter
                ? cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f))
                : cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxRayDistance)) return;
            if (hit.collider != _col) return;

            ShowBrush(hit.point, hit.normal);
            Erase(hit.textureCoord);
        }

        float _nextNoRagWarn;

        /// <summary>没拿抹布却对着桌面用力擦时，给一次提示（有冷却）。</summary>
        void TryWarnNoRag()
        {
            if (Time.time < _nextNoRagWarn) return;
            if (!Input.GetMouseButton(0)) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxRayDistance)) return;
            if (hit.collider != _col) return;

            _nextNoRagWarn = Time.time + 5f;
            if (NoRagAttempt != null) NoRagAttempt();
        }

        void ShowBrush(Vector3 worldPoint, Vector3 normal)        {
            if (brushMarker == null) return;
            if (!brushMarker.activeSelf) brushMarker.SetActive(true);

            brushMarker.transform.position = worldPoint + normal * 0.004f;
            // 光斑贴在被打到的那一面（灰尘现在是贴合网格的壳，有上下左右多个面）
            brushMarker.transform.rotation = Quaternion.FromToRotation(Vector3.forward, -normal);
        }

        void Erase(Vector2 uv)
        {
            int cx = Mathf.RoundToInt(Mathf.Clamp01(uv.x) * (_tex.width - 1));
            int cy = Mathf.RoundToInt(Mathf.Clamp01(uv.y) * (_tex.height - 1));
            int rad = Mathf.Max(2, Mathf.RoundToInt(brushRadius));
            int radSq = rad * rad;

            int minX = Mathf.Max(0, cx - rad);
            int maxX = Mathf.Min(_tex.width - 1, cx + rad);
            int minY = Mathf.Max(0, cy - rad);
            int maxY = Mathf.Min(_tex.height - 1, cy + rad);

            bool dirty = false;
            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - cy;
                int row = y * _tex.width;
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

            if (!dirty) return;

            _tex.SetPixels32(_pixels);
            _tex.Apply(false);

            float p = Progress;
            if (!Mathf.Approximately(p, _lastReported))
            {
                _lastReported = p;
                if (ProgressChanged != null) ProgressChanged(p);
            }

            if (p >= clearThreshold) Clear();
        }

        /// <summary>擦到阈值：露旧物 + 音效 + 淡出。</summary>
        public void Clear()
        {
            if (_cleared) return;
            _cleared = true;

            if (brushMarker != null) brushMarker.SetActive(false);

            if (showAfterClear != null)
            {
                for (int i = 0; i < showAfterClear.Length; i++)
                    if (showAfterClear[i] != null) showAfterClear[i].enabled = true;
            }

            SetRevealVisible(true);

            if (revealObjects != null)
            {
                for (int i = 0; i < revealObjects.Length; i++)
                {
                    GameObject go = revealObjects[i];
                    if (go == null) continue;
                    Collider[] cols = go.GetComponentsInChildren<Collider>(true);
                    for (int c = 0; c < cols.Length; c++) cols[c].enabled = true;
                }
            }

            if (audio != null) audio.PlayDust();
            if (Cleared != null) Cleared();

            StartCoroutine(FadeOutRoutine());
        }

        IEnumerator FadeOutRoutine()
        {
            _fading = true;

            Material mat = _renderer != null ? _renderer.material : null;
            float t = 0f;
            while (t < fadeDuration && mat != null)
            {
                t += Time.deltaTime;
                Color c = mat.color;
                c.a = Mathf.Lerp(1f, 0f, t / fadeDuration);
                mat.color = c;
                yield return null;
            }

            if (mat != null)
            {
                Color c = mat.color;
                c.a = 0f;
                mat.color = c;
            }

            if (_renderer != null) _renderer.enabled = false;
            if (_col != null) _col.enabled = false;
            _fading = false;
        }

        /// <summary>给自动化测试用：直接擦完。</summary>
        public void ForceClear()
        {
            if (_cleared) return;
            for (int i = 0; i < _pixels.Length; i++)
            {
                Color32 c = _pixels[i];
                c.a = 0;
                _pixels[i] = c;
            }
            _erased = _total;
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
            Clear();
        }

        /// <summary>重置（重新开始一局时用）。</summary>
        public void ResetDust()
        {
            StopAllCoroutines();
            _cleared = false;
            _fading = false;
            _erased = 0;
            BuildTexture();
            if (_renderer != null)
            {
                _renderer.enabled = true;
                _renderer.material.mainTexture = _tex;
                Color c = _renderer.material.color;
                c.a = 1f;
                _renderer.material.color = c;
            }
            if (_col != null) _col.enabled = true;
            if (brushMarker != null) brushMarker.SetActive(false);

            if (showAfterClear != null)
            {
                for (int i = 0; i < showAfterClear.Length; i++)
                    if (showAfterClear[i] != null) showAfterClear[i].enabled = false;
            }
        }
    }
}
