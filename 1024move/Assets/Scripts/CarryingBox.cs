using UnityEngine;

namespace Move1024
{
    /// <summary>开局抱着的搬家纸箱：太重会催你放下，对准空地/桌面点击左键放下。</summary>
    public class CarryingBox : MonoBehaviour
    {
        [Header("引用")]
        public GameFlowDirector director;
        public FPSController player;
        public Transform holdAnchor;
        public AudioDirector audio;

        [Header("持握")]
        public Vector3 holdLocalPosition = new Vector3(0.02f, -0.30f, 1.22f);
        public float holdScale = 1.25f;

        [Header("规则")]
        public float heavyHintDelay = 5f;     // 抱着 5 秒还没放下就催一句
        public float maxDropDistance = 4f;

        bool _held = true;
        bool _dropped;
        bool _heavySaid;
        float _heldTime;
        bool _hasDropTarget;
        Vector3 _dropPoint;
        Quaternion _dropRot;
        Collider[] _colliders;

        void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        void Start()
        {
            if (holdAnchor != null)
            {
                transform.SetParent(holdAnchor, true);
                transform.localPosition = holdLocalPosition;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one * holdScale;
            }

            SetColliders(false);
        }

        void Update()
        {
            if (_dropped) return;

            if (player != null && player.InputEnabled) _heldTime += Time.deltaTime;

            UpdateDropTarget();

            CrosshairUI crosshair = CrosshairUI.Active;
            if (crosshair != null && _held)
                crosshair.SetGlobalPrompt(_hasDropTarget ? DialogueConfig.Get(DialogueKeys.BoxDropPrompt, "[点击 放下箱子]") : "");

            if (_heldTime >= heavyHintDelay && !_heavySaid)
            {
                _heavySaid = true;
                if (director != null) director.SayDialogue(DialogueKeys.BoxHeavy);
            }

            if (Input.GetMouseButtonDown(0) && _hasDropTarget) Drop();
        }

        void UpdateDropTarget()
        {
            _hasDropTarget = false;

            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, maxDropDistance, ~0, QueryTriggerInteraction.Ignore)) return;

            if (hit.normal.y < 0.5f) return;                                  // 只放水平面
            if (player != null && hit.collider.transform.IsChildOf(player.transform)) return;

            _dropPoint = hit.point + hit.normal * 0.27f;                       // 箱子有厚度
            _dropRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
            _hasDropTarget = true;
        }

        void Drop()
        {
            if (_dropped) return;
            _dropped = true;
            _held = false;

            transform.SetParent(null, true);                                   // 回到场景根
            transform.position = _dropPoint;
            transform.rotation = _dropRot;
            transform.localScale = Vector3.one;   // 放下就是真实尺寸（约 0.5 米见方）

            SetColliders(true);

            CrosshairUI crosshair = CrosshairUI.Active;
            if (crosshair != null) crosshair.SetGlobalPrompt("");

            if (audio != null) audio.PlayBoxDrop();
            if (director != null) director.OnBoxDropped();   // 推进到"整理杂物"阶段
        }

        void SetColliders(bool enabled)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = enabled;
        }
    }
}
