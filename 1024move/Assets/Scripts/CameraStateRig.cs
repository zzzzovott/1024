using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 零依赖的镜头状态机：在"房间固定机位"和"桌面特写"之间平滑推拉。
    /// 不依赖 Cinemachine，先跑通再决定要不要引入（Cinemachine 在 2020.3 里是独立包，能省则省）。
    /// 用法：把本脚本挂在 Main Camera 上，摆好两个 Anchor（空物体）即可。
    /// </summary>
    public class CameraStateRig : MonoBehaviour
    {
        [Header("两个机位锚点")]
        public Transform roomAnchor;
        public Transform deskAnchor;

        [Header("过渡")]
        [Tooltip("位置/视场角平滑时间，越大越慢")]
        public float smoothTime = 0.55f;
        [Tooltip("旋转插值速度")]
        public float rotateLerpSpeed = 5.5f;

        [Header("视场角")]
        public float roomFov = 42f;
        public float deskFov = 30f;

        [Header("房间视角轻微跟随（0 = 完全静止）")]
        public Transform playerFollow;
        [Range(0f, 0.6f)] public float followWeight = 0.14f;
        [Tooltip("跟随位移的最大幅度（米）")]
        public float followClamp = 0.9f;

        [Header("环视（配合屏幕正中心准星）")]
        [Tooltip("房间状态下允许小幅左右/上下看。关掉就是纯固定机位——那样屏幕中心永远只指着桌前一小块，电视之类的偏侧物件会瞄不到")]
        public bool lookAround = true;
        public float lookYawRange = 24f;
        public float lookPitchRange = 14f;
        public float lookSensitivity = 2.4f;
        public float lookSmooth = 9f;
        [Tooltip("按住左键时冻结环视：擦灰/拖拽要鼠标自由移动，不能同时带镜头")]
        public bool freezeLookWhileDragging = true;

        Camera _cam;
        Vector3 _posVelocity;
        Vector3 _pivotVelocity;
        float _fovVelocity;
        bool _atDesk;
        bool _useDynamic;
        bool _snapNext;
        Vector3 _pivotOffset;
        Transform _dynamicAnchor;
        float _lookYaw, _lookPitch, _lookYawSm, _lookPitchSm;

        public bool AtDesk { get { return _atDesk; } }

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = GetComponentInChildren<Camera>();
        }

        void Start()
        {
            if (roomAnchor != null)
            {
                transform.position = roomAnchor.position;
                transform.rotation = roomAnchor.rotation;
            }
            if (_cam != null) _cam.fieldOfView = roomFov;
            _pivotOffset = Vector3.zero;
        }

        /// <summary>推到固定的桌面机位。</summary>
        public void GoDesk()
        {
            _useDynamic = false;
            _atDesk = true;
        }

        /// <summary>
        /// 推到"以某件旧物为中心"的特写机位。
        /// 只有一个固定桌面机位的话，三个物件必然有两个被裁出画面；
        /// 每件旧物单独对焦才是"桌面特写"该有的样子。
        /// </summary>
        public void GoDesk(Vector3 focusWorldPos)
        {
            if (_dynamicAnchor == null)
            {
                GameObject go = new GameObject("Anchor_DeskDynamic");
                _dynamicAnchor = go.transform;
            }
            _dynamicAnchor.position = focusWorldPos + new Vector3(0f, 0.50f, -1.40f);
            _dynamicAnchor.rotation = Quaternion.Euler(12f, 0f, 0f);
            _useDynamic = true;
            _atDesk = true;
        }

        public void GoRoom()
        {
            _atDesk = false;
            _useDynamic = false;
        }

        /// <summary>在白幕盖住的那一帧直接切到特写，玩家看不到推镜过程。</summary>
        public void SnapDesk(Vector3 focusWorldPos)
        {
            GoDesk(focusWorldPos);
            _posVelocity = Vector3.zero;
            _pivotVelocity = Vector3.zero;
            _fovVelocity = 0f;
            _snapNext = true;
        }

        /// <summary>白幕盖住时直接切回全景。</summary>
        public void SnapRoom()
        {
            GoRoom();
            _posVelocity = Vector3.zero;
            _pivotVelocity = Vector3.zero;
            _fovVelocity = 0f;
            _snapNext = true;
        }

        void LateUpdate()
        {
            Transform target = _atDesk
                ? (_useDynamic && _dynamicAnchor != null ? _dynamicAnchor : deskAnchor)
                : roomAnchor;
            if (target == null) return;

            Vector3 wantPos = target.position;
            if (!_atDesk && playerFollow != null && roomAnchor != null)
            {
                Vector3 offset = playerFollow.position - roomAnchor.position;
                offset.y = 0f;
                wantPos += Vector3.ClampMagnitude(offset * followWeight, followClamp);
            }

            // 桌面机位时用"看向桌子中心"的偏移，保证物件不会跑出画面
            _pivotOffset = Vector3.SmoothDamp(_pivotOffset, Vector3.zero, ref _pivotVelocity, 0.3f);
            wantPos += _pivotOffset;

            UpdateLook();
            Quaternion wantRot = target.rotation;
            if (lookAround && !_atDesk)
                wantRot = target.rotation * Quaternion.Euler(_lookPitchSm, _lookYawSm, 0f);

            if (_snapNext)
            {
                transform.position = wantPos;
                transform.rotation = wantRot;
                if (_cam != null) _cam.fieldOfView = _atDesk ? deskFov : roomFov;
                _snapNext = false;
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, wantPos, ref _posVelocity, smoothTime);
            float k = 1f - Mathf.Exp(-rotateLerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, k);

            if (_cam != null)
            {
                _cam.fieldOfView = Mathf.SmoothDamp(
                    _cam.fieldOfView,
                    _atDesk ? deskFov : roomFov,
                    ref _fovVelocity,
                    smoothTime);
            }
        }

        /// <summary>房间状态下的环视偏移：鼠标移动带动镜头小幅转动，屏幕中心准星才有东西可瞄。</summary>
        void UpdateLook()
        {
            if (lookAround && !_atDesk)
            {
                bool frozen = freezeLookWhileDragging && Input.GetMouseButton(0);
                if (!frozen)
                {
                    _lookYaw = Mathf.Clamp(_lookYaw + Input.GetAxisRaw("Mouse X") * lookSensitivity,
                        -lookYawRange, lookYawRange);
                    _lookPitch = Mathf.Clamp(_lookPitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity,
                        -lookPitchRange, lookPitchRange);
                }
            }

            float k = 1f - Mathf.Exp(-lookSmooth * Time.deltaTime);
            _lookYawSm = Mathf.Lerp(_lookYawSm, _lookYaw, k);
            _lookPitchSm = Mathf.Lerp(_lookPitchSm, _lookPitch, k);
        }

        /// <summary>可选：让桌面视角轻微把某个物件居中（用于物件不在正中时）。</summary>
        public void NudgePivot(Vector3 worldOffset)
        {
            _pivotOffset = worldOffset;
            _pivotVelocity = Vector3.zero;
        }

        /// <summary>给摇镜/演出用：临时查看到指定世界坐标。</summary>
        public void LookFrom(Vector3 position, Quaternion rotation, float duration = 0.8f)
        {
            StartCoroutine(BlendTo(position, rotation, duration));
        }

        System.Collections.IEnumerator BlendTo(Vector3 position, Quaternion rotation, float duration)
        {
            Vector3 p0 = transform.position;
            Quaternion r0 = transform.rotation;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                transform.position = Vector3.Lerp(p0, position, u);
                transform.rotation = Quaternion.Slerp(r0, rotation, u);
                yield return null;
            }
        }
    }
}