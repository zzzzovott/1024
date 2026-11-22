using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 轨迹球式自由旋转（完全替换旧的"单轴拨盘"逻辑）：
    ///
    /// · 鼠标左右滑 → 绕**相机的上轴**转；上下滑 → 绕**相机的右轴**转；
    /// · XYZ 三轴全开，**没有任何角度限制 / Clamp**，可以无限 360° 翻转；
    /// · 旋转基于摄像机局部空间，所以不管玩家从哪个方向看，手感都一致；
    /// · 目标四元数 + Quaternion.Slerp 阻尼，手感平滑不生硬；
    /// · 调试：按住左键拖动时，控制台实时打印当前欧拉角（间隔可调），
    ///   拿起物体时会打印一次目标角度，方便对照。
    /// </summary>
    public class DragRotateBody : MonoBehaviour
    {
        [Header("手感")]
        [Tooltip("鼠标位移 → 旋转角度倍率")]
        public float dragSensitivity = 4f;
        [Tooltip("阻尼：越大跟手越紧（6 = 有点飘，20 = 很跟手）")]
        public float smoothing = 14f;
        [Tooltip("松手后保留的角速度比例")]
        [Range(0f, 1f)] public float inertiaRetain = 0.3f;
        [Tooltip("惯性衰减速度")]
        public float angularDamping = 3f;
        [Tooltip("角速度上限（度/秒），只防甩飞，不影响角度范围")]
        public float maxAngularSpeed = 900f;

        [Header("调试")]
        public bool debugLog = true;
        [Tooltip("拖动时打印欧拉角的间隔（秒）")]
        public float debugInterval = 0.1f;
        [Tooltip("拿起时打印目标角度")]
        public bool logTargetOnEnable = true;

        [Header("状态")]
        public bool interactable = true;

        Quaternion _target;
        Vector3 _angularVelocity;
        bool _dragging;
        Camera _cam;
        float _nextLogAt;

        public bool IsInteracting { get { return _dragging; } }

        void Awake()
        {
            _cam = Camera.main;
            _target = transform.rotation;
        }

        void Update()
        {
            if (!interactable) return;
            if (_cam == null) _cam = Camera.main;

            HandleDrag();
            ApplyInertia();
            SmoothRotate();
            LogAngles();
        }

        /// <summary>拿起/放下时调用；拿起时把当前朝向作为起点，并打印目标角度。</summary>
        public void SetInteractable(bool value)
        {
            interactable = value;

            if (value)
            {
                _target = transform.rotation;
                _angularVelocity = Vector3.zero;
                _dragging = false;

                if (debugLog && logTargetOnEnable) LogTarget();
            }
            else
            {
                _dragging = false;
                _angularVelocity = Vector3.zero;
                _target = transform.rotation;
            }
        }

        void HandleDrag()
        {
            if (!Input.GetMouseButton(0))
            {
                if (_dragging)
                {
                    _dragging = false;
                    _angularVelocity *= inertiaRetain;
                }
                return;
            }

            float dx = Input.GetAxisRaw("Mouse X") * dragSensitivity;
            float dy = Input.GetAxisRaw("Mouse Y") * dragSensitivity;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) < 0.0001f) return;

            _dragging = true;

            Transform cam = _cam != null ? _cam.transform : transform;

            // 轨迹球：左右绕相机上轴，上下绕相机右轴（世界空间叠加，无任何限制）
            Quaternion delta =
                Quaternion.AngleAxis(-dx, cam.up) *
                Quaternion.AngleAxis(dy, cam.right);

            _target = delta * _target;

            // 记录角速度给惯性用（只受 maxAngularSpeed 影响，不影响角度范围）
            Quaternion change = _target * Quaternion.Inverse(transform.rotation);
            float angle;
            Vector3 axis;
            change.ToAngleAxis(out angle, out axis);
            if (angle > 180f) { angle = 360f - angle; axis = -axis; }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = axis * (angle / dt);
            if (v.magnitude > maxAngularSpeed) v = v.normalized * maxAngularSpeed;
            _angularVelocity = v;
        }

        void ApplyInertia()
        {
            if (_dragging) return;
            if (_angularVelocity.sqrMagnitude < 1f) return;

            Quaternion spin = Quaternion.AngleAxis(
                _angularVelocity.magnitude * Time.deltaTime,
                _angularVelocity.normalized);

            _target = spin * _target;

            _angularVelocity = Vector3.Lerp(
                _angularVelocity, Vector3.zero, 1f - Mathf.Exp(-angularDamping * Time.deltaTime));

            if (_angularVelocity.magnitude < 2f) _angularVelocity = Vector3.zero;
        }

        void SmoothRotate()
        {
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, _target, k);
        }

        /// <summary>平滑吸附到目标朝向（光影判定用）。</summary>
        public void SnapTo(Quaternion target, float duration = -1f)
        {
            _angularVelocity = Vector3.zero;
            _dragging = false;
            _target = target;
        }

        /// <summary>磁吸辅助：把手上目标朝向缓慢拉向答案（玩家仍在拖拽也不会打架）。</summary>
        public void AssistToward(Quaternion target, float rate)
        {
            float k = 1f - Mathf.Exp(-Mathf.Max(0.01f, rate) * Time.deltaTime);
            _target = Quaternion.Slerp(_target, target, k);
        }

        /// <summary>硬设朝向（重置用）。</summary>
        public void SetRotationImmediate(Quaternion q)
        {
            _dragging = false;
            _angularVelocity = Vector3.zero;
            _target = q;
            transform.rotation = q;
        }

        // ---------------- 调试 ----------------

        void LogAngles()
        {
            if (!debugLog || !_dragging) return;
            if (Time.time < _nextLogAt) return;

            _nextLogAt = Time.time + Mathf.Max(0.02f, debugInterval);
            Vector3 e = transform.eulerAngles;
            Debug.Log("[1024][Rotate] " + name + " Euler=(" +
                      e.x.ToString("F1") + ", " + e.y.ToString("F1") + ", " + e.z.ToString("F1") + ")");
        }

        void LogTarget()
        {
            Vector3 e = transform.eulerAngles;

            ShadowAlignPuzzle puzzle = GetComponent<ShadowAlignPuzzle>();
            if (puzzle != null)
            {
                Vector3 t = puzzle.targetRotation.eulerAngles;
                Debug.Log("[1024][Rotate] 拿起 " + name +
                          "  当前=(" + e.x.ToString("F1") + ", " + e.y.ToString("F1") + ", " + e.z.ToString("F1") + ")" +
                          "  目标=(" + t.x.ToString("F1") + ", " + t.y.ToString("F1") + ", " + t.z.ToString("F1") + ")");
            }
            else
            {
                Debug.Log("[1024][Rotate] 拿起 " + name + "  当前=(" +
                          e.x.ToString("F1") + ", " + e.y.ToString("F1") + ", " + e.z.ToString("F1") + ")");
            }
        }
    }
}
