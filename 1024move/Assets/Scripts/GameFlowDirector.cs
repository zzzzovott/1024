using System;
using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 流程大脑：探索 → 走到桌边 → 推近特写 → 旋转解谜 → 记忆碎片 → 回到探索
    /// →（三件解完）擦合同拿第四位 → 密码锁 → 终局演出。
    ///
    /// 一个能一眼看懂的 Director，比一堆事件总线更省三人小队的沟通成本。
    /// 策划要改流程只需要动 Inspector。
    /// </summary>
    public class GameFlowDirector : MonoBehaviour
    {
        public enum State
        {
            Explore,
            Walking,
            FocusIn,
            Desk,
            Memory,
            FocusOut,
            Ending
        }

        /// <summary>主线阶段：锁死流程，防止乱点穿帮。</summary>
        public enum Stage
        {
            EnterWithBox = 1,   // 刚进门：只能放箱子
            TidyClutter = 2,    // 整理杂物：只能捡杂物丢箱子
            DiscoverBox = 3,    // 发现铁盒：掀旧衣服、点铁盒
            FindClues = 4,      // 寻找线索：旧物 / 电视解锁
            Puzzle = 5          // 解谜中
        }

        [Header("引用")]
        public FPSController player;
        public CameraStateRig cameraRig;   // 第一人称下不再使用（留字段避免旧引用报错）
        public PasswordLockUI passwordLock;
        public ContractScratchUI contractUI;
        public FinaleController finale;
        public CrosshairUI crosshair;
        public InnerMonologue monologue;
        public MemoryTransition transition;

        [Header("UI 分组")]
        public CanvasGroup deskHintGroup;
        public CanvasGroup exploreHintGroup;
        public UnityEngine.UI.Text exploreHintText;
        public UnityEngine.UI.Text deskHintText;

        [Header("桌面交互")]
        [Tooltip("玩家离物件多近可以直接进入特写")]
        public float nearDistance = 2.2f;
        [Tooltip("第一人称下关掉：玩家自己走到桌前，不再自动导航")]
        public bool walkBeforeFocus = false;
        [Tooltip("多远之内才能点开旧物（米）：第一人称下靠走位控制距离")]
        public float interactDistance = 2.5f;
        [Tooltip("进入/退出特写是否播记忆白光：第一人称下关掉，否则每次点物件都闪一下")]
        public bool useFocusTransition = false;
        [Header("拾取（F 键）")]
        [Tooltip("拾取 / 放下的按键")]
        public KeyCode pickupKey = KeyCode.F;
        [Tooltip("桌面灰尘（存档要记它有没有擦干净）")]
        public DustErase dust;
        [Tooltip("抹布（存档要记有没有拿到）")]
        public RagPickup rag;
        [Tooltip("持握点：物件飞到它这里。通常是 Main Camera 下的空物体")]
        public Transform holdAnchor;
        [Tooltip("飞向镜头 / 飞回原位的时间（秒）")]
        public float pickupMoveTime = 0.32f;
        [Tooltip("没有持握点时，物件停在镜头前多远")]
        public float holdDistance = 0.55f;
        public float focusInDuration = 0.7f;
        public float focusOutDuration = 0.65f;

        [Header("关卡物件")]
        public List<DeskFocusInteractable> interactables = new List<DeskFocusInteractable>();

        [Header("主线阶段（锁死顺序）")]
        [SerializeField] Stage stage = Stage.EnterWithBox;
        [Tooltip("收几件杂物后推进到'发现铁盒'")]
        public int clutterToFindBox = 3;
        public Stage CurrentStage { get { return stage; } }
        public bool CanTidy { get { return stage == Stage.TidyClutter; } }
        public bool CanDiscoverBox { get { return stage == Stage.DiscoverBox; } }
        public bool CanUseProps { get { return stage == Stage.FindClues || stage == Stage.Puzzle; } }
        public event Action<Stage> StageChanged;

        [Header("第四个数字")]
        [Tooltip("退租合同（点它打开擦灰面板）")]
        public GameObject contractHotspot;
        [Tooltip("合同 Hitbox 上的准星提示（解锁后才显示）")]
        public PromptSource contractPrompt;
        [Tooltip("解完三件之前，合同不可点（避免跳关）")]
        public bool lockContractUntilSolved = true;

        [Header("终局")]
        public GameObject lockBox;
        public AudioSource sfxSource;
        public AudioClip latchClip;

        [Header("调试")]
        [Tooltip("按 F1 开关：显示当前角度 / 目标角度，美术对模型时用")]
        public bool debugAngles;

        [Header("运行时可读")]
        [SerializeField] State state = State.Explore;
        [SerializeField] int solvedCount;

        public State CurrentState { get { return state; } }
        public int SolvedCount { get { return solvedCount; } }

        public event Action<State> StateChanged;
        public event Action<DeskFocusInteractable> ObjectFocused;
        public event Action<DeskFocusInteractable> ObjectSolved;

        DeskFocusInteractable _current;
        DeskFocusInteractable _hovered;
        Transform _originParent;
        Vector3 _originPos;
        Quaternion _originRot;
        bool _held;
        bool _restoring;
        bool _endingPlayed;
        int _clutterStored;
        float _nextIdleHint;

        static GameFlowDirector _instance;
        public static GameFlowDirector Instance { get { return _instance; } }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void Start()
        {
            foreach (var it in interactables)
                if (it != null) it.PuzzleSolved += OnPuzzleSolved;

            if (passwordLock != null) passwordLock.Unlocked += OnLockUnlocked;
            if (contractUI != null) contractUI.Revealed += OnContractRevealed;

            // 引导：抹布拿起/放下、桌面擦干净，都播一句 OS
            if (rag != null)
            {
                rag.PickedUp += OnRagPicked;
                rag.Dropped += OnRagDropped;
            }
            if (dust != null) dust.Cleared += OnDustCleared;
            if (dust != null) dust.NoRagAttempt += OnDustNoRag;

            _nextIdleHint = Time.time + 15f;   // 发呆 15 秒给一次提示

            SetContractAvailable(false);
            SetState(State.Explore);
            ShowHints(true, false);
        }

        void OnDestroy()
        {
            foreach (var it in interactables)
                if (it != null) it.PuzzleSolved -= OnPuzzleSolved;

            if (passwordLock != null) passwordLock.Unlocked -= OnLockUnlocked;
            if (contractUI != null) contractUI.Revealed -= OnContractRevealed;
            if (rag != null)
            {
                rag.PickedUp -= OnRagPicked;
                rag.Dropped -= OnRagDropped;
            }
            if (dust != null) dust.Cleared -= OnDustCleared;
            if (dust != null) dust.NoRagAttempt -= OnDustNoRag;

            if (_instance == this) _instance = null;
        }

        void Update()
        {
            UpdateHover();
            UpdateGuidance();

            // 发呆防卡关：进入"发现铁盒"之后才开始提示，避免开局就聒噪
            if ((state == State.Explore || state == State.Walking) &&
                stage >= Stage.DiscoverBox && Time.time >= _nextIdleHint)
            {
                _nextIdleHint = Time.time + 25f;
                SayDialogue(DialogueKeys.IdlePhase3);
            }

            if (Input.GetKeyDown(KeyCode.F1)) debugAngles = !debugAngles;
            if (debugAngles) DrawAngleDebug();
            else UpdateDeskAlignment();

            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }

            // 拾取 / 放下（参考传统单机）：F 键拾取，持握时再按 F 或右键放下
            if (Input.GetKeyDown(pickupKey) || (state == State.Desk && Input.GetMouseButtonDown(1)))
            {
                if (state == State.Desk)
                {
                    EndPickup(false);
                    return;
                }

                if (state == State.Explore || state == State.Walking)
                {
                    if (_hovered != null)
                    {
                        if (player != null &&
                            Vector3.Distance(player.transform.position, _hovered.transform.position) > interactDistance)
                        {
                            SayDialogue(DialogueKeys.TooFar);
                            return;
                        }

                        BeginPickup(_hovered);
                        return;
                    }
                }
            }

            if (state != State.Explore && state != State.Walking) return;
            if (contractUI != null && contractUI.panel != null && contractUI.panel.alpha > 0.5f) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (contractHotspot != null && crosshair != null && crosshair.HoveredCollider != null &&
                crosshair.HoveredCollider.transform.IsChildOf(contractHotspot.transform))
                TryOpenContract();
        }

        void TryOpenContract()
        {
            if (lockContractUntilSolved && SolvedCount < interactables.Count) return;
            if (contractUI != null) contractUI.Open();
        }

        /// <summary>按下 F：把物件平滑吸到镜头前，进入持握状态。</summary>
        void BeginPickup(DeskFocusInteractable it)
        {
            if (it == null || it.PuzzleSolvedFlag || _held) return;

            // 阶段锁：铁盒被发现之前，桌上的旧物不可交互
            if (!CanUseProps)
            {
                SayDialogue(DialogueKeys.PropLocked);
                return;
            }

            if (stage == Stage.FindClues) SetStage(Stage.Puzzle);

            _current = it;
            _held = true;
            _originParent = it.transform.parent;
            _originPos = it.transform.position;
            _originRot = it.transform.rotation;

            SetState(State.FocusIn);

            if (_hovered != null) { _hovered.SetHover(false); _hovered = null; }

            // 持握时移动和视角都交给"拖拽旋转物件"，避免鼠标同时转镜头又转物件
            if (player != null) { player.SetMoveEnabled(false); player.SetLookEnabled(false); }

            ShowHints(false, true);
            if (deskHintText != null)
                deskHintText.text = it.rewindPuzzle != null
                    ? "按住左键，绕着磁带画圈"
                    : "按住左键拖动，把影子对齐";
            if (crosshair != null) crosshair.SetOverrideLabel("[F] 放下");

            if (ObjectFocused != null) ObjectFocused(it);

            if (monologue != null && !string.IsNullOrEmpty(it.osLine))
                monologue.Say(it.osLine);

            // 墙上的实时剪影会跟着物件跑偏，持握期间先关掉，只看桌面光斑/提示
            if (it.projection != null && it.projection.liveQuad != null) it.projection.liveQuad.enabled = false;

            StartCoroutine(PickupRoutine(it));
        }

        System.Collections.IEnumerator PickupRoutine(DeskFocusInteractable it)
        {
            Transform t = it.transform;
            Vector3 fromPos = t.position;
            Quaternion fromRot = t.rotation;

            float time = 0f;
            while (time < pickupMoveTime && it != null)
            {
                time += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / pickupMoveTime));
                t.position = Vector3.Lerp(fromPos, HoldTarget(), u);
                t.rotation = Quaternion.Slerp(fromRot, HoldRotation(), u);
                yield return null;
            }

            if (it == null) yield break;

            // 挂到持握点下面，之后跟着镜头走（持握期间镜头是锁住的）
            if (holdAnchor != null) t.SetParent(holdAnchor, true);
            t.position = HoldTarget();
            t.rotation = HoldRotation();

            if (it.rotator != null)
            {
                it.rotator.SetInteractable(true);   // 三轴自由旋转，光标锁定也能直接读鼠标位移
            }
            if (it.rewindPuzzle != null) it.rewindPuzzle.SetInteractable(true);

            SetState(State.Desk);
            StartCoroutine(DeskHintEscalation(it));
        }

        Vector3 HoldTarget()
        {
            if (holdAnchor != null) return holdAnchor.position;
            Camera cam = Camera.main;
            if (cam == null) return _originPos;
            return cam.transform.position + cam.transform.forward * holdDistance;
        }

        Quaternion HoldRotation()
        {
            if (holdAnchor != null) return holdAnchor.rotation;
            Camera cam = Camera.main;
            return cam != null ? cam.transform.rotation : _originRot;
        }

        /// <summary>放下物件（再按 F / 右键 / 解谜完成）。</summary>
        public void EndPickup(bool solved)
        {
            if (!_held || _current == null) return;
            StartCoroutine(ReturnRoutine(_current, solved));
        }

        System.Collections.IEnumerator ReturnRoutine(DeskFocusInteractable it, bool solved)
        {
            SetState(State.FocusOut);

            if (it.rotator != null)
            {
                it.rotator.SetInteractable(false);
            }
            if (it.rewindPuzzle != null) it.rewindPuzzle.SetInteractable(false);

            // 先回到原来的父节点，再飞回原位
            if (_originParent != null) it.transform.SetParent(_originParent, true);

            Vector3 fromPos = it.transform.position;
            Quaternion fromRot = it.transform.rotation;

            float time = 0f;
            while (time < pickupMoveTime && it != null)
            {
                time += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / pickupMoveTime));
                it.transform.position = Vector3.Lerp(fromPos, _originPos, u);
                it.transform.rotation = Quaternion.Slerp(fromRot, _originRot, u);
                yield return null;
            }

            if (it != null)
            {
                it.transform.position = _originPos;
                it.transform.rotation = _originRot;

                if (it.projection != null && it.projection.liveQuad != null)
                    it.projection.liveQuad.enabled = true;
            }

            _held = false;
            _current = null;
            _hovered = null;

            if (player != null) { player.SetMoveEnabled(true); player.SetLookEnabled(true); }
            ShowHints(true, false);
            SetState(State.Explore);

            if (solved && solvedCount >= interactables.Count && interactables.Count > 0)
                OnAllObjectsSolved();
        }

        /// <summary>特写里卡住 15 秒，给一句更具体的提示。</summary>
        System.Collections.IEnumerator DeskHintEscalation(DeskFocusInteractable it)
        {
            float t = 0f;
            while (t < 15f && state == State.Desk && it != null && !it.PuzzleSolvedFlag)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (it == null || it.PuzzleSolvedFlag || state != State.Desk) yield break;

            if (deskHintText != null)
                deskHintText.text = DialogueConfig.Get(DialogueKeys.DeskStall, "不对……再顺时针转一点点试试。");
        }

        void OnPuzzleSolved(DeskFocusInteractable it)
        {
            if (it == null) return;
            SetState(State.Memory);

            if (it.rotator != null) it.rotator.SetInteractable(false);
            if (it.rewindPuzzle != null) it.rewindPuzzle.SetInteractable(false);
            if (ObjectSolved != null) ObjectSolved(it);
            if (AudioDirector.Instance != null) AudioDirector.Instance.PlayClick();

            StartCoroutine(MemoryRoutine(it));
        }

        System.Collections.IEnumerator MemoryRoutine(DeskFocusInteractable it)
        {
            if (it.memoryCue != null)
            {
                bool done = false;
                Action handler = () => done = true;
                it.memoryCue.Finished += handler;
                it.memoryCue.Play();

                float guard = 0f;
                while (!done && guard < 60f)
                {
                    guard += Time.deltaTime;
                    yield return null;
                }
                it.memoryCue.Finished -= handler;
            }
            else
            {
                yield return new WaitForSeconds(0.8f);
            }

            if (passwordLock != null && !string.IsNullOrEmpty(it.digit))
                passwordLock.FillSlot(it.passwordSlot, it.digit);

            // 对齐成功的那一下
            if (it.anglePuzzle != null) SayDialogue(DialogueKeys.ShadowSolved);

            solvedCount++;

            ShowHints(false, false);
            // 解开了：把物件放回原位，再恢复操作
            yield return StartCoroutine(ReturnRoutine(it, true));
        }

        /// <summary>放下手上的物件，把操作还给玩家。</summary>
        public void CancelFocus()
        {
            EndPickup(false);
        }

        /// <summary>给玩家的分步引导（按主线阶段切换）。</summary>
        string GuidanceText()
        {
            switch (stage)
            {
                case Stage.EnterWithBox:
                    return "① 怀里这箱先放下：准星对准空地，点击左键";

                case Stage.TidyClutter:
                    return "② 收拾杂物：对准地上的杂物左键捡起，再对准大纸箱左键扔进去（已收 " + _clutterStored + " 件）";

                case Stage.DiscoverBox:
                    return "③ 桌上那堆旧衣服下面好像压着什么，掀开看看";

                case Stage.FindClues:
                    if (dust != null && dust.IsActive)
                        return (rag != null && rag.IsHeld)
                            ? "④ 先把桌子擦干净：对准桌面按住左键滑动"
                            : "④ 桌子太脏了，先去纸箱旁找块抹布（F 拾取）";
                    return "④ 看看桌上的旧物和老电视，按 F 拿起";

                default:
                    return "⑤ 转动旧物，让它的影子对上墙上的图案";
            }
        }

        void UpdateGuidance()
        {
            if (state != State.Explore && state != State.Walking) return;
            if (exploreHintText == null) return;

            string guide = GuidanceText();
            if (exploreHintText.text != guide) exploreHintText.text = guide;
        }

        void OnRagPicked()
        {
            if (_restoring) return;
            // 拿到抹布后，桌面提示切换成"[按住左键 擦除桌面]"
        }

        void OnRagDropped()
        {
            if (_restoring) return;
        }

        void OnDustCleared()
        {
            if (_restoring) return;
            SayDialogue(DialogueKeys.DustClearedFrog);
        }

        void OnDustNoRag()
        {
            SayDialogue(DialogueKeys.DustNoRag);
        }

        /// <summary>按文案表 id 说一句（统一走 DialogueManager 的严格队列）。</summary>
        public void SayDialogue(string id)
        {
            string text = DialogueConfig.Get(id, "");
            if (!string.IsNullOrEmpty(text)) DialogueManager.Say(text);
        }

        /// <summary>旧物放进纸箱（ClutterPickup 调用）。放够数量推进到"发现铁盒"。</summary>
        public void OnClutterStored()
        {
            if (stage != Stage.TidyClutter) return;

            _clutterStored++;

            if (_clutterStored >= clutterToFindBox)
            {
                // 先推进阶段（会强制清屏），再给这一步的提示
                SetStage(Stage.DiscoverBox);
                SayDialogue(DialogueKeys.ClutterEnough);
            }
            else
            {
                SayDialogue(RandomClutterJoke());
            }
        }

        /// <summary>CarryingBox：箱子放下。</summary>
        public void OnBoxDropped()
        {
            if (stage != Stage.EnterWithBox) return;
            SetStage(Stage.TidyClutter);
            SayDialogue(DialogueKeys.BoxDropped);
        }

        /// <summary>IronBoxInteract：翻过衣服、看过铁盒但打不开。</summary>
        public void OnIronBoxViewed()
        {
            if (stage != Stage.DiscoverBox) return;

            SetStage(Stage.FindClues);
            SayDialogue(DialogueKeys.LockLocked);
            SayDialogue(DialogueKeys.CluesUnlocked);
        }

        void SetStage(Stage next)
        {
            if (stage == next) return;

            stage = next;

            // 状态推进时立刻清屏：当前句 + 排队全部丢掉，避免旧台词串到新阶段
            if (DialogueManager.Instance != null) DialogueManager.Instance.ClearNow();

            if (StageChanged != null) StageChanged(next);
            UpdateGuidance();
        }

        string RandomClutterJoke()
        {
            int index = UnityEngine.Random.Range(1, 5);
            return "clutter_joke_" + index;
        }

        void OnAllObjectsSolved()
        {
            SetState(State.Explore);
            SetContractAvailable(true);

            if (monologue != null) DialogueManager.Say("还差最后一位……桌上那份退租合同，好像一直没细看过。");
            if (exploreHintText != null)
                exploreHintText.text = "桌上那份退租合同可以点了";

            Debug.Log("[1024] 三件旧物已解完，合同可以点了。");
        }

        void SetContractAvailable(bool on)
        {
            if (contractHotspot == null) return;
            if (!lockContractUntilSolved) return;

            Collider[] cols = contractHotspot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = on;

            if (contractPrompt != null)
            {
                if (on) contractPrompt.SetHint(DialogueConfig.Get(DialogueKeys.ContractPrompt, "[点击 查看退租合同]"));
                else contractPrompt.Clear();
            }
        }

        void OnContractRevealed()
        {
            if (passwordLock != null) passwordLock.FillSlot(3, "4");
        }

        void OnLockUnlocked()
        {
            PlayEnding();
        }

        public void PlayEnding()
        {
            if (_endingPlayed) return;
            _endingPlayed = true;

            SetState(State.Ending);
            if (player != null) player.SetInputEnabled(false);
            if (cameraRig != null) cameraRig.GoRoom();
            ShowHints(false, false);

            if (sfxSource != null && latchClip != null) sfxSource.PlayOneShot(latchClip);
            if (lockBox != null)
            {
                Transform lid = lockBox.transform.Find("Lid");
                if (lid != null)
                    StartCoroutine(OpenLid(lid));
            }

            if (finale != null) finale.Play();
            else Debug.Log("[1024] 终局：没有配置 FinaleController。");
        }

        System.Collections.IEnumerator OpenLid(Transform lid)
        {
            Quaternion from = lid.localRotation;
            Quaternion to = from * Quaternion.Euler(-105f, 0f, 0f);
            float t = 0f;
            while (t < 0.6f)
            {
                t += Time.deltaTime;
                lid.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / 0.6f));
                yield return null;
            }
            lid.localRotation = to;
        }

        void UpdateHover()
        {
            DeskFocusInteractable hitIt = null;

            // 准星（屏幕中心射线）告诉我们正对着哪件旧物
            if ((state == State.Explore || state == State.Walking) && crosshair != null)
                hitIt = crosshair.HoveredInteractable;

            if (hitIt != _hovered)
            {
                if (_hovered != null) _hovered.SetHover(false);
                _hovered = hitIt;
                if (_hovered != null) _hovered.SetHover(true);
            }

            if (crosshair != null)
            {
                if (state == State.Desk && _current != null)
                {
                    string op = string.IsNullOrEmpty(_current.deskHint) ? "按住左键拖动" : _current.deskHint;
                    crosshair.SetOverrideLabel("[F] 放下   ·   " + op);
                }
                else
                {
                    crosshair.SetOverrideLabel(null);
                }
            }
        }

        /// <summary>特写时在屏幕下方实时显示"对齐度"，让玩家知道自己在靠近答案。</summary>
        void UpdateDeskAlignment()
        {
            if (state != State.Desk || _current == null || deskHintText == null) return;

            ShadowAlignPuzzle puzzle = _current.anglePuzzle;
            if (puzzle == null) return;

            int pct = Mathf.RoundToInt(puzzle.Similarity * 100f);

            string hint;
            if (pct >= 80) hint = "快对上了 · 对齐度 " + pct + "%";
            else if (pct >= 40) hint = "对齐度 " + pct + "% · 继续转";
            else hint = "调整角度，让影子投在墙上";

            if (deskHintText.text != hint) deskHintText.text = hint;
        }

        void DrawAngleDebug()
        {
            if (state != State.Desk || _current == null || deskHintText == null) return;

            float cur = _current.transform.eulerAngles.y;
            float tgt = _current.anglePuzzle != null
                ? _current.anglePuzzle.targetRotation.eulerAngles.y
                : 0f;

            deskHintText.text = "当前 " + cur.ToString("F0") + "°　·　目标 " + tgt.ToString("F0") + "°　(F1 关闭)";
        }

        void SetState(State next)
        {
            if (state == next) return;
            state = next;
            if (StateChanged != null) StateChanged(next);
        }

        void ShowHints(bool explore, bool desk)
        {
            if (exploreHintGroup != null) exploreHintGroup.alpha = explore ? 1f : 0f;
            if (deskHintGroup != null) deskHintGroup.alpha = desk ? 1f : 0f;
        }

        // ---------------- 存档 / 读档 ----------------

        /// <summary>记录当前进度：玩家位置朝向 + 三件旧物是否解开 + 密码槽数字。</summary>
        public SaveData CaptureSave()
        {
            var d = new SaveData();

            if (player != null)
            {
                Vector3 p = player.transform.position;
                d.hasPlayer = true;
                d.px = p.x; d.py = p.y; d.pz = p.z;
                d.yaw = player.Yaw;
                d.pitch = player.Pitch;
            }

            int n = interactables != null ? interactables.Count : 0;
            d.solved = new bool[n];
            for (int i = 0; i < n; i++)
                d.solved[i] = interactables[i] != null && interactables[i].PuzzleSolvedFlag;

            d.digits = passwordLock != null ? passwordLock.GetValues() : new string[0];
            d.dustCleared = dust == null || !dust.IsActive;
            d.hasRag = rag == null || rag.IsHeld;
            d.solvedCount = solvedCount;
            return d;
        }

        /// <summary>恢复进度：位置、已解开的旧物、密码槽。不重播记忆片段、不触发结局。</summary>
        public void ApplySave(SaveData d)
        {
            if (d == null) return;

            _restoring = true;

            if (player != null && d.hasPlayer)
                player.Teleport(new Vector3(d.px, d.py, d.pz), d.yaw, d.pitch);

            solvedCount = 0;
            int n = interactables != null ? interactables.Count : 0;
            for (int i = 0; i < n; i++)
            {
                if (interactables[i] == null) continue;
                bool solved = d.solved != null && i < d.solved.Length && d.solved[i];
                if (solved)
                {
                    interactables[i].RestoreSolved();
                    solvedCount++;
                }
            }

            if (passwordLock != null) passwordLock.RestoreSlots(d.digits);

            // 灰已经擦干净的话，读档后别再盖回去
            if (d.dustCleared && dust != null && dust.IsActive) dust.ForceClear();

            // 读过档时手里应该有抹布，就补上
            if (d.hasRag && rag != null && !rag.IsHeld) rag.PickUp();

            if (n > 0 && solvedCount >= n) SetContractAvailable(true);

            _restoring = false;

            SetState(State.Explore);
            ShowHints(true, false);
        }

        // ---------------- 给自动化冒烟测试用 ----------------

        public void DebugSolveAll()
        {
            for (int i = 0; i < interactables.Count; i++)
                if (interactables[i] != null && !interactables[i].PuzzleSolvedFlag)
                    interactables[i].ForceSolve();
        }

        public void DebugOpenContract()
        {
            if (contractUI != null) contractUI.ForceReveal();
        }
    }
}