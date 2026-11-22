using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 标准第一人称控制器：WASD 自由行走 + 鼠标 360° 视角。
    ///
    /// 结构要求（GameSceneBuilder 已按这个结构生成）：
    ///   Player            ← 本脚本 + CharacterController，负责水平朝向(yaw)
    ///     ├─ Body         ← 胶囊网格（纯视觉，无碰撞）
    ///     └─ Main Camera  ← 子物体，负责俯仰(pitch)
    ///
    /// 这样人走到哪，镜头就跟到哪；鼠标左右转身体、上下抬低头，互不干扰。
    /// 重力用 CharacterController 自己算，不挂 Rigidbody。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [Header("移动")]
        public float walkSpeed = 3.2f;
        public float sprintSpeed = 5.4f;
        public float acceleration = 18f;
        public float gravity = -22f;
        public float jumpHeight = 1.0f;
        [Tooltip("空中允许的操控比例（0 = 完全不能改方向）")]
        [Range(0f, 1f)] public float airControl = 0.35f;

        [Header("视角")]
        [Tooltip("俯仰用的相机（必须是本物体的子物体；留空自动取 Camera.main）")]
        public Transform cameraPivot;
        [Tooltip("鼠标灵敏度（度/单位鼠标位移）")]
        public float mouseSensitivity = 2.2f;
        [Tooltip("俯仰平滑速度，越小越飘")]
        public float lookSmooth = 20f;
        public float minPitch = -85f;
        public float maxPitch = 85f;

        [Header("按键")]
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode jumpKey = KeyCode.Space;
        [Tooltip("按住这个键临时解锁鼠标（点 UI / 菜单）；松开自动回到锁定视角")]
        public KeyCode freeCursorKey = KeyCode.LeftControl;

        [Header("光标")]
        public bool lockCursor = true;

        [Header("运行时（只读）")]
        [SerializeField] bool inputEnabled = true;
        [SerializeField] bool moveEnabled = true;
        [SerializeField] bool lookEnabled = true;

        CharacterController _cc;
        float _yaw;
        float _pitch;
        float _pitchSm;
        Vector3 _planarVelocity;
        float _verticalVelocity;
        Vector3 _autoTarget;
        bool _hasAutoTarget;
        bool _freeCursor;

        /// <summary>整体输入开关（菜单 / 开场 / 终结演出时关掉）。</summary>
        public bool InputEnabled { get { return inputEnabled; } }
        public float Yaw { get { return _yaw; } }
        public float Pitch { get { return _pitch; } }
        public bool IsCursorLocked { get { return Cursor.lockState == CursorLockMode.Locked; } }
        /// <summary>玩家是不是正按住"呼出鼠标"键（Ctrl）。</summary>
        public bool IsFreeCursorHeld { get { return _freeCursor; } }
        public bool IsMoving { get { return _hasAutoTarget || _planarVelocity.sqrMagnitude > 0.04f; } }

        /// <summary>水平移动速度（米/秒），给双手摆动之类的表现用。</summary>
        public float PlanarSpeed { get { return _planarVelocity.magnitude; } }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null)
                cameraPivot = Camera.main.transform;
            _yaw = transform.eulerAngles.y;
        }

        void Start()
        {
            if (inputEnabled) LockCursor();
            else UnlockCursor();
        }

        // ---------------------------------------------------------------- 对外开关

        public void SetInputEnabled(bool value)
        {
            inputEnabled = value;
            if (!value)
            {
                _hasAutoTarget = false;
                _planarVelocity = Vector3.zero;
                UnlockCursor();
            }
            else
            {
                LockCursor();
            }
        }

        /// <summary>只关移动，视角还能转（过场里用）。</summary>
        public void SetMoveEnabled(bool value)
        {
            moveEnabled = value;
            if (!value) _planarVelocity = Vector3.zero;
        }

        /// <summary>只关视角，鼠标留给拖拽解谜用（桌面特写）。</summary>
        public void SetLookEnabled(bool value)
        {
            lookEnabled = value;
        }

        public void LockCursor()
        {
            if (!lockCursor) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---------------------------------------------------------------- 兼容旧接口

        /// <summary>让角色自动走到某点（保留给流程脚本用；FPS 下一般不用）。</summary>
        public void MoveTo(Vector3 worldPosition)
        {
            if (!inputEnabled || !moveEnabled) return;
            _autoTarget = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
            _hasAutoTarget = true;
        }

        public void Stop()
        {
            _hasAutoTarget = false;
            _planarVelocity = Vector3.zero;
        }

        /// <summary>读档用：直接把人放到某个位置和朝向上。</summary>
        public void Teleport(Vector3 position, float yaw, float pitch)
        {
            _hasAutoTarget = false;
            _planarVelocity = Vector3.zero;
            _verticalVelocity = 0f;

            // CharacterController 会挡住直接改 transform，先关掉再移动
            if (_cc != null) _cc.enabled = false;
            transform.position = position;
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            _pitchSm = _pitch;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitchSm, 0f, 0f);
            if (_cc != null) _cc.enabled = true;
        }

        // ---------------------------------------------------------------- 每帧

        void Update()
        {
            HandleCursorToggle();

            if (!inputEnabled)
            {
                ApplyGravity(Vector3.zero);
                return;
            }

            Look();
            Vector3 desired = moveEnabled ? ReadMoveInput() : Vector3.zero;
            Move(desired);
        }

        void HandleCursorToggle()
        {
            // 按住 Ctrl 临时解锁鼠标，方便点 UI；松开立刻回到锁定视角。
            // Esc 留给暂停菜单，不再在这里处理。
            bool ctrl = Input.GetKey(freeCursorKey);
            if (ctrl == _freeCursor) return;

            _freeCursor = ctrl;
            if (ctrl) UnlockCursor();
            else if (inputEnabled) LockCursor();
        }

        void Look()
        {
            if (!lookEnabled || !IsCursorLocked || _freeCursor) return;

            float mx = Input.GetAxisRaw("Mouse X") * mouseSensitivity;
            float my = Input.GetAxisRaw("Mouse Y") * mouseSensitivity;

            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch - my, minPitch, maxPitch);

            // 身体只转 yaw，相机只转 pitch：这样上下看不会把角色带歪
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            float k = 1f - Mathf.Exp(-lookSmooth * Time.deltaTime);
            _pitchSm = Mathf.Lerp(_pitchSm, _pitch, k);
            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(_pitchSm, 0f, 0f);
        }

        Vector3 ReadMoveInput()
        {
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            if (_hasAutoTarget)
            {
                Vector3 to = _autoTarget - transform.position;
                to.y = 0f;
                if (to.magnitude < 0.15f) _hasAutoTarget = false;
                else if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f) _hasAutoTarget = false;
                else return to.normalized * walkSpeed;
            }

            if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f) return Vector3.zero;

            // 输入方向永远相对身体朝向（鼠标转视角 → 走的方向跟着变）
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 dir = yawRot * new Vector3(h, 0f, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float speed = Input.GetKey(sprintKey) ? sprintSpeed : walkSpeed;
            return dir * speed;
        }

        void Move(Vector3 desired)
        {
            float accel = _cc.isGrounded ? acceleration : acceleration * Mathf.Max(0.05f, airControl);
            _planarVelocity = Vector3.MoveTowards(_planarVelocity, desired, accel * Time.deltaTime);

            if (_cc.isGrounded && Input.GetKeyDown(jumpKey) && moveEnabled)
                _verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);

            ApplyGravity(_planarVelocity);
        }

        void ApplyGravity(Vector3 planar)
        {
            if (_cc.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = planar;
            motion.y = _verticalVelocity;
            _cc.Move(motion * Time.deltaTime);
        }
    }
}
