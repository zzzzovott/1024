using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 地上的一件杂物（和抹布同一套手感）：
    /// · 准星对准 → [F 拾取杂物] → 拿到镜头前;
    /// · 拿着时对准大纸箱 → [F 扔进箱子]（算作收纳成功）;
    /// · 拿着时没对准纸箱按 F → [F 放下]，落在准星指着的桌面/地面，还能再捡。
    ///
    /// 只有"整理杂物"阶段可用（主线状态机锁）。
    /// </summary>
    public class ClutterPickup : MonoBehaviour
    {
        [Header("引用")]
        public CrosshairUI crosshair;
        public GameFlowDirector director;
        public Transform player;
        public Transform holdAnchor;
        public Transform storeTarget;     // 纸箱 A
        public Transform storeTarget2;    // 纸箱 B（开局放下的大纸箱）
        public PromptSource prompt;
        public AudioDirector audio;

        [Header("规则")]
        public float maxPickupDistance = 2.5f;
        public Vector3 holdLocalPosition = new Vector3(0.20f, -0.24f, 0.55f);
        [Tooltip("拿起时在原始尺寸上的倍率（1 = 保持原大小）")]
        public float holdScale = 1f;
        public float maxPlaceDistance = 4f;

        bool _carried;
        bool _stored;
        Collider[] _colliders;
        Vector3 _baseScale = Vector3.one;

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
            _baseScale = transform.localScale;   // 记住这件杂物原本的尺寸
        }

        void Start()
        {
            RestorePrompt();
        }

        void Update()
        {
            if (_stored) return;

            // 阶段锁：只有"整理杂物"阶段才能捡 / 扔
            if (director != null && !director.CanTidy)
            {
                if (prompt != null) prompt.Clear();
                return;
            }

            // 阶段推进/放下之后要把提示补回来，否则准星永远认不出这个物体
            if (!_carried) RestorePrompt();

            if (_carried)
            {
                if (HandleCarrying()) return;
                return;
            }

            if (!IsHovered()) return;
            if (player != null && Vector3.Distance(player.position, transform.position) > maxPickupDistance) return;

            if (Input.GetKeyDown(KeyCode.F)) PickUp();
        }

        void RestorePrompt()
        {
            if (prompt == null) return;
            if (string.IsNullOrEmpty(prompt.hint))
                prompt.SetHint(DialogueConfig.Get(DialogueKeys.ClutterPickupPrompt, "[F 拾取杂物]"));
        }

        bool HandleCarrying()
        {
            bool aimingBox = IsAimingStoreTarget();

            CrosshairUI ui = crosshair != null ? crosshair : CrosshairUI.Active;
            if (ui != null)
            {
                ui.SetGlobalPrompt(aimingBox
                    ? DialogueConfig.Get(DialogueKeys.ClutterStorePrompt, "[F 扔进箱子]")
                    : DialogueConfig.Get(DialogueKeys.ClutterDropPrompt, "[F 放下]"));
            }

            if (!Input.GetKeyDown(KeyCode.F)) return false;

            if (aimingBox) Store();
            else Drop();
            return true;
        }

        bool IsHovered()
        {
            return crosshair != null && crosshair.HoveredCollider != null &&
                   crosshair.HoveredCollider.transform.IsChildOf(transform);
        }

        bool IsAimingStoreTarget()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            // 用球扫描（和准星同款宽容度），稍微瞄偏一点也算对准箱子
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.SphereCast(ray, 0.25f, out hit, 6f, ~0, QueryTriggerInteraction.Ignore)) return false;

            if (storeTarget != null && hit.collider.transform.IsChildOf(storeTarget)) return true;
            if (storeTarget2 != null && hit.collider.transform.IsChildOf(storeTarget2)) return true;
            return false;
        }

        void PickUp()
        {
            _carried = true;

            if (holdAnchor != null)
            {
                transform.SetParent(holdAnchor, true);
                transform.localPosition = holdLocalPosition;
                transform.localRotation = Quaternion.Euler(10f, 20f, -6f);
                // 关键：在原始尺寸上乘系数，而不是直接写死一个绝对缩放
                transform.localScale = _baseScale * holdScale;
            }

            SetColliders(false);
            if (prompt != null) prompt.Clear();
            if (audio != null) audio.PlayPickup();
        }

        void Drop()
        {
            _carried = false;

            transform.SetParent(null, true);
            transform.localScale = _baseScale;

            Vector3 pos;
            Quaternion rot;
            if (TryFindPlace(out pos, out rot))
            {
                transform.position = pos;
                transform.rotation = rot;
            }
            else
            {
                Vector3 fwd = player != null ? player.forward : Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                pos = (player != null ? player.position : transform.position) + fwd.normalized * 0.45f;
                pos.y = 0.06f;
                transform.position = pos;
                transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            }

            SetColliders(true);
            RestorePrompt();

            CrosshairUI ui = crosshair != null ? crosshair : CrosshairUI.Active;
            if (ui != null) ui.SetGlobalPrompt("");

            if (audio != null) audio.PlayPickup();
        }

        bool TryFindPlace(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxPlaceDistance, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.5f) return false;
            if (player != null && hit.collider.transform.IsChildOf(player)) return false;
            if (hit.collider.transform.IsChildOf(transform)) return false;

            position = hit.point + hit.normal * 0.08f;
            rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                       Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            return true;
        }

        void Store()
        {
            _stored = true;
            _carried = false;

            CrosshairUI ui = crosshair != null ? crosshair : CrosshairUI.Active;
            if (ui != null) ui.SetGlobalPrompt("");

            if (audio != null) audio.PlayStore();
            if (director != null) director.OnClutterStored();

            gameObject.SetActive(false);
        }

        void SetColliders(bool enabled)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = enabled;
        }
    }
}
