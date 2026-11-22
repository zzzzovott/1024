using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 3D 旧物的"手感"核心：鼠标拖拽 → 带阻尼与惯性的自由旋转。
    /// 用轨迹球式（相机空间）旋转，比屏幕 XY 轴更符合直觉：任何机位下都不会"转不动"。
    /// 挂在物件根节点上，需要 Collider 才能被 OnMouseDrag 命中。
    /// </summary>
    public class DragRotateBody : MonoBehaviour
    {
        [Header("手感")]
        [Tooltip("鼠标位移 → 旋转角度倍率（Unity 的 Mouse X 已是 像素×0.1）")]
        public float dragSensitivity = 5f;

        [Header("转轴限制（拨盘）")]
        [Tooltip("勾上后只能绕一根轴转，像拨转盘。自由三轴会让玩家在 3D 空间里瞎找角度。")]
        public bool axisLock = true;
        [Tooltip("锁定的世界坐标轴")]
        public Vector3 lockAxis = Vector3.up;
        [Tooltip("松手后角速度衰减速度（越大停得越快）")]
        public float angularDamping = 3.2f;
        [Tooltip("松手后保留的角速度比例")]
        [Range(0f, 1f)] public float inertiaRetain = 0.55f;
        [Tooltip("角速度上限（度/秒），防止甩飞")]
        public float maxAngularSpeed = 520f;

        [Header("吸附")]
        [Tooltip("吸附时的插值时长")]
        public float snapDuration = 0.25f;

        [Header("状态")]
        public bool interactable = true;

        Vector3 _angularVelocity;
        bool _dragging;
        bool _snapping;
        Camera _cam;

        public bool IsInteracting { get { return _dragging || _snapping; } }

        void Awake()
        {
            _cam = Camera.main;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            if (!value)
            {
                _dragging = false;
                _snapping = false;
                _angularVelocity = Vector3.zero;
            }
        }

        void Update()
        {
            if (!interactable) return;

            if (_cam == null) _cam = Camera.main;

            if (Input.GetMouseButtonUp(0) && _dragging)
            {
                _dragging = false;
                _angularVelocity *= inertiaRetain;
            }

            if (_dragging || _snapping) return;

            if (_angularVelocity.sqrMagnitude > 0.0001f)
            {
                if (axisLock && lockAxis.sqrMagnitude > 0.0001f)
                    _angularVelocity = Vector3.Project(_angularVelocity, lockAxis.normalized);

                float speed = _angularVelocity.magnitude;
                transform.rotation = Quaternion.AngleAxis(speed * Time.deltaTime, _angularVelocity / speed) * transform.rotation;
                _angularVelocity = Vector3.Lerp(_angularVelocity, Vector3.zero, 1f - Mathf.Exp(-angularDamping * Time.deltaTime));
                if (_angularVelocity.magnitude < 1.5f) _angularVelocity = Vector3.zero;
            }
        }

        void OnMouseDrag()
        {
            if (!interactable || _snapping) return;

            _dragging = true;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            float dx = Input.GetAxisRaw("Mouse X") * dragSensitivity;
            float dy = Input.GetAxisRaw("Mouse Y") * dragSensitivity;

            Quaternion delta;
            if (axisLock)
            {
                // 拨盘模式：只绕一根轴转。左右拖是主方向，上下拖给一点微调，
                // 免得玩家竖着拖发现没反应。
                Vector3 axis = lockAxis.sqrMagnitude > 0.0001f ? lockAxis.normalized : Vector3.up;
                float turn = -dx + dy * 0.35f;
                delta = Quaternion.AngleAxis(turn, axis);
            }
            else
            {
                delta =
                    Quaternion.AngleAxis(-dx, _cam.transform.up) *
                    Quaternion.AngleAxis(dy, _cam.transform.right);
            }

            Quaternion before = transform.rotation;
            transform.rotation = delta * transform.rotation;

            RecordVelocity(before, transform.rotation);
        }

        void RecordVelocity(Quaternion before, Quaternion after)
        {
            Quaternion d = after * Quaternion.Inverse(before);
            float angle;
            Vector3 axis;
            d.ToAngleAxis(out angle, out axis);
            if (angle > 180f) { angle = 360f - angle; axis = -axis; }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 v = axis * (angle / dt);
            if (v.magnitude > maxAngularSpeed) v = v.normalized * maxAngularSpeed;
            _angularVelocity = v;
        }

        /// <summary>平滑吸附到目标朝向（由光影判定调用）。</summary>
        public void SnapTo(Quaternion target, float duration = -1f)
        {
            if (!interactable) return;
            StopAllCoroutines();
            StartCoroutine(SnapRoutine(target, duration < 0f ? snapDuration : duration));
        }

        System.Collections.IEnumerator SnapRoutine(Quaternion target, float duration)
        {
            _snapping = true;
            _dragging = false;
            _angularVelocity = Vector3.zero;

            Quaternion from = transform.rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                transform.rotation = Quaternion.Slerp(from, target, u);
                yield return null;
            }
            transform.rotation = target;
            _snapping = false;
        }

        /// <summary>硬设朝向（重置用）。</summary>
        public void SetRotationImmediate(Quaternion q)
        {
            StopAllCoroutines();
            _snapping = false;
            _dragging = false;
            _angularVelocity = Vector3.zero;
            transform.rotation = q;
        }
    }
}