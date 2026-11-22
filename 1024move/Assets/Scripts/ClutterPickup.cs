using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 地上的旧物：左键拾取 → 走到纸箱前左键放进纸箱。
    /// 连续放进 2 件后播一句 OS（文案在 Dialogue_1024.json）。
    /// </summary>
    public class ClutterPickup : MonoBehaviour
    {
        [Header("引用")]
        public CrosshairUI crosshair;
        public GameFlowDirector director;
        public Transform player;
        public Transform holdAnchor;
        public Transform storeTarget;     // 纸箱
        public PromptSource prompt;
        public AudioDirector audio;

        [Header("规则")]
        public float maxPickupDistance = 2.5f;
        public Vector3 holdLocalPosition = new Vector3(0.20f, -0.24f, 0.55f);
        public float holdScale = 0.55f;

        bool _carried;
        bool _stored;
        Collider[] _colliders;

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        void Start()
        {
            if (prompt != null) prompt.SetHint(DialogueConfig.Get(DialogueKeys.ClutterPickupPrompt, "[左键 拾取旧物]"));
        }

        void Update()
        {
            if (_stored) return;

            if (_carried)
            {
                bool aimingBox = IsAimingStoreTarget();
                CrosshairUI ui = crosshair != null ? crosshair : CrosshairUI.Active;
                if (ui != null)
                    ui.SetGlobalPrompt(aimingBox ? DialogueConfig.Get(DialogueKeys.ClutterStorePrompt, "[左键 放进纸箱]") : "");

                if (aimingBox && Input.GetMouseButtonDown(0)) Store();
                return;
            }

            if (!IsHovered()) return;
            if (player != null && Vector3.Distance(player.position, transform.position) > maxPickupDistance) return;

            if (Input.GetMouseButtonDown(0)) PickUp();
        }

        bool IsHovered()
        {
            return crosshair != null && crosshair.HoveredCollider != null &&
                   crosshair.HoveredCollider.transform.IsChildOf(transform);
        }

        bool IsAimingStoreTarget()
        {
            if (storeTarget == null) return false;
            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.transform.IsChildOf(storeTarget);
        }

        void PickUp()
        {
            _carried = true;

            if (holdAnchor != null)
            {
                transform.SetParent(holdAnchor, true);
                transform.localPosition = holdLocalPosition;
                transform.localRotation = Quaternion.Euler(10f, 20f, -6f);
                transform.localScale = Vector3.one * holdScale;
            }

            SetColliders(false);
            if (prompt != null) prompt.Clear();
            if (audio != null) audio.PlayClick();
        }

        void Store()
        {
            _stored = true;
            _carried = false;

            CrosshairUI ui = crosshair != null ? crosshair : CrosshairUI.Active;
            if (ui != null) ui.SetGlobalPrompt("");

            if (audio != null) audio.PlayClick();
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
