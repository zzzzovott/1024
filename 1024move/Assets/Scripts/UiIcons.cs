using UnityEngine;

namespace Move1024
{
    /// <summary>代码画的 UI 图标（房子 / 存档 / 齿轮 / 圆点），不依赖美术切图。</summary>
    public static class UiIcons
    {
        static Sprite _house, _save, _gear, _dot;

        public static Sprite House { get { if (_house == null) _house = MakeHouse(64); return _house; } }
        public static Sprite Save { get { if (_save == null) _save = MakeSave(64); return _save; } }
        public static Sprite Gear { get { if (_gear == null) _gear = MakeGear(64); return _gear; } }
        public static Sprite Dot { get { if (_dot == null) _dot = MakeCircle(64); return _dot; } }

        static Sprite MakeHouse(int size)
        {
            Texture2D tex = NewTex(size);
            Color32[] px = new Color32[size * size];

            float roofTop = size * 0.92f;
            float roofBase = size * 0.55f;
            float roofHalf = size * 0.40f;
            float bodyLeft = size * 0.22f;
            float bodyRight = size * 0.78f;
            float bodyBottom = size * 0.14f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;

                    bool inRoof = fy >= roofBase && fy <= roofTop &&
                                  Mathf.Abs(fx - size * 0.5f) <= roofHalf * (roofTop - fy) / (roofTop - roofBase);

                    bool inBody = fx >= bodyLeft && fx <= bodyRight && fy >= bodyBottom && fy <= roofBase + 1f;

                    px[y * size + x] = (inRoof || inBody) ? White : Clear;
                }
            }

            return Finish(tex, px);
        }

        /// <summary>软盘：外框 + 右上角快门缺口 + 下方标签条。</summary>
        static Sprite MakeSave(int size)
        {
            Texture2D tex = NewTex(size);
            Color32[] px = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x + 0.5f;
                    float fy = y + 0.5f;

                    bool outer = fx >= size * 0.14f && fx <= size * 0.86f && fy >= size * 0.12f && fy <= size * 0.88f;
                    bool shutter = fx >= size * 0.56f && fx <= size * 0.76f && fy >= size * 0.62f && fy <= size * 0.88f;
                    bool label = fx >= size * 0.26f && fx <= size * 0.74f && fy >= size * 0.20f && fy <= size * 0.46f;

                    px[y * size + x] = outer && !shutter && !label ? White : Clear;
                }
            }

            return Finish(tex, px);
        }

        /// <summary>齿轮：圆盘 + 8 个齿 + 中间轴孔。</summary>
        static Sprite MakeGear(int size)
        {
            Texture2D tex = NewTex(size);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);

                    float body = 0.62f + 0.16f * Mathf.Cos(a * 8f);
                    bool solid = r <= body && r >= 0.20f;

                    px[y * size + x] = solid ? White : Clear;
                }
            }

            return Finish(tex, px);
        }

        static Sprite MakeCircle(int size)
        {
            Texture2D tex = NewTex(size);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((0.94f - d) / 0.12f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            return Finish(tex, px);
        }

        static readonly Color32 White = new Color32(255, 255, 255, 255);
        static readonly Color32 Clear = new Color32(255, 255, 255, 0);

        static Texture2D NewTex(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        static Sprite Finish(Texture2D tex, Color32[] px)
        {
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
