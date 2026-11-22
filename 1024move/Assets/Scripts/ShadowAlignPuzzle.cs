using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 白盒阶段的光影/角度判定。
    ///
    /// 设计要点（别照抄策划案里的"5 度误差"）：
    /// 1) 判定分"进入"和"离开"两个阈值，防止边界抖动反复触发；
    /// 2) 越接近目标，输出 Progress 越高 —— 用这个值去驱动墙上光斑的透明度/亮度，
    ///    玩家才有"我在把它转对"的手感，而不是盲转；
    /// 3) 进入磁吸范围后自动吸附并判定成功，比要求玩家手抖停在 5 度内友好得多。
    /// </summary>
    public class ShadowAlignPuzzle : MonoBehaviour
    {
        [Header("目标朝向")]
        [Tooltip("目标旋转（右键组件 → 把当前朝向设为谜底）")]
        public Quaternion targetRotation = Quaternion.identity;
        [Tooltip("是否在 Start 时把当前朝向记录为谜底（摆好物体后勾上即可）")]
        public bool captureCurrentOnStart = true;

        [Header("判定")]
        [Tooltip("进入磁吸/判定的角度误差")]
        public float enterTolerance = 8f;
        [Tooltip("离开判定区的角度误差（略大于 enter，防抖）")]
        public float exitTolerance = 11f;
        [Tooltip("磁吸范围：进入后自动吸附到谜底")]
        public bool useMagnet = true;
        [Tooltip("超过这个误差，光斑完全不亮")]
        public float fadeRange = 70f;

        [Header("引用")]
        [Tooltip("实际被旋转的物体，留空则取自身")]
        public Transform rotatingPart;
        public DragRotateBody rotator;

        [Header("运行时状态（只读）")]
        [SerializeField] float currentError;
        [SerializeField] bool solved;

        /// <summary>0 = 完全没对上，1 = 完全对上。用来驱动光斑亮度。</summary>
        public event Action<float> ProgressChanged;
        /// <summary>解开时触发，参数为这个物件代表的密码位（比如 "1"）。</summary>
        public event Action Solved;

        public float CurrentError { get { return currentError; } }
        public bool IsSolved { get { return solved; } }

        float _lastProgress = -1f;

        void Awake()
        {
            if (rotatingPart == null) rotatingPart = transform;
            if (rotator == null) rotator = GetComponent<DragRotateBody>();
        }

        void Start()
        {
            if (captureCurrentOnStart)
                targetRotation = rotatingPart.rotation;
        }

        void Update()
        {
            currentError = Quaternion.Angle(rotatingPart.rotation, targetRotation);

            float p = Mathf.Clamp01(1f - currentError / Mathf.Max(1f, fadeRange));
            p = Mathf.SmoothStep(0f, 1f, p);

            if (!Mathf.Approximately(p, _lastProgress))
            {
                _lastProgress = p;
                if (ProgressChanged != null) ProgressChanged(p);
            }

            if (solved) return;

            float threshold = solved ? exitTolerance : enterTolerance;
            if (currentError <= threshold)
            {
                if (useMagnet && rotator != null)
                    rotator.SnapTo(targetRotation);

                solved = true;
                // 吸附结束后再判定，避免"刚进范围就锁死"的突兀
                Invoke("FireSolved", useMagnet ? 0.3f : 0.05f);
            }
        }

        void FireSolved()
        {
            if (Solved != null) Solved();
        }

        /// <summary>把物体当前朝向记成谜底（物体摆到正确位置后调用）。</summary>
        [ContextMenu("把当前朝向设为谜底")]
        public void CaptureTarget()
        {
            targetRotation = rotatingPart != null ? rotatingPart.rotation : transform.rotation;
        }

        /// <summary>重置谜题（重开/失败重试用）。</summary>
        public void ResetPuzzle(Quaternion startRotation)
        {
            solved = false;
            _lastProgress = -1f;
            if (rotator != null) rotator.SetRotationImmediate(startRotation);
        }

        void OnDrawGizmosSelected()
        {
            if (rotatingPart == null) rotatingPart = transform;
            Gizmos.color = solved ? Color.green : Color.cyan;
            Vector3 p = rotatingPart.position;
            Gizmos.DrawRay(p, rotatingPart.rotation * Vector3.forward * 0.4f);
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
            Gizmos.DrawRay(p, targetRotation * Vector3.forward * 0.4f);
        }
    }
}