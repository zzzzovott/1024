using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 给一件旧物生成一份"纯白替身"，放在剪影专用层上，只给剪影相机看。
    ///
    /// 为什么不用 SetReplacementShader：那个接口依赖 shader 在运行时找得到，
    /// 打包裁剪后经常拿到 null，剪影就带上物件的颜色了。
    /// 直接复制一份白色网格最稳，而且美术只需要改原件，替身是运行时自动生成的。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class SilhouetteProxy : MonoBehaviour
    {
        [Tooltip("替身所在的层（要和剪影相机的 cullingMask 对上）")]
        public int layer = 8;

        [Tooltip("留空则从 Resources/SilhouetteWhite 取")]
        public Material whiteMaterial;

        [Tooltip("额外放大一点点，避免和原件深度打架")]
        public float scaleJitter = 1.0f;

        void Awake()
        {
            Build();
        }

        void Build()
        {
            if (whiteMaterial == null)
                whiteMaterial = Resources.Load<Material>("SilhouetteWhite");

            if (whiteMaterial == null)
            {
                Debug.LogWarning("[1024] 找不到 Resources/SilhouetteWhite，剪影替身无法生成");
                return;
            }

            GameObject root = new GameObject("_ShadowProxy");
            root.transform.SetParent(transform, false);
            root.layer = layer;

            Copy(transform, root.transform);
        }

        void Copy(Transform src, Transform parent)
        {
            for (int i = 0; i < src.childCount; i++)
            {
                Transform child = src.GetChild(i);
                if (child.name == "_ShadowProxy") continue;

                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    GameObject go = new GameObject(child.name + "_sil");
                    go.layer = layer;
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = child.localPosition;
                    go.transform.localRotation = child.localRotation;
                    go.transform.localScale = child.localScale * scaleJitter;

                    MeshFilter nmf = go.AddComponent<MeshFilter>();
                    nmf.sharedMesh = mf.sharedMesh;

                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = whiteMaterial;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                }

                Copy(child, parent);
            }
        }
    }
}