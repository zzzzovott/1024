#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Move1024.EditorTools
{
    /// <summary>
    /// 真实音频镜像：把 Assets/Audio/ 下的音频自动复制一份到 Assets/Resources/Audio/，
    /// 这样策划只要把文件丢进 Assets/Audio（保持和音效 id 同名），运行时就自动优先播真实音频，
    /// 不需要改任何代码。
    ///
    /// 约定：文件名 = 音效 id，例如 sfx_box_drop.wav / sfx_wipe.wav / hum_tape.wav
    /// 支持 .wav / .ogg / .mp3 / .aiff
    /// </summary>
    public class AudioAssetPostprocessor : AssetPostprocessor
    {
        const string SourceDir = "Assets/Audio";
        const string TargetDir = "Assets/Resources/Audio";

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            for (int i = 0; i < importedAssets.Length; i++) Mirror(importedAssets[i]);
            for (int i = 0; i < movedAssets.Length; i++) Mirror(movedAssets[i]);
        }

        static void Mirror(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!path.StartsWith(SourceDir + "/")) return;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext != ".wav" && ext != ".ogg" && ext != ".mp3" && ext != ".aiff") return;

            EnsureFolder("Assets/Resources");
            EnsureFolder(TargetDir);

            string target = TargetDir + "/" + Path.GetFileName(path);
            if (AssetDatabase.LoadAssetAtPath<Object>(target) == null)
            {
                AssetDatabase.CopyAsset(path, target);
                Debug.Log("[1024][Audio] 已镜像真实音频：" + path + " → " + target);
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
