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

        [Header("引用")]
        public Player25DController player;
        public CameraStateRig cameraRig;
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
        public bool walkBeforeFocus = true;
        public float focusInDuration = 0.7f;
        public float focusOutDuration = 0.65f;

        [Header("关卡物件")]
        public List<DeskFocusInteractable> interactables = new List<DeskFocusInteractable>();

        [Header("第四个数字")]
        [Tooltip("退租合同（点它打开擦灰面板）")]
        public GameObject contractHotspot;
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
        Vector3 _pendingWalkTarget;
        bool _waitingArrive;
        bool _endingPlayed;

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

            if (_instance == this) _instance = null;
        }

        void Update()
        {
            UpdateHover();

            if (Input.GetKeyDown(KeyCode.F1)) debugAngles = !debugAngles;
            if (debugAngles) DrawAngleDebug();

            if (Input.GetKeyDown(KeyCode.R))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }

            if (state != State.Explore && state != State.Walking) return;
            if (contractUI != null && contractUI.panel != null && contractUI.panel.alpha > 0.5f) return;
            if (!Input.GetMouseButtonDown(0)) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 50f)) return;

            DeskFocusInteractable it = hit.collider.GetComponentInParent<DeskFocusInteractable>();
            if (it != null)
            {
                BeginFocus(it);
                return;
            }

            if (contractHotspot != null && hit.collider.transform.IsChildOf(contractHotspot.transform))
                TryOpenContract();
        }

        void TryOpenContract()
        {
            if (lockContractUntilSolved && SolvedCount < interactables.Count) return;
            if (contractUI != null) contractUI.Open();
        }

        void BeginFocus(DeskFocusInteractable it)
        {
            if (it == null || it.PuzzleSolvedFlag) return;

            _current = it;

            if (walkBeforeFocus && player != null &&
                Vector3.Distance(player.transform.position, it.transform.position) > nearDistance)
            {
                Vector3 target = it.transform.position;
                Vector3 dir = player.transform.position - it.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.back;
                target += dir.normalized * 0.9f;

                player.MoveTo(target);
                _pendingWalkTarget = target;
                _waitingArrive = true;
                SetState(State.Walking);
                return;
            }

            EnterDesk(it);
        }

        void LateUpdate()
        {
            if (state != State.Walking || !_waitingArrive) return;
            if (player == null) { EnterDesk(_current); return; }

            if (!player.IsMoving ||
                Vector3.Distance(player.transform.position, _pendingWalkTarget) <= 0.35f)
            {
                _waitingArrive = false;
                EnterDesk(_current);
            }
        }

        void EnterDesk(DeskFocusInteractable it)
        {
            if (it == null) return;
            SetState(State.FocusIn);

            if (_hovered != null) { _hovered.SetHover(false); _hovered = null; }

            if (player != null) player.SetInputEnabled(false);
            ShowHints(false, true);
            if (deskHintText != null)
                deskHintText.text = it.rewindPuzzle != null
                    ? "按住左键，绕着磁带画圈"
                    : "左右拖动，把影子转到最亮的地方";
            if (ObjectFocused != null) ObjectFocused(it);

            // 记忆穿梭：白光盖住的那一刻再切镜头，玩家看不到推镜过程
            if (transition != null)
            {
                transition.Play(() =>
                {
                    if (cameraRig != null) cameraRig.SnapDesk(it.transform.position);
                    if (it.rotator != null) it.rotator.SetInteractable(true);
                    if (it.rewindPuzzle != null) it.rewindPuzzle.SetInteractable(true);
                });
            }
            else
            {
                if (cameraRig != null) cameraRig.GoDesk(it.transform.position);
                if (it.rotator != null) it.rotator.SetInteractable(true);
                if (it.rewindPuzzle != null) it.rewindPuzzle.SetInteractable(true);
            }

            if (monologue != null && !string.IsNullOrEmpty(it.osLine))
                monologue.Say(it.osLine);

            StartCoroutine(FocusInRoutine());
            StartCoroutine(DeskHintEscalation(it));
        }

        /// <summary>卡住 14 秒 / 26 秒后，把提示说得更直白一点。别让玩家以为自己按错了。</summary>
        System.Collections.IEnumerator DeskHintEscalation(DeskFocusInteractable it)
        {
            float t = 0f;
            while (t < 14f && state == State.Desk && it != null && !it.PuzzleSolvedFlag)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (it == null || it.PuzzleSolvedFlag || state != State.Desk) yield break;

            if (it.rewindPuzzle != null)
            {
                if (deskHintText != null) deskHintText.text = "按住左键，绕着磁带画圈，转满 3 圈就倒好了";
                yield break;
            }

            if (deskHintText != null) deskHintText.text = "影子越亮，说明越接近。慢慢转，别停手。";

            yield return new WaitForSeconds(12f);

            if (it != null && !it.PuzzleSolvedFlag && state == State.Desk && deskHintText != null)
                deskHintText.text = "就是左右拖。转到影子最亮的那一下，它会自己吸附。";
        }

        System.Collections.IEnumerator FocusInRoutine()
        {
            yield return new WaitForSeconds(focusInDuration);
            if (state == State.FocusIn) SetState(State.Desk);
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

            solvedCount++;

            SetState(State.FocusOut);
            ShowHints(false, false);

            if (transition != null)
            {
                transition.Play(() =>
                {
                    if (cameraRig != null) cameraRig.SnapRoom();
                });
                yield return new WaitForSeconds(transition.whiteIn + transition.hold + transition.whiteOut + 0.15f);
            }
            else
            {
                if (cameraRig != null) cameraRig.GoRoom();
                yield return new WaitForSeconds(focusOutDuration);
            }

            if (player != null) player.SetInputEnabled(true);
            _current = null;
            ShowHints(true, false);

            if (solvedCount >= interactables.Count && interactables.Count > 0)
                OnAllObjectsSolved();
            else
                SetState(State.Explore);
        }

        void OnAllObjectsSolved()
        {
            SetState(State.Explore);
            SetContractAvailable(true);

            if (monologue != null)
                monologue.Say("还差最后一位……桌上那份退租合同，好像一直没细看过。");
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

            if ((state == State.Explore || state == State.Walking) && Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 50f))
                    hitIt = hit.collider.GetComponentInParent<DeskFocusInteractable>();
            }

            if (hitIt != _hovered)
            {
                if (_hovered != null) _hovered.SetHover(false);
                _hovered = hitIt;
                if (_hovered != null) _hovered.SetHover(true);
            }

            if (crosshair != null)
            {
                if (state == State.Desk && _current != null)
                    crosshair.SetHover(true, string.IsNullOrEmpty(_current.deskHint) ? "按住左键拖动" : _current.deskHint);
                else if (hitIt != null)
                    crosshair.SetHover(true, string.IsNullOrEmpty(hitIt.hoverHint) ? "点击查看" : hitIt.hoverHint);
                else
                    crosshair.SetHover(false, "");
            }
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