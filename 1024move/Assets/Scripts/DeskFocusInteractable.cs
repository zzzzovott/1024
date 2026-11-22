using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 桌上的一件"旧物"：把物件、它的解谜组件、它的记忆片段、它代表的密码位绑在一起。
    /// 策划把三个物件各挂一个，填 4 个字段就完事。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DeskFocusInteractable : MonoBehaviour
    {
        [Header("身份")]
        [Tooltip("物件名（UI 提示用，比如：生锈的铁皮青蛙）")]
        public string displayName = "旧物";
        [Tooltip("解开后填入第几位密码（0 = 第一位）")]
        public int passwordSlot = 0;
        [Tooltip("填入的数字")]
        public string digit = "1";
        [Tooltip("拿起它时主角的内心 OS（打字机），留空则不播")]
        [TextArea(2, 4)] public string osLine = "";
        [Tooltip("准星指上来时显示的操作提示")]
        public string hoverHint = "点击查看";
        [Tooltip("进入特写后准星上显示的操作提示")]
        public string deskHint = "按住左键拖动";

        [Header("解谜组件（三选一）")]
        public ShadowAlignPuzzle anglePuzzle;
        public SilhouetteMatchPuzzle silhouettePuzzle;
        [Tooltip("画圈倒带（磁带关），填了就优先用这个")]
        public CircularRewind rewindPuzzle;

        [Header("手感与演出")]
        public DragRotateBody rotator;
        public MemoryCuePlayer memoryCue;
        [Tooltip("桌面/墙上的光斑（进度越高越亮越大），可留空")]
        public Renderer lightSpot;
        [Tooltip("墙上的剪影投影（实时影子 + 目标轮廓），可留空")]
        public SilhouetteProjection projection;

        [Header("悬停反馈")]
        [Tooltip("鼠标扫过时放大的比例，用来告诉玩家这个能点")]
        public float hoverScale = 1.06f;
        public float hoverLerpSpeed = 7f;

        [Header("运行时（只读）")]
        [SerializeField] bool puzzleSolvedFlag;
        [SerializeField] float progress;

        /// <summary>解谜成功时触发（Director 监听）。</summary>
        public event Action<DeskFocusInteractable> PuzzleSolved;
        /// <summary>进度 0~1，用来驱动光斑亮度、UI 呼吸等。</summary>
        public event Action<float> ProgressChanged;

        public bool PuzzleSolvedFlag { get { return puzzleSolvedFlag; } }
        public float Progress { get { return progress; } }

        Vector3 _spotBaseScale = Vector3.one;
        bool _spotInit;
        bool _hover;
        float _hoverT;

        /// <summary>由 Director 每帧根据鼠标射线设置。</summary>
        public void SetHover(bool value)
        {
            _hover = value;
        }

        void Update()
        {
            // 悬停时微微放大：这是"这个能点"最便宜的提示，不依赖任何美术资源
            float target = (_hover && !puzzleSolvedFlag) ? 1f : 0f;
            _hoverT = Mathf.MoveTowards(_hoverT, target, Time.deltaTime * hoverLerpSpeed);
            float s = Mathf.Lerp(1f, hoverScale, _hoverT);
            transform.localScale = new Vector3(s, s, s);
        }

        void Awake()
        {
            if (rotator == null) rotator = GetComponent<DragRotateBody>();
            if (anglePuzzle == null) anglePuzzle = GetComponent<ShadowAlignPuzzle>();
            if (silhouettePuzzle == null) silhouettePuzzle = GetComponent<SilhouetteMatchPuzzle>();
            if (rewindPuzzle == null) rewindPuzzle = GetComponent<CircularRewind>();
            if (lightSpot != null) _spotBaseScale = lightSpot.transform.localScale;
        }

        void Start()
        {
            if (rotator != null) rotator.SetInteractable(false);

            if (anglePuzzle != null)
            {
                anglePuzzle.ProgressChanged += OnProgress;
                anglePuzzle.Solved += OnSolved;
            }
            if (silhouettePuzzle != null)
            {
                silhouettePuzzle.ProgressChanged += OnProgress;
                silhouettePuzzle.Solved += OnSolved;
            }
            if (rewindPuzzle != null)
            {
                rewindPuzzle.ProgressChanged += OnProgress;
                rewindPuzzle.Solved += OnSolved;
            }

            ApplySpot(0f);
        }

        void OnDestroy()
        {
            if (anglePuzzle != null)
            {
                anglePuzzle.ProgressChanged -= OnProgress;
                anglePuzzle.Solved -= OnSolved;
            }
            if (silhouettePuzzle != null)
            {
                silhouettePuzzle.ProgressChanged -= OnProgress;
                silhouettePuzzle.Solved -= OnSolved;
            }
            if (rewindPuzzle != null)
            {
                rewindPuzzle.ProgressChanged -= OnProgress;
                rewindPuzzle.Solved -= OnSolved;
            }
        }

        void OnProgress(float p)
        {
            progress = p;
            ApplySpot(p);
            if (ProgressChanged != null) ProgressChanged(p);
        }

        void ApplySpot(float p)
        {
            if (projection != null) projection.SetProgress(p);
            if (lightSpot == null) return;
            if (!_spotInit) { _spotBaseScale = lightSpot.transform.localScale; _spotInit = true; }

            Material m = lightSpot.material;
            if (m != null && m.HasProperty("_Color"))
            {
                Color c = m.color;
                c.a = Mathf.Lerp(0.10f, 0.95f, p);
                m.color = c;
            }
            lightSpot.transform.localScale = _spotBaseScale * Mathf.Lerp(0.55f, 1.15f, p);
        }

        void OnSolved()
        {
            if (puzzleSolvedFlag) return;
            puzzleSolvedFlag = true;

            ApplySpot(1f);
            if (rotator != null) rotator.SetInteractable(false);
            if (PuzzleSolved != null) PuzzleSolved(this);
        }

        /// <summary>给自动化测试用。</summary>
        public void ForceSolve()
        {
            OnSolved();
        }

        public void ResetObject()
        {
            puzzleSolvedFlag = false;
            progress = 0f;
        }
    }
}