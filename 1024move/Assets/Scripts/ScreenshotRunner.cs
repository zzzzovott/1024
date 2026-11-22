using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Move1024
{
    /// <summary>
    /// 截图巡检：命令行传 -shot 目录，游戏会自己跑一遍关键画面并截图，然后退出。
    /// 用来在没有美术的情况下检查构图、字体、灯光有没有问题。
    ///
    ///   1024Move.exe -shot "D:\shots" -screen-width 1280 -screen-height 720 -screen-fullscreen 0
    /// </summary>
    public class ScreenshotRunner : MonoBehaviour
    {
        public GameFlowDirector director;

        string _dir;

        void Awake()
        {
            _dir = ReadArg("-shot");
            if (string.IsNullOrEmpty(_dir)) enabled = false;
        }

        static string ReadArg(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        IEnumerator Start()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            if (director == null) director = FindObjectOfType<GameFlowDirector>();

            yield return new WaitForSeconds(2.2f);
            yield return Shot("01_room");

            if (director != null && director.cameraRig != null)
            {
                Vector3 focus = director.interactables.Count > 0
                    ? director.interactables[0].transform.position
                    : new Vector3(0f, 0.8f, 2.2f);
                director.cameraRig.GoDesk(focus);
                yield return new WaitForSeconds(1.8f);
                yield return Shot("02_desk");

                if (director.interactables.Count > 0)
                {
                    var it = director.interactables[0];
                    if (it.rotator != null) it.rotator.SetInteractable(true);
                    it.ForceSolve();
                    yield return new WaitForSeconds(2.2f);
                    yield return Shot("03_memory");
                }

                yield return new WaitForSeconds(2.5f);

                // 频谱（倒带成功那一下）
                AudioVisualizer viz = FindObjectOfType<AudioVisualizer>();
                if (viz != null)
                {
                    if (AudioDirector.Instance != null) AudioDirector.Instance.PlayHumming();
                    viz.Show(14f);
                    yield return new WaitForSeconds(1.6f);
                    yield return Shot("05_spectrum");
                }

                director.PlayEnding();
                yield return new WaitForSeconds(8.2f);
                yield return Shot("04_ending");
            }

            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(_dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return new WaitForSeconds(0.45f);
            Debug.Log("[1024] shot -> " + path);
        }
    }
}