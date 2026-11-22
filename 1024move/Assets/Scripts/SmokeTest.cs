using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 冒烟测试：命令行传 -smoke 时自动跑完整条流程，
    /// 把"有没有运行时异常 / 状态有没有走完"写进报告文件，然后退出。
    ///
    /// 用法（打包出来的 exe）：
    ///   1024Move.exe -batchmode -nographics -smoke "D:\report.txt"
    /// </summary>
    public class SmokeTest : MonoBehaviour
    {
        public GameFlowDirector director;
        public ContractScratchUI contract;
        public FinaleController finale;

        string _reportPath;
        int _errors;
        int _warnings;
        StringBuilder _log = new StringBuilder();

        void Awake()
        {
            _reportPath = ReadArg("-smoke");
            if (string.IsNullOrEmpty(_reportPath))
            {
                enabled = false;
                return;
            }
            Application.logMessageReceived += OnLog;
            _log.AppendLine("=== 1024 smoke test ===");
        }

        static string ReadArg(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                _errors++;
                _log.AppendLine("[ERROR] " + condition);
                if (!string.IsNullOrEmpty(stackTrace)) _log.AppendLine(stackTrace);
            }
            else if (type == LogType.Warning)
            {
                _warnings++;
                _log.AppendLine("[WARN] " + condition);
            }
        }

        System.Collections.IEnumerator Start()
        {
            yield return new WaitForSeconds(1.5f);

            if (director == null) director = FindObjectOfType<GameFlowDirector>();
            if (contract == null) contract = FindObjectOfType<ContractScratchUI>(true);
            if (finale == null) finale = FindObjectOfType<FinaleController>(true);

            Step("director found: " + (director != null));
            Step("interactables: " + (director != null ? director.interactables.Count : -1));

            if (director == null)
            {
                Finish(1);
                yield break;
            }

            // 1. 三件旧物
            for (int i = 0; i < director.interactables.Count; i++)
            {
                var it = director.interactables[i];
                Step("solve prop " + i + " (" + it.displayName + ")");
                it.ForceSolve();
                float wait = 0f;
                while (wait < 12f)
                {
                    wait += Time.deltaTime;
                    if (director.SolvedCount > i) break;
                    yield return null;
                }
                Step("  solvedCount=" + director.SolvedCount);
            }

            yield return new WaitForSeconds(1f);

            // 2. 合同
            Step("open contract");
            if (contract != null) director.DebugOpenContract();
            else Step("  contract null");

            float w2 = 0f;
            while (w2 < 8f) { w2 += Time.deltaTime; yield return null; }

            // 3. 密码锁状态
            var lockUi = FindObjectOfType<PasswordLockUI>(true);
            Step("lock unlocked: " + (lockUi != null ? lockUi.IsUnlocked.ToString() : "null"));
            Step("state: " + director.CurrentState);

            // 4. 结束
            yield return new WaitForSeconds(1f);
            Step("final state: " + director.CurrentState);
            Finish(0);
        }

        void Step(string s)
        {
            _log.AppendLine(s);
        }

        void Finish(int code)
        {
            _log.AppendLine("errors: " + _errors);
            _log.AppendLine("warnings: " + _warnings);
            _log.AppendLine(_errors == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            try
            {
                File.WriteAllText(_reportPath, _log.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("写报告失败: " + e.Message);
            }
            Application.Quit(_errors == 0 ? code : 2);
        }
    }
}