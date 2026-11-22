using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Move1024;

namespace Move1024.EditorTools
{
    /// <summary>
    /// 一键生成《第1024次搬家》白盒验证场景。
    /// 菜单：1024 / 生成白盒场景
    ///
    /// 目的：让程序半天内就能验证"走位 → 推近 → 转物 → 对上 → 填数字"这条主循环，
    /// 不被美术资源和 UI 排版卡住。
    /// </summary>
    public static class WhiteBoxSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/WhiteBox_1024.unity";

        [MenuItem("1024/① 生成白盒场景", false, 1)]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog(
                    "生成白盒场景",
                    "会在当前工程新建 Assets/Scenes/WhiteBox_1024.unity，并生成房间、桌子、3 件旧物和基础 UI。\n继续吗？",
                    "生成", "取消"))
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------- 清场 ----------
            foreach (var oldCam in Object.FindObjectsOfType<Camera>()) Object.DestroyImmediate(oldCam.gameObject);
            foreach (var oldLight in Object.FindObjectsOfType<Light>()) Object.DestroyImmediate(oldLight.gameObject);

            // ---------- 房间 ----------
            GameObject env = new GameObject("Environment");

            GameObject floor = MakeBox("Floor", env.transform, new Vector3(0f, -0.05f, 0f),
                new Vector3(7f, 0.1f, 6f), new Color(0.32f, 0.30f, 0.28f));
            floor.isStatic = true;

            MakeBox("Wall_Back", env.transform, new Vector3(0f, 1.4f, 3f), new Vector3(7f, 2.8f, 0.15f),
                new Color(0.55f, 0.53f, 0.50f));
            MakeBox("Wall_Left", env.transform, new Vector3(-3.5f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 6f),
                new Color(0.50f, 0.49f, 0.47f));
            MakeBox("Wall_Right", env.transform, new Vector3(3.5f, 1.4f, 0f), new Vector3(0.15f, 2.8f, 6f),
                new Color(0.50f, 0.49f, 0.47f));

            // 桌子
            GameObject desk = MakeBox("Desk", env.transform, new Vector3(0f, 0.4f, 2.0f),
                new Vector3(2.2f, 0.08f, 0.9f), new Color(0.36f, 0.26f, 0.19f));
            MakeBox("DeskLeg_L", desk.transform, new Vector3(-0.95f, -0.4f, 0f), new Vector3(0.08f, 0.8f, 0.08f),
                new Color(0.30f, 0.22f, 0.16f));
            MakeBox("DeskLeg_R", desk.transform, new Vector3(0.95f, -0.4f, 0f), new Vector3(0.08f, 0.8f, 0.08f),
                new Color(0.30f, 0.22f, 0.16f));

            // ---------- 灯光 ----------
            GameObject lightRoot = new GameObject("Lights");

            GameObject dirGo = new GameObject("Directional Light");
            dirGo.transform.SetParent(lightRoot.transform);
            dirGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light dir = dirGo.AddComponent<Light>();
            dir.type = LightType.Directional;
            dir.intensity = 0.85f;
            dir.color = new Color(0.72f, 0.78f, 0.90f);

            GameObject fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(lightRoot.transform);
            fillGo.transform.position = new Vector3(0f, 2.2f, 1f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 6f;
            fill.intensity = 0f;
            fill.color = new Color(1f, 0.62f, 0.35f);

            // ---------- 相机 ----------
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);

            CameraStateRig rig = camGo.AddComponent<CameraStateRig>();

            GameObject roomAnchor = new GameObject("Anchor_Room");
            roomAnchor.transform.position = new Vector3(4.2f, 2.4f, -3.6f);
            roomAnchor.transform.rotation = Quaternion.Euler(16f, -46f, 0f);

            GameObject deskAnchor = new GameObject("Anchor_Desk");
            deskAnchor.transform.position = new Vector3(0f, 1.05f, 0.95f);
            deskAnchor.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            rig.roomAnchor = roomAnchor.transform;
            rig.deskAnchor = deskAnchor.transform;

            // ---------- 玩家 ----------
            GameObject playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            // 胶囊网格高 1.8，原点在几何中心：把原点抬到 0.9，脚底正好落在 y = 0
            playerGo.transform.position = new Vector3(0f, 0.9f, -1.5f);
            Object.DestroyImmediate(playerGo.GetComponent<CapsuleCollider>());
            CharacterController cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = Vector3.zero;
            Player25DController pc = playerGo.AddComponent<Player25DController>();
            pc.cameraTransform = camGo.transform;

            rig.playerFollow = playerGo.transform;

            // ---------- 三件旧物 ----------
            GameObject props = new GameObject("Props");
            string[] names = { "铁皮青蛙", "复读机磁带", "半截火车票" };
            string[] digits = { "1", "0", "2" };
            Vector3[] positions =
            {
                new Vector3(-0.65f, 0.55f, 2.0f),
                new Vector3( 0.00f, 0.55f, 2.05f),
                new Vector3( 0.65f, 0.55f, 1.95f)
            };

            var interactables = new System.Collections.Generic.List<DeskFocusInteractable>();

            for (int i = 0; i < names.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Prop_" + (i + 1) + "_" + names[i];
                go.transform.SetParent(props.transform);
                go.transform.position = positions[i];
                go.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
                go.transform.rotation = Quaternion.Euler(Random.Range(-40f, 40f), Random.Range(0f, 360f), Random.Range(-40f, 40f));

                Renderer r = go.GetComponent<Renderer>();
                r.sharedMaterial = GetOrCreateMaterial("M_Prop_Base", new Color(0.55f, 0.35f, 0.20f), 0.35f);

                DragRotateBody rot = go.AddComponent<DragRotateBody>();
                rot.interactable = false;

                ShadowAlignPuzzle puzzle = go.AddComponent<ShadowAlignPuzzle>();
                puzzle.rotatingPart = go.transform;
                puzzle.rotator = rot;
                puzzle.captureCurrentOnStart = false;
                puzzle.targetRotation = Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));

                MemoryCuePlayer cue = go.AddComponent<MemoryCuePlayer>();

                DeskFocusInteractable it = go.AddComponent<DeskFocusInteractable>();
                it.displayName = names[i];
                it.digit = digits[i];
                it.rotator = rot;
                it.anglePuzzle = puzzle;
                it.memoryCue = cue;

                interactables.Add(it);
            }

            // ---------- 流程 ----------
            GameObject flowGo = new GameObject("GameFlow");
            GameFlowDirector director = flowGo.AddComponent<GameFlowDirector>();
            director.player = pc;
            director.cameraRig = rig;
            director.interactables = interactables;

            GameObject moodGo = new GameObject("MoodLighting");
            MoodLightingController mood = moodGo.AddComponent<MoodLightingController>();
            mood.mainLight = dir;
            mood.fillLight = fill;
            mood.blend = 0f;

            // ---------- 基础 UI ----------
            BuildUI(out PasswordLockUI lockUi, out CanvasGroup deskHint, out CanvasGroup exploreHint, out Text subtitle);
            director.passwordLock = lockUi;
            director.deskHintGroup = deskHint;
            director.exploreHintGroup = exploreHint;

            foreach (var it in interactables)
                if (it.memoryCue != null) it.memoryCue.subtitle = subtitle;

            // ---------- 保存 ----------
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("完成",
                "白盒场景已生成：\n" + ScenePath +
                "\n\n下一步：\n" +
                "1. 检查 Console 有无报错\n" +
                "2. 点 Play，左键点桌上的方块 → 拖拽旋转 → 转到目标角度会磁吸并触发字幕\n" +
                "3. 想调难度就改物件上 ShadowAlignPuzzle 的 enterTolerance / useMagnet",
                "好");

            Debug.Log("[1024] 白盒场景已生成： " + ScenePath);
        }

        [MenuItem("1024/② 把选中物件的当前朝向设为谜底", false, 2)]
        public static void CaptureTargetOnSelection()
        {
            foreach (var go in Selection.gameObjects)
            {
                ShadowAlignPuzzle p = go.GetComponent<ShadowAlignPuzzle>();
                if (p != null) { p.CaptureTarget(); EditorUtility.SetDirty(p); }
            }
            Debug.Log("[1024] 已把选中物件的当前朝向记录为谜底。");
        }

        // ------------------------------------------------------------------

        static GameObject MakeBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial("M_" + name, color, 0.15f);
            return go;
        }

        /// <summary>
        /// 运行时 new 出来的材质不会被场景保存，必须落成 .mat 资源，否则重开工程全是粉红。
        /// </summary>
        static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            string path = "Assets/Materials/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                existing.SetFloat("_Glossiness", smoothness);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            Material m = new Material(shader);
            m.color = color;
            m.SetFloat("_Glossiness", smoothness);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static void BuildUI(out PasswordLockUI lockUi, out CanvasGroup deskHint, out CanvasGroup exploreHint, out Text subtitle)
        {
            // EventSystem
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }

            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // 密码锁：底部居中四个格子
            GameObject lockRoot = new GameObject("PasswordLock", typeof(RectTransform));
            lockRoot.transform.SetParent(canvasGo.transform, false);
            RectTransform lockRt = lockRoot.GetComponent<RectTransform>();
            lockRt.anchorMin = new Vector2(0.5f, 0f);
            lockRt.anchorMax = new Vector2(0.5f, 0f);
            lockRt.pivot = new Vector2(0.5f, 0.5f);
            lockRt.anchoredPosition = new Vector2(0f, 90f);
            lockRt.sizeDelta = new Vector2(420f, 80f);

            PasswordLockUI lockComp = lockRoot.AddComponent<PasswordLockUI>();
            lockComp.slots = new Text[4];

            for (int i = 0; i < 4; i++)
            {
                GameObject cell = new GameObject("Slot" + i, typeof(RectTransform), typeof(Image));
                cell.transform.SetParent(lockRoot.transform, false);
                RectTransform crt = cell.GetComponent<RectTransform>();
                crt.sizeDelta = new Vector2(72f, 72f);
                crt.anchoredPosition = new Vector2(-150f + i * 100f, 0f);
                Image img = cell.GetComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 0.06f);
                img.raycastTarget = false;

                GameObject txtGo = new GameObject("Text", typeof(RectTransform));
                txtGo.transform.SetParent(cell.transform, false);
                Text t = txtGo.AddComponent<Text>();
                t.font = font;
                t.fontSize = 40;
                t.alignment = TextAnchor.MiddleCenter;
                t.color = new Color(1f, 1f, 1f, 0.18f);
                t.text = "—";
                t.raycastTarget = false;
                RectTransform trt = txtGo.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = Vector2.zero;
                trt.offsetMax = Vector2.zero;

                lockComp.slots[i] = t;
            }

            // 字幕
            GameObject subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(canvasGo.transform, false);
            Text sub = subGo.AddComponent<Text>();
            sub.font = font;
            sub.fontSize = 30;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.color = new Color(1f, 1f, 1f, 0.92f);
            sub.text = "";
            sub.raycastTarget = false;
            RectTransform srt = subGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.5f);
            srt.anchorMax = new Vector2(0.5f, 0.5f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.anchoredPosition = new Vector2(0f, -140f);
            srt.sizeDelta = new Vector2(1200f, 120f);

            // 探索提示
            GameObject exGo = new GameObject("Hint_Explore", typeof(RectTransform));
            exGo.transform.SetParent(canvasGo.transform, false);
            CanvasGroup exCg = exGo.AddComponent<CanvasGroup>();
            Text exTxt = MakeHintText(exGo, font, "点击桌上的旧物");
            RectTransform ert = exGo.GetComponent<RectTransform>();
            ert.anchorMin = new Vector2(0.5f, 0f);
            ert.anchorMax = new Vector2(0.5f, 0f);
            ert.anchoredPosition = new Vector2(0f, 200f);
            ert.sizeDelta = new Vector2(900f, 60f);

            // 桌面提示
            GameObject dkGo = new GameObject("Hint_Desk", typeof(RectTransform));
            dkGo.transform.SetParent(canvasGo.transform, false);
            CanvasGroup dkCg = dkGo.AddComponent<CanvasGroup>();
            MakeHintText(dkGo, font, "按住左键拖动，让光斑对上墙上的轮廓");
            RectTransform drt = dkGo.GetComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0f);
            drt.anchorMax = new Vector2(0.5f, 0f);
            drt.anchoredPosition = new Vector2(0f, 200f);
            drt.sizeDelta = new Vector2(1200f, 60f);
            dkCg.alpha = 0f;

            lockUi = lockComp;
            deskHint = dkCg;
            exploreHint = exCg;
            subtitle = sub;
        }

        static Text MakeHintText(GameObject go, Font font, string content)
        {
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = 24;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(1f, 1f, 1f, 0.55f);
            t.text = content;
            t.raycastTarget = false;
            return t;
        }
    }
}