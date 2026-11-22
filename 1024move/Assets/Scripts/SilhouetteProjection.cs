using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 把旧物的剪影实时投到墙上。
    ///
    /// 做法：一台正交相机（相当于光源）只渲染这件旧物所在的层，渲到 RenderTexture，
    /// 再把这张图贴到墙上的一块薄板上 —— 玩家转动物件时，墙上的影子跟着变。
    /// 墙上还压着一张"目标轮廓"（数字模板），玩家的任务就是让影子转回到轮廓里。
    ///
    /// 这样"光影解谜"就不是靠调灯调出来的，而是可控、可预期的视觉错位。
    /// </summary>
    public class SilhouetteProjection : MonoBehaviour
    {
        [Header("剪影相机（正交，正对物件）")]
        public Camera silhouetteCamera;

        [Header("墙上的两块投影板")]
        [Tooltip("实时剪影")]
        public Renderer liveQuad;
        [Tooltip("目标轮廓（数字模板）")]
        public Renderer targetQuad;

        [Header("数字模板")]
        public Texture2D targetMask;

        [Header("外观")]
        public Color liveColor = new Color(1f, 0.90f, 0.70f, 1f);
        [Tooltip("轮廓静止时的透明度")]
        [Range(0f, 0.6f)] public float targetAlpha = 0.14f;
        [Tooltip("影子在完全没对上 / 完全对上时的透明度")]
        public float liveMinAlpha = 0.38f;
        public float liveMaxAlpha = 1f;

        [Header("分辨率")]
        public int resolution = 256;

        RenderTexture _rt;
        Material _liveMat;
        Material _targetMat;

        public RenderTexture Texture { get { return _rt; } }

        void Start()
        {
            if (silhouetteCamera == null) return;

            _rt = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32);
            _rt.name = "RT_Silhouette_" + gameObject.name;
            _rt.Create();

            silhouetteCamera.targetTexture = _rt;
            silhouetteCamera.clearFlags = CameraClearFlags.SolidColor;
            silhouetteCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            silhouetteCamera.orthographic = true;
            silhouetteCamera.enabled = true;

            // 剪影由 SilhouetteProxy 生成的纯白替身提供，这里不再做 shader 替换

            if (liveQuad != null)
            {
                _liveMat = liveQuad.material;
                _liveMat.mainTexture = _rt;
                SetProgress(0f);
            }

            if (targetQuad != null)
            {
                _targetMat = targetQuad.material;
                if (targetMask != null) _targetMat.mainTexture = targetMask;
                _targetMat.color = new Color(1f, 1f, 1f, targetAlpha);
            }

            if (silhouetteCamera != null && liveQuad != null)
            {
                Debug.Log("[1024][proj] " + name + " ready");
            }
        }

        /// <summary>由 DeskFocusInteractable 每帧驱动：0 = 完全没对上，1 = 对上。</summary>
        public void SetProgress(float p)
        {
            if (_liveMat == null) return;
            float a = Mathf.Lerp(liveMinAlpha, liveMaxAlpha, Mathf.Clamp01(p));
            _liveMat.color = new Color(liveColor.r, liveColor.g, liveColor.b, a * liveColor.a);
        }

        void OnDestroy()
        {
            if (silhouetteCamera != null) silhouetteCamera.targetTexture = null;
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }
    }
}