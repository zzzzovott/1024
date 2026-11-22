using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 抹布：擦灰的前置道具。可以反复拿起 / 放下。
    ///
    /// · 地上：准星对准 → [F] 拾取抹布 → 抹布被拿到镜头前（贴着右下角）；
    /// · 手里：准星没对着别的旧物时按 F → [F] 放下抹布 → 掉在脚前地面上，还能再捡；
    /// · 只有拿在手里时 DustErase 才允许擦灰。
    /// </summary>
    public class RagPickup : MonoBehaviour
    {
        [Header("引用")]
        public CrosshairUI crosshair;
        public Transform player;
        [Tooltip("持握点：通常挂在 Main Camera 下，抹布会被拿在这里")]
        public Transform holdAnchor;
        public AudioDirector audio;

        [Header("规则")]
        public float maxPlayerDistance = 2.5f;
        public string hoverHint = "[F] 拾取抹布";
        public string dropHint = "[F] 放下抹布";
        [Tooltip("拿在手里的偏移（相对持握点）：正前方 = 准心位置")]
        public Vector3 holdLocalPosition = new Vector3(0f, 0f, 0.55f);
        public Vector3 holdLocalEuler = new Vector3(8f, 18f, -4f);
        public float holdScale = 0.34f;

        /// <summary>拿起 / 放下（流程脚本用来播提示）。</summary>
        public event Action PickedUp;
        public event Action Dropped;

        public bool IsHeld { get; private set; }

        Transform _homeParent;
        Vector3 _homePos;
        Quaternion _homeRot;
        Collider[] _colliders;

        void Awake()
        {
            _homeParent = transform.parent;
            _homePos = transform.position;
            _homeRot = transform.rotation;
            _colliders = GetComponentsInChildren<Collider>(true);
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.F)) return;

            if (IsHeld)
            {
                // 手里有布时，准星正对着别的旧物就不抢它的 F 键
                if (crosshair != null && crosshair.HoveredInteractable != null) return;
                Drop();
                return;
            }

            if (!IsHovered()) return;
            if (player != null && Vector3.Distance(player.position, transform.position) > maxPlayerDistance) return;

            PickUp();
        }

        bool IsHovered()
        {
            return crosshair != null && crosshair.HoveredCollider != null &&
                   crosshair.HoveredCollider.transform.IsChildOf(transform);
        }

        public void PickUp()
        {
            if (IsHeld) return;
            IsHeld = true;

            if (holdAnchor != null)
            {
                transform.SetParent(holdAnchor, true);
                transform.localPosition = holdLocalPosition;
                transform.localRotation = Quaternion.Euler(holdLocalEuler);
                transform.localScale = Vector3.one * holdScale;
            }

            // 举到准心上会正好挡住中心射线（擦灰/瞄物件都会先打到布），拿在手里时先关掉碰撞
            SetColliders(false);

            if (audio != null) audio.PlayClick();
            if (PickedUp != null) PickedUp();
        }

        public void Drop()
        {
            if (!IsHeld) return;
            IsHeld = false;

            // 关键：必须换回原来的父节点。Rag 是场景根节点，父节点就是 null，
            // SetParent(null) 才是"回到场景里"；否则它会一直挂在相机下面跟着镜头跑。
            transform.SetParent(_homeParent, true);
            transform.localScale = Vector3.one;

            // 放在准星打到的那个面上：地面、桌面都行；打不到就丢在脚前地上
            Vector3 dropPos;
            Quaternion dropRot;

            if (TryGetDropPoint(out dropPos, out dropRot))
            {
                transform.position = dropPos;
                transform.rotation = dropRot;
            }
            else
            {
                Vector3 fwd = player != null ? player.forward : Vector3.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;

                dropPos = (player != null ? player.position : _homePos) + fwd.normalized * 0.55f;
                dropPos.y = 0.02f;
                transform.position = dropPos;
                transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            }

            SetColliders(true);

            if (audio != null) audio.PlayClick();
            if (Dropped != null) Dropped();
        }

        /// <summary>用准星射线找一个能放下的面（桌面上、地上都能放）。</summary>
        bool TryGetDropPoint(out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;

            Camera cam = Camera.main;
            if (cam == null) return false;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return false;

            // 别把布放在玩家自己身上
            if (player != null && hit.collider.transform.IsChildOf(player)) return false;
            if (hit.collider.transform.IsChildOf(transform)) return false;

            // 只接受偏水平的承接面（地面、桌面）；墙面之类退回"丢脚前"
            if (hit.normal.y < 0.5f) return false;

            position = hit.point + hit.normal * 0.02f;
            rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                       Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            return true;
        }

        /// <summary>读档用：恢复成"拿在手里"或"放在原地"。</summary>
        public void RestoreHeld(bool held)
        {
            if (held) PickUp();
            else
            {
                IsHeld = false;
                transform.SetParent(_homeParent, true);   // null = 回到场景根
                transform.position = _homePos;
                transform.rotation = _homeRot;
                transform.localScale = Vector3.one;
                SetColliders(true);
            }
        }

        void SetColliders(bool enabled)
        {
            if (_colliders == null) return;
            for (int i = 0; i < _colliders.Length; i++)
                if (_colliders[i] != null) _colliders[i].enabled = enabled;
        }
    }
}
