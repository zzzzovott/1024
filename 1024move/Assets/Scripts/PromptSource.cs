using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 通用交互提示：把它挂在任意可交互物体上，准星命中时显示 hint。
    /// hint 为空则不显示（脚本每帧按状态改它）。
    /// </summary>
    public class PromptSource : MonoBehaviour
    {
        [Tooltip("准星命中时显示的文字，例如 [点击 查看铁盒]；空字符串 = 不显示")]
        public string hint = "";

        public void SetHint(string value)
        {
            hint = value ?? "";
        }

        public void Clear()
        {
            hint = "";
        }
    }
}
