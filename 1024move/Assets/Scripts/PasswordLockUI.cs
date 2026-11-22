using System;
using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 四位密码锁。只负责"显示 + 校验"，不关心数字从哪来。
    /// 策划把 4 个 Text 拖进 slots 即可，不用写代码。
    /// </summary>
    public class PasswordLockUI : MonoBehaviour
    {
        [Header("四位槽位（顺序即密码顺序）")]
        public Text[] slots = new Text[4];

        [Header("正确答案")]
        public string answer = "1024";

        [Header("视觉")]
        public Color emptyColor = new Color(1f, 1f, 1f, 0.18f);
        public Color filledColor = new Color(1f, 1f, 1f, 1f);
        public Color errorColor = new Color(0.85f, 0.25f, 0.25f, 1f);
        [Tooltip("每填一位的弹入动画时长")]
        public float popDuration = 0.22f;

        [Header("音效")]
        public AudioSource audioSource;
        public AudioClip digitTickClip;

        public event Action<int, string> SlotFilled;
        public event Action AllFilled;
        public event Action Unlocked;

        readonly string[] _values = new string[4];
        int _filledCount;
        bool _unlocked;

        public bool IsUnlocked { get { return _unlocked; } }
        public int FilledCount { get { return _filledCount; } }

        void Awake()
        {
            for (int i = 0; i < _values.Length; i++) _values[i] = "";
            Refresh();
        }

        /// <summary>填入一位数字（index 从 0 开始）。</summary>
        public void FillSlot(int index, string digit)
        {
            if (index < 0 || index >= slots.Length) return;
            if (slots[index] == null) return;

            _values[index] = digit;
            _filledCount = 0;
            for (int i = 0; i < _values.Length; i++)
                if (!string.IsNullOrEmpty(_values[i])) _filledCount++;

            StartCoroutine(PopRoutine(slots[index].transform));

            if (audioSource != null && digitTickClip != null)
                audioSource.PlayOneShot(digitTickClip);

            Refresh();

            if (SlotFilled != null) SlotFilled(index, digit);

            if (_filledCount >= _values.Length)
            {
                if (AllFilled != null) AllFilled();
                StartCoroutine(ValidateRoutine());
            }
        }

        /// <summary>按顺序自动填下一位。</summary>
        public void FillNext(string digit)
        {
            for (int i = 0; i < _values.Length; i++)
            {
                if (string.IsNullOrEmpty(_values[i]))
                {
                    FillSlot(i, digit);
                    return;
                }
            }
        }

        string CurrentCode()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < _values.Length; i++) sb.Append(_values[i]);
            return sb.ToString();
        }

        System.Collections.IEnumerator ValidateRoutine()
        {
            yield return new WaitForSeconds(0.45f);

            if (CurrentCode() == answer)
            {
                _unlocked = true;
                if (Unlocked != null) Unlocked();
            }
            else
            {
                yield return StartCoroutine(ShakeRoutine());
                ClearAll();
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _values.Length; i++) _values[i] = "";
            _filledCount = 0;
            Refresh();
        }

        /// <summary>当前四位数字（读档/存档用）。</summary>
        public string[] GetValues()
        {
            return new string[] { _values[0], _values[1], _values[2], _values[3] };
        }

        /// <summary>读档用：直接恢复四位数字，不触发校验、不触发 Unlocked。</summary>
        public void RestoreSlots(string[] digits)
        {
            if (digits == null) return;
            for (int i = 0; i < _values.Length; i++)
                _values[i] = i < digits.Length && digits[i] != null ? digits[i] : "";

            _filledCount = 0;
            for (int i = 0; i < _values.Length; i++)
                if (!string.IsNullOrEmpty(_values[i])) _filledCount++;

            _unlocked = false;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                bool filled = !string.IsNullOrEmpty(_values[i]);
                slots[i].text = filled ? _values[i] : "—";
                slots[i].color = filled ? filledColor : emptyColor;
            }
        }

        System.Collections.IEnumerator PopRoutine(Transform t)
        {
            Vector3 baseScale = Vector3.one;
            t.localScale = baseScale * 0.75f;
            float k = 0f;
            while (k < popDuration)
            {
                k += Time.deltaTime;
                float u = Mathf.Clamp01(k / popDuration);
                // 轻微回弹
                float s = 0.75f + 0.35f * Mathf.Sin(u * Mathf.PI * 0.85f) + 0.25f * u;
                t.localScale = baseScale * s;
                yield return null;
            }
            t.localScale = baseScale;
        }

        System.Collections.IEnumerator ShakeRoutine()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null) slots[i].color = errorColor;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                RectTransform rt = slots[i].rectTransform;
                Vector3 p0 = rt.anchoredPosition;
                float t = 0f;
                while (t < 0.35f)
                {
                    t += Time.deltaTime;
                    rt.anchoredPosition = p0 + new Vector3(Mathf.Sin(t * 60f) * 6f, 0f, 0f);
                    yield return null;
                }
                rt.anchoredPosition = p0;
            }
            Refresh();
        }
    }
}