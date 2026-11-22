using UnityEngine;

namespace Move1024
{
    /// <summary>铁盒：点击一次播"四位数的密码锁……"的 OS（有冷却，别刷屏）。</summary>
    public class IronBoxInteract : MonoBehaviour
    {
        [Header("引用")]
        public CrosshairUI crosshair;
        public GameFlowDirector director;
        public PromptSource prompt;
        public AudioDirector audio;

        [Header("规则")]
        public float maxDistance = 2.5f;
        public float repeatCooldown = 6f;

        float _nextAllowed;

        void OnEnable()
        {
            if (prompt != null) prompt.SetHint(DialogueConfig.Get(DialogueKeys.BoxViewPrompt, "[点击 查看铁盒]"));
        }

        void Update()
        {
            if (Time.time < _nextAllowed) return;
            if (!Input.GetMouseButtonDown(0)) return;

            if (crosshair == null || crosshair.HoveredCollider == null) return;
            if (!crosshair.HoveredCollider.transform.IsChildOf(transform)) return;

            if (director != null && director.player != null &&
                Vector3.Distance(director.player.transform.position, transform.position) > maxDistance) return;

            _nextAllowed = Time.time + repeatCooldown;
            if (audio != null) audio.PlayMetal();
            if (director != null) director.OnIronBoxViewed();
        }
    }
}
