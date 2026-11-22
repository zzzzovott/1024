using System.IO;
using UnityEditor;
using UnityEngine;

namespace Move1024.EditorTools
{
    /// <summary>
    /// 生成"数字轮廓"模板（纯 CPU 画像素，不需要显卡，命令行也能跑）。
    /// 这张图是墙上那圈淡淡的轮廓，玩家要把物件的剪影转进这个轮廓里。
    ///
    /// 正式版的美术资产可以从"物件转到像数字的那一帧"直接截取（见 SilhouetteAuthoring），
    /// 这里先用真正的数字形状，保证初稿一眼就能看懂要干什么。
    /// </summary>
    public static class DigitMaskFactory
    {
        const int Size = 256;
        const string Dir = "Assets/Textures";

        [MenuItem("1024/数字轮廓/重新生成数字模板", false, 20)]
        public static void RegenerateAll()
        {
            Ensure("1");
            Ensure("0");
            Ensure("2");
            Ensure("4");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[1024] 数字模板已重新生成");
        }

        public static Texture2D Get(string digit)
        {
            string path = Dir + "/Mask_" + digit + ".png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) return tex;
            return Create(digit, path);
        }

        static Texture2D Ensure(string digit)
        {
            return Get(digit);
        }

        static Texture2D Create(string digit, string path)
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Textures");

            Color32[] px = new Color32[Size * Size];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);

            float T = 15f;   // 笔画粗细

            switch (digit)
            {
                case "1":
                    // 顶上的小旗 + 主干 + 底座
                    Stroke(px, 0.40f, 0.72f, 0.52f, 0.84f, T);
                    Stroke(px, 0.52f, 0.84f, 0.52f, 0.17f, T);
                    Stroke(px, 0.34f, 0.17f, 0.70f, 0.17f, T);
                    break;

                case "0":
                    Ring(px, 0.50f, 0.50f, 0.21f, 0.335f, T);
                    break;

                case "2":
                    // 上半圆弧
                    Arc(px, 0.50f, 0.66f, 0.195f, 0.165f, 8f, 190f, T);
                    // 斜下到左下
                    Stroke(px, 0.50f, 0.495f, 0.35f, 0.20f, T);
                    // 底横
                    Stroke(px, 0.33f, 0.17f, 0.68f, 0.17f, T);
                    break;

                case "4":
                    // 竖 + 斜 + 横
                    Stroke(px, 0.50f, 0.16f, 0.50f, 0.84f, T);
                    Stroke(px, 0.20f, 0.55f, 0.74f, 0.55f, T);
                    Stroke(px, 0.60f, 0.84f, 0.60f, 0.16f, T);
                    break;
            }

            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- 画图小工具 ----------

        static void Plot(Color32[] px, int x, int y, float coverage)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            float a = Mathf.Clamp01(coverage);
            if (a <= 0f) return;
            int i = y * Size + x;
            if (px[i].a / 255f >= a) return;
            px[i] = new Color32(255, 255, 255, (byte)(a * 255f));
        }

        static void Disc(Color32[] px, float cx, float cy, float r)
        {
            int x0 = Mathf.FloorToInt(cx - r - 1);
            int x1 = Mathf.CeilToInt(cx + r + 1);
            int y0 = Mathf.FloorToInt(cy - r - 1);
            int y1 = Mathf.CeilToInt(cy + r + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - cx;
                    float dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    if (a > 0f) Plot(px, x, y, a);
                }
            }
        }

        static void Stroke(Color32[] px, float x0, float y0, float x1, float y1, float thickness)
        {
            float dx = x1 - x0;
            float dy = y1 - y0;
            // 注意：x0/y0 是 0~1 的归一化坐标，算步数前要换算成像素，否则笔画会变成几个点
            float len = Mathf.Sqrt(dx * dx + dy * dy) * Size;
            int steps = Mathf.CeilToInt(len / (thickness * 0.35f)) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Disc(px, (x0 + dx * t) * Size, (y0 + dy * t) * Size, thickness * 0.5f);
            }
        }

        static void Ring(Color32[] px, float cx, float cy, float rx, float ry, float thickness)
        {
            int steps = Mathf.CeilToInt((rx + ry) * Size * 2f);
            for (int i = 0; i < steps; i++)
            {
                float a = (float)i / steps * Mathf.PI * 2f;
                float x = cx * Size + Mathf.Cos(a) * rx * Size;
                float y = cy * Size + Mathf.Sin(a) * ry * Size;
                Disc(px, x, y, thickness * 0.5f);
            }
        }

        static void Arc(Color32[] px, float cx, float cy, float rx, float ry,
            float fromDeg, float toDeg, float thickness)
        {
            float span = Mathf.Abs(toDeg - fromDeg);
            int steps = Mathf.CeilToInt((rx + ry) * Size * 2f * (span / 360f)) + 2;
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
                float x = cx * Size + Mathf.Cos(a) * rx * Size;
                float y = cy * Size + Mathf.Sin(a) * ry * Size;
                Disc(px, x, y, thickness * 0.5f);
            }
        }
    }
}