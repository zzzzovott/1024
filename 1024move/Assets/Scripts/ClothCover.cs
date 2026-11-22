using System.Collections;
using UnityEngine;

namespace Move1024
{
    /// <summary>盖在铁盒上的旧衣服：左键挪开，露出铁盒并播金属声 + OS。</summary>
    public class ClothCover : MonoBehaviour
    {
        [Header("引用")]
        public CrosshairUI crosshair;
        public GameFlowDirector director;
        public PromptSource prompt;
        public AudioDirector audio;
        [Tooltip("揭开后要启用的铁盒交互")]
        public IronBoxInteract ironBox;

        [Header("规则")]
        public float maxDistance = 2.5f;
        public Vector3 moveOffset = new Vector3(-0.35f, 0.02f, 0.12f);
        public float moveTime = 0.45f;

        bool _moved;

        void Start()
        {
            if (prompt != null) prompt.SetHint(DialogueConfig.Get(DialogueKeys.ClothPrompt, "[左键 掀开旧衣服]"));
        }

        void Update()
        {
            if (_moved) return;

            // 阶段锁：只有"发现铁盒"阶段才能掀衣服
            if (director != null && !director.CanDiscoverBox)
            {
                if (prompt != null) prompt.Clear();
                return;
            }

            if (prompt != null && string.IsNullOrEmpty(prompt.hint))
                prompt.SetHint(DialogueConfig.Get(DialogueKeys.ClothPrompt, "[左键 掀开旧衣服]"));

            if (!Input.GetMouseButtonDown(0)) return;

            if (crosshair == null || crosshair.HoveredCollider == null) return;
            if (!crosshair.HoveredCollider.transform.IsChildOf(transform)) return;

            Camera cam = Camera.main;
            if (cam != null && director != null && director.player != null &&
                Vector3.Distance(director.player.transform.position, transform.position) > maxDistance) return;

            MoveAway();
        }

        void MoveAway()
        {
            _moved = true;

            if (prompt != null) prompt.Clear();

            Collider[] cols = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) if (cols[i] != null) cols[i].enabled = false;

            StartCoroutine(MoveRoutine());

            if (audio != null) audio.PlayMetal();
            if (director != null) director.SayDialogue(DialogueKeys.ClothMoved);
            if (ironBox != null) ironBox.enabled = true;
        }

        IEnumerator MoveRoutine()
        {
            Vector3 from = transform.localPosition;
            Vector3 to = from + moveOffset;
            float t = 0f;

            while (t < moveTime)
            {
                t += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, t / moveTime);
                transform.localPosition = Vector3.Lerp(from, to, u);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -25f, u));
                yield return null;
            }

            transform.localPosition = to;
        }
    }
}
