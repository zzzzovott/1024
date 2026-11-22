using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 画圈倒带（设计稿 V3.0 第五部分，模块 5）。
    ///
    /// 玩家按住左键绕着磁带画圈，累计转满 3 圈（1080°）就算倒完带。
    /// 用逐帧角差累加（Mathf.DeltaAngle）而不是记录轨迹，
    /// 这样顺时针逆时针都算、鼠标抖一下也不会崩。
    ///
    /// 两个关键防护：
    /// 1) 靠近圆心时不累计 —— 圆心附近的角速度是发散的，会瞬间刷满进度；
    /// 2) 按住不动时不累计 —— 否则玩家松手前一直在原地也会算圈数。
    /// </summary>
    public class CircularRewind : MonoBehaviour
    {
        [Header("目标")]
        [Tooltip("要转几圈")]
        public float turnsRequired = 3f;

        [Header("判定半径（屏幕像素）")]
        [Tooltip("比这个更靠近圆心就不算，防止抖动刷进度")]
        public float minRadius = 55f;
        [Tooltip("比这个更远也不就算了，避免玩家在屏幕角落乱晃")]
        public float maxRadius = 520f;

        [Header("反馈")]
        [Tooltip("转轮跟着转的子物体，可留空")]
        public Transform reel;
        [Tooltip("每转满一圈转轮转多少度")]
        public float reelDegreesPerCircle = 360f;

        [Header("完成演出")]
        public AudioVisualizer visualizer;
        public float humDuration = 7f;

        public event Action<float> ProgressChanged;   // 0~1
        public event Action<int, int> TurnCompleted;  // 当前圈数, 总圈数
        public event Action Solved;

        public float Accumulated { get { return _accum; } }
        public float Progress01 { get { return Mathf.Clamp01(_accum / (_total)); } }
        public bool IsSolved { get { return _solved; } }
        public bool Interactable { get { return _interactable; } set { _interactable = value; } }

        float _accum;
        float _total;
        bool _interactable;
        bool _solved;
        bool _hasPrev;
        float _prevAngle;
        float _lastReported = -1f;
        int _turns;

        void Awake()
        {
            _total = Mathf.Max(1f, turnsRequired) * 360f;
        }

        public void SetInteractable(bool value)
        {
            _interactable = value;
            if (!value) _hasPrev = false;
        }

        public void ResetRewind()
        {
            _accum = 0f;
            _turns = 0;
            _solved = false;
            _hasPrev = false;
            _lastReported = -1f;
        }

        void Update()
        {
            if (!_interactable || _solved) return;

            if (!Input.GetMouseButton(0))
            {
                _hasPrev = false;
                return;
            }

            Vector3 sp = Camera.main != null
                ? Camera.main.WorldToScreenPoint(transform.position)
                : (Vector3)Input.mousePosition;
            if (sp.z < 0f) { _hasPrev = false; return; }

            Vector2 d = (Vector2)Input.mousePosition - new Vector2(sp.x, sp.y);
            float dist = d.magnitude;

            if (dist < minRadius || dist > maxRadius)
            {
                _hasPrev = false;
                return;
            }

            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

            if (!_hasPrev)
            {
                _prevAngle = angle;
                _hasPrev = true;
                return;
            }

            float delta = Mathf.DeltaAngle(_prevAngle, angle);
            _prevAngle = angle;

            // 单帧超过 40° 基本是瞬移/切窗口，丢掉
            if (Mathf.Abs(delta) > 40f) return;

            _accum += Mathf.Abs(delta);

            // 转轮绕自己的轴转（磁带轮是圆柱，轴是 Y）
            if (reel != null)
                reel.Rotate(0f, -delta, 0f, Space.Self);

            int turns = Mathf.FloorToInt(_accum / 360f);
            if (turns > _turns)
            {
                _turns = turns;
                if (AudioDirector.Instance != null) AudioDirector.Instance.PlayTick();
                if (TurnCompleted != null) TurnCompleted(_turns, Mathf.RoundToInt(turnsRequired));
            }

            float p = Progress01;
            if (!Mathf.Approximately(p, _lastReported))
            {
                _lastReported = p;
                if (ProgressChanged != null) ProgressChanged(p);
            }

            if (_accum >= _total) Complete();
        }

        void Complete()
        {
            if (_solved) return;
            _solved = true;
            _interactable = false;

            if (AudioDirector.Instance != null)
            {
                AudioDirector.Instance.PlayClick();
                AudioDirector.Instance.PlayHumming();
            }
            if (visualizer != null) visualizer.Show(humDuration);

            if (Solved != null) Solved();
        }

        /// <summary>给自动化测试用。</summary>
        public void ForceComplete()
        {
            _accum = _total;
            _turns = Mathf.RoundToInt(turnsRequired);
            Complete();
        }
    }
}