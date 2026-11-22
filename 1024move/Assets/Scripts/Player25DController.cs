using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 2.5D 固定机位下的角色移动。
    /// 主推"点击移动"（鼠标已经是解谜的主交互语言），WASD 作为可选补充。
    /// 移动方向始终相对当前相机水平面，因此镜头怎么摆都能走对方向。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class Player25DController : MonoBehaviour
    {
        [Header("移动参数")]
        [Tooltip("每秒移动距离（米）")]
        public float moveSpeed = 2.2f;
        [Tooltip("转身速度")]
        public float rotateSpeed = 14f;
        [Tooltip("重力")]
        public float gravity = -22f;
        [Tooltip("走到目标点的判定半径")]
        public float arriveDistance = 0.12f;

        [Header("镜头参考（留空则自动取 Camera.main）")]
        public Transform cameraTransform;

        [Header("开关")]
        public bool allowKeyboard = true;
        public bool allowClickToMove = true;

        CharacterController _cc;
        Vector3 _planarVelocity;
        float _verticalVelocity;
        Vector3 _clickTarget;
        bool _hasClickTarget;
        bool _inputEnabled = true;

        public bool IsMoving
        {
            get { return _hasClickTarget || _planarVelocity.sqrMagnitude > 0.01f; }
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        public void SetInputEnabled(bool value)
        {
            _inputEnabled = value;
            if (!value)
            {
                _hasClickTarget = false;
                _planarVelocity = Vector3.zero;
            }
        }

        /// <summary>让小人自动走到某个世界坐标（走到后自动停下）。</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            if (!allowClickToMove) return;
            Vector3 p = worldPosition;
            p.y = transform.position.y;
            _clickTarget = p;
            _hasClickTarget = true;
        }

        public void Stop()
        {
            _hasClickTarget = false;
            _planarVelocity = Vector3.zero;
        }

        void Update()
        {
            if (!_inputEnabled)
            {
                _planarVelocity = Vector3.zero;
                ApplyGravityOnly();
                return;
            }

            Vector3 desired = Vector3.zero;
            Vector3 inputDir = ReadKeyboard();

            if (inputDir.sqrMagnitude > 0.0001f)
            {
                _hasClickTarget = false;
                desired = inputDir.normalized * moveSpeed;
            }
            else if (_hasClickTarget)
            {
                Vector3 to = _clickTarget - transform.position;
                to.y = 0f;
                if (to.magnitude > arriveDistance)
                    desired = to.normalized * moveSpeed;
                else
                    _hasClickTarget = false;
            }

            // 简单的加减速，避免"机器人式"瞬移感
            float accel = desired.sqrMagnitude > 0.0001f ? 14f : 18f;
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desired, accel * Time.deltaTime);

            ApplyGravity();

            if (_planarVelocity.sqrMagnitude > 0.05f)
                FaceDirection(_planarVelocity);
        }

        Vector3 ReadKeyboard()
        {
            if (!allowKeyboard) return Vector3.zero;

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return Vector3.zero;

            Vector3 f = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            Vector3 r = cameraTransform != null ? cameraTransform.right : Vector3.right;
            f.y = 0f; r.y = 0f;
            f = f.sqrMagnitude > 0.0001f ? f.normalized : Vector3.forward;
            r = r.sqrMagnitude > 0.0001f ? r.normalized : Vector3.right;

            return f * v + r * h;
        }

        void FaceDirection(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, rotateSpeed * Time.deltaTime);
        }

        void ApplyGravity()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = _planarVelocity;
            motion.y = _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }

        void ApplyGravityOnly()
        {
            if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            else _verticalVelocity += gravity * Time.deltaTime;
            _cc.Move(new Vector3(0f, _verticalVelocity, 0f) * Time.deltaTime);
        }
    }
}