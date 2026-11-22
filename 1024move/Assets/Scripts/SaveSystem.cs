using System;
using UnityEngine;

namespace Move1024
{
    /// <summary>一局游戏的最小存档：玩家位置朝向 + 三件旧物是否解开 + 密码槽已填的数字。</summary>
    [Serializable]
    public class SaveData
    {
        public bool hasPlayer;
        public float px, py, pz;
        public float yaw, pitch;

        public bool[] solved = new bool[0];
        public string[] digits = new string[0];
        public bool dustCleared;

        public string savedAt = "";
        public int solvedCount;
    }

    /// <summary>
    /// 简易存档：PlayerPrefs 里放一段 JSON，够用且不依赖文件路径权限。
    /// 想换成真文件存档，只要改 Save/Load 的实现，调用方不用动。
    /// </summary>
    public static class SaveSystem
    {
        const string Key = "move1024_save_v1";

        public static bool HasSave
        {
            get { return PlayerPrefs.HasKey(Key) && !string.IsNullOrEmpty(PlayerPrefs.GetString(Key)); }
        }

        public static string SavedAt
        {
            get
            {
                SaveData d = Load();
                return d != null ? d.savedAt : "";
            }
        }

        public static void Save(SaveData data)
        {
            if (data == null) return;
            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }

        public static SaveData Load()
        {
            if (!HasSave) return null;
            try
            {
                return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(Key));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[1024] 存档解析失败：" + e.Message);
                return null;
            }
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
