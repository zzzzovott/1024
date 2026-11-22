using System;
using System.Collections.Generic;
using UnityEngine;

namespace Move1024
{
    [Serializable]
    public class DialogueEntry
    {
        public string id;
        public string text;
    }

    [Serializable]
    public class DialogueTable
    {
        public DialogueEntry[] entries;
    }

    /// <summary>
    /// 全流程文案表：运行时从 Resources/Dialogue_1024.json 读取，
    /// 策划改文案只需要改这个 JSON，不用动代码。
    /// 文件缺失或解析失败时退回空串（调用处一般会传 fallback）。
    /// </summary>
    public static class DialogueConfig
    {
        public const string ResourcePath = "Dialogue_1024";

        static Dictionary<string, string> _map;
        static bool _loaded;

        public static string Get(string id, string fallback = "")
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(id)) return fallback;

            string value;
            return _map.TryGetValue(id, out value) && !string.IsNullOrEmpty(value) ? value : fallback;
        }

        public static bool Has(string id)
        {
            EnsureLoaded();
            return !string.IsNullOrEmpty(id) && _map.ContainsKey(id);
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _map = new Dictionary<string, string>(StringComparer.Ordinal);

            TextAsset asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning("[1024] 找不到 Resources/" + ResourcePath + ".json，文案将使用代码内兜底");
                return;
            }

            try
            {
                DialogueTable table = JsonUtility.FromJson<DialogueTable>(asset.text);
                if (table != null && table.entries != null)
                {
                    for (int i = 0; i < table.entries.Length; i++)
                    {
                        DialogueEntry e = table.entries[i];
                        if (e != null && !string.IsNullOrEmpty(e.id)) _map[e.id] = e.text;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[1024] 文案表解析失败：" + ex.Message);
            }
        }
    }

    /// <summary>常用文案 key，避免到处写裸字符串写错。</summary>
    public static class DialogueKeys
    {
        public const string IntroStartBox = "intro_start_box";
        public const string BoxHeavy = "box_heavy";
        public const string BoxDropPrompt = "box_drop_prompt";
        public const string BoxDropped = "box_dropped";

        public const string ClutterPickupPrompt = "clutter_pickup_prompt";
        public const string ClutterStorePrompt = "clutter_store_prompt";
        public const string ClutterStored2 = "clutter_stored_2";

        public const string RagPickupPrompt = "rag_pickup_prompt";
        public const string DustNoRag = "dust_no_rag";
        public const string DustWipePrompt = "dust_wipe_prompt";

        public const string DustClearedFrog = "dust_cleared_frog";
        public const string ClothPrompt = "cloth_prompt";
        public const string ClothMoved = "cloth_moved";
        public const string BoxViewPrompt = "box_view_prompt";
        public const string LockLocked = "lock_locked";
        public const string IdlePhase3 = "idle_phase3";

        public const string FrogHold = "frog_hold";
        public const string ShadowHint = "shadow_hint";
        public const string ShadowSolved = "shadow_solved";
        public const string DeskStall = "desk_stall";
    }
}
