using UnityEngine;
using UnityEngine.UI;

namespace Move1024
{
    /// <summary>
    /// 用系统字体渲染中文。
    /// Unity 内置的 Arial 在打包后往往拿不到中文字形（显示成方块），
    /// 这里在运行时把 Canvas 下所有 Text 换成系统动态字体，Windows 上就是微软雅黑。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class RuntimeFont : MonoBehaviour
    {
        static Font _font;
        static bool _tried;

        public static Font Font
        {
            get
            {
                if (_font == null && !_tried)
                {
                    _tried = true;
                    string[] prefer =
                    {
                        "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                        "Source Han Sans CN", "Noto Sans CJK SC", "SimHei", "黑体", "SimSun", "宋体"
                    };
                    _font = Font.CreateDynamicFontFromOSFont(prefer, 32);
                    if (_font == null)
                    {
                        try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                        catch { _font = null; }
                    }
                }
                return _font;
            }
        }

        void Awake()
        {
            ApplyAll();
        }

        public static void ApplyAll()
        {
            Font f = Font;
            if (f == null) return;

            Text[] texts = FindObjectsOfType<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i] != null) texts[i].font = f;
        }
    }
}