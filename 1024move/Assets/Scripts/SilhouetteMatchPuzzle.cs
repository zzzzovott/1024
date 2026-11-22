using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 【进阶方案 · 推荐最终形态】
    /// 剪影比对判定：用一台正交相机（模拟"光源"方向）把物件渲染成纯白剪影，
    /// 再和墙上的"数字模板"做像素重合度比对（IoU）。
    ///
    /// 为什么需要它：真让一只铁皮青蛙的物理阴影投出"1"，靠调灯是调不出来的。
    /// 正确做法是"视觉欺骗"—— 让剪影在某个角度看起来像数字，用像素重合度判定。
    /// 这个方案对美术可控、对玩家诚，白盒阶段就能验证。
    ///
    /// 使用前提：
    /// - 物件放在单独 Layer（如 "PuzzleSilhouette"），剪影相机只渲染该层；
    /// - 剪影相机为正交、背景清成全透明、物件材质用 Unlit/纯白；
    /// - 模板图是一张黑白 Mask（白=数字区域）。
    /// </summary>
    public class SilhouetteMatchPuzzle : MonoBehaviour
    {
        [Header("剪影相机（放在光源位置，正交）")]
        public Camera silhouetteCamera;
        public LayerMask silhouetteLayer = ~0;

        [Header("数字模板")]
        public Texture2D targetMask;
        [Tooltip("渲染分辨率，越小越省，128 足够")]
        public int resolution = 128;

        [Header("判定")]
        [Tooltip("像素重合度达到多少算解开")]
        [Range(0.3f, 1f)] public float solveThreshold = 0.78f;
        [Tooltip("低于多少完全不亮")]
        [Range(0f, 1f)] public float deadZone = 0.15f;
        [Tooltip("检测间隔（秒），别每帧都读像素")]
        public float checkInterval = 0.1f;

        [Header("引用")]
        public DragRotateBody rotator;
        public ShadowAlignPuzzle angleFallback;

        public event Action<float> ProgressChanged;
        public event Action Solved;

        public float Score { get; private set; }
        public bool IsSolved { get; private set; }

        RenderTexture _rt;
        Texture2D _readback;
        bool[] _maskBits;
        int _maskW, _maskH;
        float _timer;
        float _lastReported = -1f;

        void Awake()
        {
            if (rotator == null) rotator = GetComponent<DragRotateBody>();
            BuildMaskBits();
        }

        void BuildMaskBits()
        {
            if (targetMask == null) return;
            _maskW = targetMask.width;
            _maskH = targetMask.height;
            Color32[] px = targetMask.GetPixels32();
            _maskBits = new bool[px.Length];
            for (int i = 0; i < px.Length; i++)
                _maskBits[i] = px[i].r > 127;
        }

        void Start()
        {
            if (silhouetteCamera == null) return;

            _rt = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
            _rt.Create();
            silhouetteCamera.targetTexture = _rt;
            silhouetteCamera.clearFlags = CameraClearFlags.SolidColor;
            silhouetteCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            silhouetteCamera.cullingMask = silhouetteLayer;
            silhouetteCamera.orthographic = true;

            _readback = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        }

        void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            if (_readback != null) Destroy(_readback);
        }

        void Update()
        {
            if (silhouetteCamera == null || _maskBits == null || IsSolved) return;

            _timer += Time.deltaTime;
            if (_timer < checkInterval) return;
            _timer = 0f;

            Score = MeasureIoU();
            float p = Mathf.Clamp01((Score - deadZone) / Mathf.Max(0.0001f, solveThreshold - deadZone));
            p = Mathf.SmoothStep(0f, 1f, p);

            if (!Mathf.Approximately(p, _lastReported))
            {
                _lastReported = p;
                if (ProgressChanged != null) ProgressChanged(p);
            }

            if (Score >= solveThreshold)
            {
                IsSolved = true;
                if (Solved != null) Solved();
            }
        }

        float MeasureIoU()
        {
            RenderTexture prev = RenderTexture.active;
            silhouetteCamera.Render();
            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            _readback.Apply(false);
            RenderTexture.active = prev;

            Color32[] px = _readback.GetPixels32();
            int inter = 0, union = 0;

            for (int y = 0; y < resolution; y++)
            {
                // 把剪影图缩放到模板分辨率再比
                int my = Mathf.Clamp(y * _maskH / resolution, 0, _maskH - 1);
                for (int x = 0; x < resolution; x++)
                {
                    int mx = Mathf.Clamp(x * _maskW / resolution, 0, _maskW - 1);
                    bool a = px[y * resolution + x].a > 100;
                    bool b = _maskBits[my * _maskW + mx];
                    if (a && b) inter++;
                    if (a || b) union++;
                }
            }
            if (union == 0) return 0f;
            return (float)inter / union;
        }

        /// <summary>给 UI 用的"当前剪影图"，可以直接显示成墙上的光斑。</summary>
        public RenderTexture SilhouetteTexture { get { return _rt; } }
    }
}