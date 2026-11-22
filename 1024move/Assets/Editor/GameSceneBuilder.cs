using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Move1024.EditorTools
{
    /// <summary>
    /// 一键生成《第1024次搬家》初稿场景（可从开局玩到结局）。
    /// 菜单：1024 / 生成初稿场景
    /// 命令行：
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Move1024.EditorTools.GameSceneBuilder.BuildSceneBatch
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod Move1024.EditorTools.GameSceneBuilder.BuildPlayerBatch
    /// </summary>
    public static class GameSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Game_1024.unity";
        const string TexDir = "Assets/Textures";
        const string MatDir = "Assets/Materials";

        static Material wallTargetMat;
        static Material wallLiveMat;

        // ================= 入口 =================

        [MenuItem("1024/生成初稿场景（可玩通）", false, 3)]
        public static void BuildSceneMenu()
        {
            if (!EditorUtility.DisplayDialog("生成初稿场景",
                    "会新建 Assets/Scenes/Game_1024.unity 并覆盖同名文件。继续吗？", "生成", "取消"))
                return;

            BuildSceneCore();
            EditorUtility.DisplayDialog("完成",
                "初稿场景已生成：" + ScenePath + "\n\n打开它按 Play 即可玩通全程。", "好");
        }

        public static void BuildSceneBatch()
        {
            BuildSceneCore();
            Debug.Log("[1024] batch scene build done");
        }

        /// <summary>同 BuildSceneMenu，但不弹确认框：给自动化 / 命令行用。</summary>
        [MenuItem("1024/重建场景（不弹确认）", false, 4)]
        public static void RebuildNoDialog()
        {
            BuildSceneCore();
            Debug.Log("[1024] 场景已重建（不弹确认）");
        }

        public static void BuildPlayerBatch()
        {
            BuildSceneCore();

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Build"));
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, "1024Move.exe");

            BuildPlayerOptions opt = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(opt);
            Debug.Log("[1024] build result: " + report.summary.result +
                      " size=" + report.summary.totalSize +
                      " errors=" + report.summary.totalErrors);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }

        // ================= 核心 =================

        static void BuildSceneCore()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(TexDir);
            EnsureFolder(MatDir);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var oldCam in Object.FindObjectsOfType<Camera>()) Object.DestroyImmediate(oldCam.gameObject);
            foreach (var oldLight in Object.FindObjectsOfType<Light>()) Object.DestroyImmediate(oldLight.gameObject);

            // ---------- 材质 ----------
            for (int i = 0; i < 3; i++) EnsureLayer(8 + i, "Sil_" + (i + 1));
            EnsureSilhouetteWhiteMaterial();

            Texture2D spotTex = GetRadialTexture("spot_radial", 128);
            Material spotMat = GetSpotMaterial("M_LightSpot", spotTex);

            wallTargetMat = GetTransparentMaterial("M_WallTarget", new Color(1f, 1f, 1f, 0.14f));
            wallLiveMat = GetTransparentMaterial("M_WallLive", new Color(1f, 0.90f, 0.70f, 0.16f));

            Texture2D mask1 = DigitMaskFactory.Get("1");
            Texture2D mask0 = DigitMaskFactory.Get("0");
            Texture2D mask2 = DigitMaskFactory.Get("2");

            Material woodMat = GetOrCreateMaterial("M_Wood", new Color(0.36f, 0.26f, 0.19f), 0.12f);
            Material wallMat = GetOrCreateMaterial("M_Wall", new Color(0.62f, 0.60f, 0.57f), 0.05f);
            Material floorMat = GetOrCreateMaterial("M_Floor", new Color(0.34f, 0.31f, 0.28f), 0.08f);
            Material cardboardMat = GetOrCreateMaterial("M_Cardboard", new Color(0.58f, 0.47f, 0.33f), 0.05f);
            Material metalMat = GetOrCreateMaterial("M_Tin", new Color(0.52f, 0.50f, 0.44f), 0.45f);
            Material frogMat = GetOrCreateMaterial("M_Frog", new Color(0.42f, 0.50f, 0.38f), 0.30f);
            Material tapeMat = GetOrCreateMaterial("M_Tape", new Color(0.22f, 0.22f, 0.24f), 0.35f);
            Material paperMat = GetOrCreateMaterial("M_Paper", new Color(0.86f, 0.83f, 0.74f), 0.02f);

            // ---------- 房间 ----------
            GameObject env = new GameObject("Environment");

            MakeBox("Floor", env.transform, new Vector3(0f, -0.05f, -0.4f), new Vector3(8.6f, 0.1f, 9.2f), floorMat);
            MakeBox("Wall_Back", env.transform, new Vector3(0f, 1.5f, 3.6f), new Vector3(8f, 3f, 0.15f), wallMat);
            MakeBox("Wall_Left", env.transform, new Vector3(-4f, 1.5f, -0.4f), new Vector3(0.15f, 3f, 9.2f), wallMat);
            MakeBox("Wall_Right", env.transform, new Vector3(4f, 1.5f, -0.4f), new Vector3(0.15f, 3f, 9.2f), wallMat);
            MakeBox("Ceiling", env.transform, new Vector3(0f, 3f, -0.4f), new Vector3(8.6f, 0.1f, 9.2f), wallMat);

            // 窗（发光薄片，代替真实窗户）
            Material windowMat = GetOrCreateEmissiveMaterial("M_Window", new Color(0.34f, 0.40f, 0.50f));
            GameObject win = MakeBox("Window", env.transform, new Vector3(-2.6f, 1.9f, 3.44f),
                new Vector3(1.4f, 1.2f, 0.06f), windowMat);
            win.GetComponent<Collider>().enabled = false;

            // 桌子
            GameObject desk = MakeBox("Desk_Top", env.transform, new Vector3(0f, 0.72f, 2.2f),
                new Vector3(2.3f, 0.06f, 0.95f), woodMat);
            MakeBox("Desk_Leg_L", desk.transform, new Vector3(-0.98f, -0.36f, 0f), new Vector3(0.07f, 0.72f, 0.07f), woodMat);
            MakeBox("Desk_Leg_R", desk.transform, new Vector3(0.98f, -0.36f, 0f), new Vector3(0.07f, 0.72f, 0.07f), woodMat);
            MakeBox("Desk_Leg_FL", desk.transform, new Vector3(-0.98f, -0.36f, 0.4f), new Vector3(0.07f, 0.72f, 0.07f), woodMat);
            MakeBox("Desk_Leg_FR", desk.transform, new Vector3(0.98f, -0.36f, 0.4f), new Vector3(0.07f, 0.72f, 0.07f), woodMat);

            // 地上的纸箱与行李箱
            MakeBox("Carton_1", env.transform, new Vector3(-2.6f, 0.28f, -0.8f), new Vector3(0.7f, 0.55f, 0.6f), cardboardMat);
            MakeBox("Carton_2", env.transform, new Vector3(-2.35f, 0.2f, -1.55f), new Vector3(0.5f, 0.4f, 0.45f), cardboardMat);
            MakeBox("Carton_3", env.transform, new Vector3(2.9f, 0.32f, -1.2f), new Vector3(0.8f, 0.64f, 0.7f), cardboardMat);
            MakeBox("Suitcase", env.transform, new Vector3(3.0f, 0.18f, 1.4f), new Vector3(0.6f, 0.36f, 0.42f), tapeMat);

            // ---------- 灯光 ----------
            GameObject lightRoot = new GameObject("Lights");

            GameObject dirGo = new GameObject("Directional Light");
            dirGo.transform.SetParent(lightRoot.transform);
            dirGo.transform.rotation = Quaternion.Euler(46f, -34f, 0f);
            Light dir = dirGo.AddComponent<Light>();
            dir.type = LightType.Directional;
            dir.intensity = 0.75f;
            dir.color = new Color(0.72f, 0.78f, 0.90f);
            dir.shadows = LightShadows.Soft;
            dir.shadowStrength = 0.7f;

            GameObject fillGo = new GameObject("Fill Light");
            fillGo.transform.SetParent(lightRoot.transform);
            fillGo.transform.position = new Vector3(0f, 2.4f, 1.2f);
            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 7f;
            fill.intensity = 0f;
            fill.color = new Color(1f, 0.62f, 0.35f);

            GameObject spotGo = new GameObject("Spot_Desk");
            spotGo.transform.SetParent(lightRoot.transform);
            spotGo.transform.position = new Vector3(0f, 2.9f, 1.9f);
            spotGo.transform.rotation = Quaternion.Euler(72f, 0f, 0f);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 6f;
            spot.spotAngle = 55f;
            spot.intensity = 2.1f;
            spot.color = new Color(1f, 0.93f, 0.82f);
            spot.shadows = LightShadows.Soft;

            // ---------- 玩家（第一人称） ----------
            // 层级：Player(root, CharacterController) → Body(胶囊网格) + Main Camera(子物体)
            // 相机是玩家的子物体，人走到哪镜头就跟到哪。
            GameObject playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(-1.15f, 0.02f, 0.55f);
            playerGo.transform.rotation = Quaternion.identity;

            CharacterController cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.7f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.85f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;

            GameObject bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGo.name = "Body";
            Object.DestroyImmediate(bodyGo.GetComponent<CapsuleCollider>());
            bodyGo.transform.SetParent(playerGo.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            bodyGo.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
            Renderer pr = bodyGo.GetComponent<Renderer>();
            pr.sharedMaterial = GetOrCreateMaterial("M_Player", new Color(0.26f, 0.27f, 0.30f), 0.1f);
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---------- 相机（挂在玩家身上，第一人称） ----------
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(playerGo.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.56f, 0f);
            camGo.transform.localRotation = Quaternion.identity;

            Camera cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 68f;                  // 第一人称给宽一点的视野
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
            cam.nearClipPlane = 0.05f;
            // 没有 AudioListener 的话整个游戏是无声的（频谱也全是 0）
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            // 剪影替身只给剪影相机看，主相机要排除这三个层
            cam.cullingMask = ~((1 << 8) | (1 << 9) | (1 << 10));

            FPSController fps = playerGo.AddComponent<FPSController>();
            fps.cameraPivot = camGo.transform;
            fps.walkSpeed = 3.2f;
            fps.sprintSpeed = 5.4f;
            fps.mouseSensitivity = 2.2f;

            // 拾取持握点：物件飞到这里（相机正前方稍低一点，不挡视线中心）
            GameObject holdGo = new GameObject("HoldAnchor");
            holdGo.transform.SetParent(camGo.transform, false);
            holdGo.transform.localPosition = new Vector3(0f, -0.12f, 0.55f);
            holdGo.transform.localRotation = Quaternion.identity;

            // ---------- 三件旧物 ----------
            GameObject props = new GameObject("Props");
            var interactables = new List<DeskFocusInteractable>();

            // 1. 铁皮青蛙
            GameObject frog = new GameObject("Prop1_Frog");
            frog.transform.SetParent(props.transform);
            frog.transform.position = new Vector3(-0.68f, 0.80f, 2.25f);
            MakeChild("Body", frog.transform, Vector3.zero, new Vector3(0.20f, 0.12f, 0.17f), frogMat);
            MakeChild("LegL", frog.transform, new Vector3(-0.11f, -0.03f, 0.02f), new Vector3(0.06f, 0.05f, 0.10f), frogMat);
            MakeChild("LegR", frog.transform, new Vector3(0.11f, -0.03f, 0.02f), new Vector3(0.06f, 0.05f, 0.10f), frogMat);
            MakeChild("EyeL", frog.transform, new Vector3(-0.06f, 0.07f, 0.05f), new Vector3(0.035f, 0.035f, 0.035f), metalMat);
            MakeChild("EyeR", frog.transform, new Vector3(0.06f, 0.07f, 0.05f), new Vector3(0.035f, 0.035f, 0.035f), metalMat);
            RegisterProp(frog, "生锈的铁皮青蛙", 0, "1",
                new Vector3(0f, 205f, 0f),
                new Vector3(-0.68f, 0.754f, 2.52f), spotMat,
                "楼下有人喊吃饭，声音穿过整个夏天。",
                8, mask1, DialogueConfig.Get(DialogueKeys.FrogHold, "得找个亮一点的地方看看。"), 0f, interactables);

            // 桌子特写时的操作提示（文案表里那条"调整角度，让它的影子投在墙上……"）
            DeskFocusInteractable frogIt = frog.GetComponent<DeskFocusInteractable>();
            if (frogIt != null)
                frogIt.deskHint = DialogueConfig.Get(DialogueKeys.ShadowHint, "调整角度，让它的影子投在墙上……");

            // 2. 复读机磁带
            GameObject tape = new GameObject("Prop2_Cassette");
            tape.transform.SetParent(props.transform);
            tape.transform.position = new Vector3(0.02f, 0.775f, 2.22f);
            MakeChild("Body", tape.transform, Vector3.zero, new Vector3(0.24f, 0.045f, 0.16f), tapeMat);
            MakeChild("ReelL", tape.transform, new Vector3(-0.055f, 0.026f, 0f), new Vector3(0.06f, 0.01f, 0.06f), paperMat);
            MakeChild("ReelR", tape.transform, new Vector3(0.055f, 0.026f, 0f), new Vector3(0.06f, 0.01f, 0.06f), paperMat);
            RegisterProp(tape, "复读机磁带", 1, "0",
                Vector3.zero,
                new Vector3(0.02f, 0.754f, 2.50f), spotMat,
                "磁带里有一段偷偷录下的哼唱。",
                9, mask0, "这盘磁带……卡带了。", 3f, interactables);

            // 3. 半截火车票
            GameObject ticket = new GameObject("Prop3_Ticket");
            ticket.transform.SetParent(props.transform);
            ticket.transform.position = new Vector3(0.72f, 0.762f, 2.18f);
            MakeChild("Body", ticket.transform, Vector3.zero, new Vector3(0.28f, 0.006f, 0.12f), paperMat);
            RegisterProp(ticket, "半截火车票", 2, "2",
                new Vector3(0f, 312f, 0f),
                new Vector3(0.72f, 0.754f, 2.50f), spotMat,
                "开往大城市的那班车，票根只剩一半。",
                10, mask2, "开往大城市的那趟车，就是它。", 0f, interactables);

            // 铁盒
            GameObject box = new GameObject("LockBox");
            box.transform.SetParent(props.transform);
            box.transform.position = new Vector3(-1.02f, 0.795f, 2.02f);
            MakeChild("Shell", box.transform, Vector3.zero, new Vector3(0.26f, 0.09f, 0.18f), metalMat);
            GameObject lid = MakeChild("Lid", box.transform, new Vector3(0f, 0.05f, -0.09f),
                new Vector3(0.26f, 0.02f, 0.18f), metalMat);
            MakeChild("Latch", box.transform, new Vector3(0f, 0.02f, 0.095f), new Vector3(0.05f, 0.03f, 0.02f), metalMat);

            // 退租合同（三件旧物解完才能点）
            GameObject contract = new GameObject("Contract");
            contract.transform.SetParent(props.transform);
            contract.transform.position = new Vector3(0.95f, 0.756f, 1.92f);
            MakeChild("Sheet", contract.transform, Vector3.zero, new Vector3(0.30f, 0.004f, 0.21f), paperMat);

            GameObject contractHit = new GameObject("Hitbox");
            contractHit.transform.SetParent(contract.transform, false);
            BoxCollider contractCol = contractHit.AddComponent<BoxCollider>();
            contractCol.size = new Vector3(0.38f, 0.10f, 0.30f);
            contractCol.center = new Vector3(0f, 0.03f, 0f);
            // 准星要能认出它（否则 HoveredCollider 一直是 null，点不到）
            PromptSource contractPrompt = contractHit.AddComponent<PromptSource>();

            // ---------- 流程 ----------
            GameObject flowGo = new GameObject("GameFlow");
            GameFlowDirector director = flowGo.AddComponent<GameFlowDirector>();
            director.player = fps;
            // 第一人称：没有固定机位，CameraStateRig 不再挂载（cameraRig 留空，流程脚本里都有 null 判断）
            director.interactables = interactables;
            director.contractHotspot = contract;
            director.contractPrompt = contractPrompt;
            director.lockBox = box;
            director.holdAnchor = holdGo.transform;
            // 第一人称：玩家自己走到桌前，拾取不再自动导航、也不闪白光
            director.interactDistance = 2.5f;
            director.walkBeforeFocus = false;
            director.useFocusTransition = false;

            GameObject moodGo = new GameObject("MoodLighting");
            MoodLightingController mood = moodGo.AddComponent<MoodLightingController>();
            mood.mainLight = dir;
            mood.fillLight = fill;
            mood.blend = 0f;
            mood.coldMood.ambient = new Color(0.27f, 0.28f, 0.31f);
            mood.coldMood.mainLight = new Color(0.74f, 0.80f, 0.92f);
            mood.coldMood.mainIntensity = 1.05f;
            mood.coldMood.fogDensity = 0.012f;
            mood.warmMood.ambient = new Color(0.40f, 0.30f, 0.20f);
            mood.warmMood.fogDensity = 0.006f;

            GameObject audioGo = new GameObject("Audio");
            AudioDirector audioDirector = audioGo.AddComponent<AudioDirector>();
            director.sfxSource = audioGo.AddComponent<AudioSource>();
            director.sfxSource.playOnAwake = false;

            // ---------- 桌面灰尘（模块 2） ----------
            // 三件旧物一开始被灰盖住：碰撞体先关掉，擦到 80% 后再由 DustErase 打开
            foreach (var it in interactables)
            {
                Collider c = it.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }

            Material dustMat = GetDustMaterial("M_Dust", GetDustTexture("dust_mask"));
            // 灰尘层：直接复制桌面自己的网格，沿法线外扩 1.5mm —— 完全贴合桌面几何，
            // 不再是一块飘在桌上的 Quad。
            GameObject dustQuad = BuildDustShell(desk, "DeskDust", 0.0015f, dustMat);
            Renderer dustRend = dustQuad.GetComponent<Renderer>();
            dustRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dustRend.receiveShadows = false;

            // 擦灰时跟着鼠标走的小光斑，否则玩家看不见自己在擦哪
            GameObject dustBrush = GameObject.CreatePrimitive(PrimitiveType.Quad);
            dustBrush.name = "DustBrush";
            Object.DestroyImmediate(dustBrush.GetComponent<Collider>());
            dustBrush.transform.localScale = new Vector3(0.26f, 0.26f, 1f);
            Renderer brushRend = dustBrush.GetComponent<Renderer>();
            brushRend.sharedMaterial = spotMat;
            brushRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dustBrush.SetActive(false);

            DustErase dust = dustQuad.AddComponent<DustErase>();
            dust.clearThreshold = 0.8f;          // 设计稿：擦掉 80%
            dust.brushRadius = 30f;
            dust.textureSize = 256;
            dust.fadeDuration = 0.9f;
            dust.player = playerGo.transform;
            dust.maxPlayerDistance = 2.0f;      // 第一人称：走到桌前 2 米内才能擦
            dust.maxRayDistance = 3.5f;
            dust.aimFromCenter = true;          // 用准星（屏幕中心）擦，移动视角即涂抹
            dust.revealObjects = new[] { frog, tape, ticket };

            // 桌面光斑是半透明（ZWrite Off），不关掉会"透"过同样半透明的灰尘显示出来
            var dustSpots = new List<Renderer>();
            foreach (var it in interactables)
                if (it.lightSpot != null) dustSpots.Add(it.lightSpot);
            dust.showAfterClear = dustSpots.ToArray();

            dust.audio = audioDirector;
            dust.brushMarker = dustBrush;
            dust.hoverHint = DialogueConfig.Get(DialogueKeys.DustWipePrompt, "[按住左键 擦除桌面]");
            director.dust = dust;      // 存档要记灰尘状态

            // ---------- 老电视（模块 3） ----------
            GameObject tvRoot = new GameObject("Prop4_TV");
            tvRoot.transform.SetParent(props.transform);
            tvRoot.transform.position = new Vector3(-2.85f, 1.12f, 3.02f);

            MakeBox("Stand", tvRoot.transform, new Vector3(0f, -0.62f, 0f), new Vector3(0.50f, 0.66f, 0.40f), woodMat);
            MakeBox("Body", tvRoot.transform, Vector3.zero, new Vector3(0.86f, 0.58f, 0.50f), metalMat);

            Material tvScreenMat = GetOrCreateEmissiveMaterial("M_TVScreen", new Color(0.16f, 0.18f, 0.23f));
            GameObject tvScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            tvScreen.name = "Screen";
            tvScreen.transform.SetParent(tvRoot.transform, false);
            tvScreen.transform.localPosition = new Vector3(0f, 0.02f, -0.255f);
            tvScreen.transform.localScale = new Vector3(0.70f, 0.44f, 1f);
            Object.DestroyImmediate(tvScreen.GetComponent<Collider>());
            Renderer tvScreenRend = tvScreen.GetComponent<Renderer>();
            tvScreenRend.sharedMaterial = tvScreenMat;
            tvScreenRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tvScreenRend.receiveShadows = false;

            GameObject antL = MakeChild("AntennaL", tvRoot.transform, new Vector3(-0.12f, 0.40f, 0f), new Vector3(0.015f, 0.28f, 0.015f), metalMat);
            antL.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            GameObject antR = MakeChild("AntennaR", tvRoot.transform, new Vector3(0.12f, 0.40f, 0f), new Vector3(0.015f, 0.28f, 0.015f), metalMat);
            antR.transform.localRotation = Quaternion.Euler(0f, 0f, -22f);

            GameObject tvSpotGo = new GameObject("Spot_TV");
            tvSpotGo.transform.SetParent(lightRoot.transform);
            tvSpotGo.transform.position = new Vector3(-2.35f, 1.60f, 2.50f);
            Vector3 tvAim = new Vector3(-0.10f, 1.15f, 3.40f);
            tvSpotGo.transform.rotation = Quaternion.LookRotation((tvAim - tvSpotGo.transform.position).normalized);
            Light tvSpot = tvSpotGo.AddComponent<Light>();
            tvSpot.type = LightType.Spot;
            tvSpot.range = 9f;
            tvSpot.spotAngle = 72f;
            tvSpot.intensity = 0f;
            tvSpot.color = new Color(0.82f, 0.88f, 1f);
            tvSpot.shadows = LightShadows.Soft;

            TVInteraction tv = tvRoot.AddComponent<TVInteraction>();
            tv.player = playerGo.transform;      // 距离判定用
            tv.maxPlayerDistance = 2.0f;
            tv.director = director;
            tv.spotLight = tvSpot;
            tv.screenRenderer = tvScreenRend;
            tv.audio = audioDirector;
            tv.clickWindow = 1.5f;               // 设计稿：1.5 秒
            tv.clicksRequired = 6;               // 设计稿：6 下
            tv.lightOnIntensity = 10f;           // 设计稿：0 → 10
            tv.lightHoldSeconds = 4f;            // 设计稿：维持 4 秒
            tv.hoverHint = "点击拍打";

            // ---------- UI ----------
            Font uiFont = LoadUiFont();
            Canvas canvas = BuildUI(uiFont,
                out CanvasGroup deskHint, out CanvasGroup exploreHint, out Text exploreHintText,
                out Text subtitle, out PasswordLockUI lockUi, out ContractScratchUI contractUi,
                out CanvasGroup fade, out FinaleController finale, out CanvasGroup posterPanel,
                out RawImage posterShot, out Text posterCaption, out Button collectButton);

            director.passwordLock = lockUi;
            director.contractUI = contractUi;
            director.finale = finale;
            director.deskHintGroup = deskHint;
            director.exploreHintGroup = exploreHint;
            director.exploreHintText = exploreHintText;
            Transform deskHintNode = canvas.transform.Find("Hint_Desk");
            if (deskHintNode != null) director.deskHintText = deskHintNode.GetComponent<Text>();

            // ---------- 全局基础系统（V3.0 设计稿） ----------
            BuildGlobalSystems(canvas, uiFont, director, audioDirector);
            if (_pendingIntro != null) _pendingIntro.player = fps;

            finale.mood = mood;
            finale.fade = fade;

            foreach (var it in interactables)
            {
                if (it.memoryCue != null)
                {
                    it.memoryCue.subtitle = subtitle;
                    it.memoryCue.ambienceSource = audioGo.AddComponent<AudioSource>();
                    it.memoryCue.voiceSource = audioGo.AddComponent<AudioSource>();
                }
            }

            // ---------- 自动化 ----------
            GameObject smokeGo = new GameObject("SmokeTest");
            SmokeTest smoke = smokeGo.AddComponent<SmokeTest>();
            smoke.director = director;
            smoke.contract = contractUi;
            smoke.finale = finale;

            GameObject shotGo = new GameObject("ScreenshotRunner");
            ScreenshotRunner shot = shotGo.AddComponent<ScreenshotRunner>();
            shot.director = director;

            // ---------- 保存 ----------
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[1024] 场景已生成: " + ScenePath);
        }

        // ================= 单件旧物装配 =================

        static void RegisterProp(GameObject go, string displayName, int slot, string digit,
            Vector3 targetEuler, Vector3 spotPos, Material spotMat, string memoryText,
            int silhouetteLayer, Texture2D digitMask, string osLine, float rewindTurns,
            List<DeskFocusInteractable> list)
        {
            // 按子物体包围盒生成贴合的可点区域，否则默认 1 米立方体会和邻件打架
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            Renderer[] rends = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends.Length; i++)
            {
                if (!has) { b = rends[i].bounds; has = true; }
                else b.Encapsulate(rends[i].bounds);
            }
            BoxCollider bc = go.AddComponent<BoxCollider>();
            if (has)
            {
                bc.center = go.transform.InverseTransformPoint(b.center);
                bc.size = b.size * 1.15f;
            }

            DragRotateBody rot = go.AddComponent<DragRotateBody>();
            rot.dragSensitivity = 5f;
            rot.smoothing = 14f;
            rot.angularDamping = 3.0f;
            rot.inertiaRetain = 0.35f;
            rot.interactable = false;
            rot.debugLog = true;

            // 两种关卡类型：转角度 / 画圈倒带
            ShadowAlignPuzzle puzzle = null;
            CircularRewind rewind = null;
            if (rewindTurns > 0f)
            {
                rewind = go.AddComponent<CircularRewind>();
                rewind.turnsRequired = rewindTurns;
                rewind.visualizer = _spectrum;
            }
            else
            {
                puzzle = go.AddComponent<ShadowAlignPuzzle>();
                puzzle.rotatingPart = go.transform;
                puzzle.rotator = rot;
                puzzle.captureCurrentOnStart = false;
                puzzle.targetRotation = Quaternion.Euler(targetEuler);
                puzzle.enterTolerance = 26f;
                puzzle.exitTolerance = 30f;
                puzzle.useMagnet = true;
                // 整圈都要有亮度反馈，玩家才能"转到最亮的地方"
                puzzle.fadeRange = 110f;
                // 强磁吸：转到大概方向就会被吸过去，不用死抠角度
                puzzle.assistRange = 85f;
                puzzle.assistRate = 3.5f;
            }

            MemoryCuePlayer cue = go.AddComponent<MemoryCuePlayer>();
            cue.text = memoryText;
            cue.charsPerSecond = 17f;
            cue.holdAfterText = 1.2f;

            // 桌面上的暖色光晕（辅助反馈，配合墙上的剪影用）
            GameObject spotGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spotGo.name = "LightSpot_" + displayName;
            Object.DestroyImmediate(spotGo.GetComponent<Collider>());
            spotGo.transform.position = spotPos;
            spotGo.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            spotGo.transform.localScale = new Vector3(0.95f, 0.95f, 1f);
            spotGo.GetComponent<Renderer>().sharedMaterial = spotMat;
            spotGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 墙上的投影：目标轮廓（暗）+ 实时剪影（亮）
            SilhouetteProjection projection = BuildProjection(go, displayName, silhouetteLayer, digitMask);

            DeskFocusInteractable it = go.AddComponent<DeskFocusInteractable>();
            it.displayName = displayName;
            it.passwordSlot = slot;
            it.digit = digit;
            it.osLine = osLine;
            it.hoverHint = "点击查看";
            it.rotator = rot;
            it.anglePuzzle = puzzle;
            it.rewindPuzzle = rewind;
            it.deskHint = rewind != null ? "按住左键画圈" : "按住左键拖动";
            it.memoryCue = cue;
            it.lightSpot = spotGo.GetComponent<Renderer>();
            it.projection = projection;

            list.Add(it);
        }

        /// <summary>
        /// 给一件旧物搭一套"剪影投影"：正交相机 + RenderTexture + 墙上两块板。
        /// 相机只渲染这件旧物自己的层，所以三件旧物的影子互不干扰。
        /// </summary>
        static SilhouetteProjection BuildProjection(GameObject prop, string displayName,
            int layer, Texture2D digitMask)
        {
            Vector3 p = prop.transform.position;

            GameObject root = new GameObject("Projection_" + displayName);
            root.transform.SetParent(prop.transform.parent, false);

            // 墙上的目标轮廓（先建立，后被实时影子盖住）
            // 墙面在 z = 3.525 ~ 3.675，投影板必须贴在墙前面，否则会埋进墙里
            GameObject targetQuad = MakeWallPanel("Target_" + displayName,
                new Vector3(p.x, 1.10f, 3.505f), 0.58f, wallTargetMat);
            targetQuad.transform.SetParent(root.transform, true);

            GameObject liveQuad = MakeWallPanel("Live_" + displayName,
                new Vector3(p.x, 1.10f, 3.475f), 0.58f, wallLiveMat);
            liveQuad.transform.SetParent(root.transform, true);

            // 剪影相机：正对旧物，正交
            GameObject camGo = new GameObject("SilCam_" + displayName);
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.position = new Vector3(p.x, p.y, p.z - 2.0f);
            camGo.transform.rotation = Quaternion.identity;
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.17f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 4.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << layer;
            cam.depth = -10;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            cam.renderingPath = RenderingPath.Forward;

            // 旧物本体留在默认层（主相机可见），由 SilhouetteProxy 复制一份白色替身到剪影层
            SilhouetteProxy proxy = prop.AddComponent<SilhouetteProxy>();
            proxy.layer = layer;
            proxy.whiteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/SilhouetteWhite.mat");

            SilhouetteProjection proj = root.AddComponent<SilhouetteProjection>();
            proj.silhouetteCamera = cam;
            proj.liveQuad = liveQuad.GetComponent<Renderer>();
            proj.targetQuad = targetQuad.GetComponent<Renderer>();
            proj.targetMask = digitMask;
            proj.resolution = 256;

            return proj;
        }

        static GameObject MakeWallPanel(string name, Vector3 pos, float size, Material mat)
        {
            // 用 Quad 而不是 Cube：Sprites/Default 是 Cull Off，两面都看得见，且不会有立方体侧面的拉伸
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(size, size, 1f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        static void EnsureLayer(int index, string layerName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty layers = so.FindProperty("layers");
            if (layers == null || index >= layers.arraySize) return;
            SerializedProperty slot = layers.GetArrayElementAtIndex(index);
            if (slot.stringValue != layerName)
            {
                slot.stringValue = layerName;
                so.ApplyModifiedProperties();
            }
        }

        // ================= UI =================

        /// <summary>V3.0 设计稿的全局基础系统：内心 OS、记忆穿梭白幕、准星。</summary>
        static AudioVisualizer _spectrum;

        static void BuildGlobalSystems(Canvas canvas, Font font, GameFlowDirector director, AudioDirector audio)
        {
            // --- 内心 OS（屏幕下方打字机） ---
            Text osText = NewText(canvas.transform, "OS", "", 28,
                new Color(0.96f, 0.94f, 0.89f, 1f), font);
            Anchor(osText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -300f), new Vector2(1320f, 120f));
            osText.alignment = TextAnchor.MiddleCenter;
            osText.raycastTarget = false;
            osText.lineSpacing = 1.25f;

            CanvasGroup osGroup = osText.gameObject.AddComponent<CanvasGroup>();
            osGroup.alpha = 0f;

            InnerMonologue os = osText.gameObject.AddComponent<InnerMonologue>();
            os.target = osText;
            os.group = osGroup;
            os.font = font;
            os.alpha = 0.8f;
            os.charsPerSecond = 20f;
            os.holdSeconds = 2.0f;

            // 全局对话管理：所有打字机文本都必须经过它排队（同一时刻只播一条）
            GameObject dialogueGo = new GameObject("Dialogue");
            DialogueManager dialogue = dialogueGo.AddComponent<DialogueManager>();
            dialogue.monologue = os;

            // --- 记忆穿梭白幕（放在所有面板之上） ---
            RectTransform flashRt = NewRect(canvas.transform, "Flash");
            StretchFull(flashRt);
            Image flashImg = flashRt.gameObject.AddComponent<Image>();
            flashImg.color = Color.white;
            flashImg.raycastTarget = false;
            CanvasGroup flashCg = flashRt.gameObject.AddComponent<CanvasGroup>();
            flashCg.alpha = 0f;

            MemoryTransition tr = flashRt.gameObject.AddComponent<MemoryTransition>();
            tr.flash = flashCg;
            tr.flashImage = flashImg;

            // --- 准星（最上层） ---
            RectTransform chRt = NewRect(canvas.transform, "Crosshair");
            CrosshairUI cross = chRt.gameObject.AddComponent<CrosshairUI>();

            // --- 抹布：擦灰的前置道具，放在纸箱旁的地上 ---
            Material ragMat = GetOrCreateMaterial("M_Rag", new Color(0.72f, 0.69f, 0.62f), 0.05f);
            GameObject ragGo = new GameObject("Rag");
            ragGo.transform.position = new Vector3(-1.95f, 0.015f, -1.05f);
            ragGo.transform.rotation = Quaternion.Euler(0f, 24f, 0f);
            MakeChild("Body", ragGo.transform, Vector3.zero, new Vector3(0.34f, 0.02f, 0.28f), ragMat);
            GameObject ragFold = MakeChild("Fold", ragGo.transform, new Vector3(0.02f, 0.026f, 0f),
                new Vector3(0.26f, 0.03f, 0.18f), ragMat);
            ragFold.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);

            BoxCollider ragCol = ragGo.AddComponent<BoxCollider>();
            ragCol.size = new Vector3(0.46f, 0.16f, 0.40f);
            ragCol.center = new Vector3(0f, 0.05f, 0f);

            RagPickup rag = ragGo.AddComponent<RagPickup>();
            rag.crosshair = cross;
            rag.player = director.player != null ? director.player.transform : null;
            rag.holdAnchor = Camera.main != null ? Camera.main.transform : null;
            rag.audio = audio;
            rag.director = director;
            rag.hoverHint = DialogueConfig.Get(DialogueKeys.RagPickupPrompt, "[F 拾取抹布]");
            if (director.dust != null) director.dust.rag = rag;
            director.rag = rag;

            // ---------- 第一阶段：开局抱着的纸箱 ----------
            Material cartonMat = GetOrCreateMaterial("M_Carton", new Color(0.58f, 0.47f, 0.33f), 0.05f);
            GameObject carryGo = new GameObject("Box_Carry");
            MakeBox("Body", carryGo.transform, Vector3.zero, new Vector3(0.64f, 0.52f, 0.58f), cartonMat);
            CarryingBox carrying = carryGo.AddComponent<CarryingBox>();
            carrying.director = director;
            carrying.player = director.player;
            carrying.holdAnchor = Camera.main != null ? Camera.main.transform : null;
            carrying.audio = audio;

            // ---------- 第二阶段：满屋子的杂物（纯干扰项，全是日常破烂） ----------
            Material paperScrap = GetOrCreateMaterial("M_Paper", new Color(0.86f, 0.83f, 0.74f), 0.02f);
            Material tinScrap = GetOrCreateMaterial("M_Tin", new Color(0.55f, 0.57f, 0.60f), 0.55f);
            Material clothScrap = GetOrCreateMaterial("M_Tape", new Color(0.24f, 0.23f, 0.22f), 0.30f);
            GameObject cartonTarget = GameObject.Find("Carton_1");

            // 地上
            CreateClutter("PaperBall_1", new Vector3(-2.90f, 0.07f, 0.40f), Vector3.one * 0.14f, new Vector3(0f, 20f, 0f), PrimitiveType.Sphere, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("PaperBall_2", new Vector3(-1.40f, 0.07f, -1.90f), Vector3.one * 0.13f, new Vector3(10f, 50f, 0f), PrimitiveType.Sphere, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("PaperBall_3", new Vector3(1.90f, 0.07f, -2.20f), Vector3.one * 0.12f, Vector3.zero, PrimitiveType.Sphere, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Can_1", new Vector3(-2.20f, 0.09f, -0.20f), new Vector3(0.09f, 0.08f, 0.09f), new Vector3(0f, 0f, 80f), PrimitiveType.Cylinder, tinScrap, cross, director, audio, cartonTarget);
            CreateClutter("Can_2", new Vector3(2.40f, 0.09f, 0.20f), new Vector3(0.09f, 0.08f, 0.09f), new Vector3(0f, 0f, -70f), PrimitiveType.Cylinder, tinScrap, cross, director, audio, cartonTarget);
            CreateClutter("Sock_1", new Vector3(-0.60f, 0.04f, -2.40f), new Vector3(0.22f, 0.06f, 0.12f), new Vector3(0f, 35f, 0f), PrimitiveType.Cube, clothScrap, cross, director, audio, cartonTarget);
            CreateClutter("Sock_2", new Vector3(0.80f, 0.04f, -2.60f), new Vector3(0.20f, 0.06f, 0.11f), new Vector3(0f, -25f, 0f), PrimitiveType.Cube, clothScrap, cross, director, audio, cartonTarget);
            CreateClutter("Magazine_1", new Vector3(-3.20f, 0.02f, 1.60f), new Vector3(0.34f, 0.03f, 0.24f), new Vector3(0f, 15f, 0f), PrimitiveType.Cube, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Magazine_2", new Vector3(2.90f, 0.02f, 2.40f), new Vector3(0.32f, 0.03f, 0.22f), new Vector3(0f, -40f, 0f), PrimitiveType.Cube, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Bottle_1", new Vector3(-3.40f, 0.13f, -1.40f), new Vector3(0.10f, 0.13f, 0.10f), Vector3.zero, PrimitiveType.Cylinder, tinScrap, cross, director, audio, cartonTarget);
            CreateClutter("Cup_1", new Vector3(1.40f, 0.06f, -1.10f), new Vector3(0.09f, 0.06f, 0.09f), Vector3.zero, PrimitiveType.Cylinder, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Tissue_1", new Vector3(-1.00f, 0.02f, 0.90f), new Vector3(0.18f, 0.04f, 0.14f), new Vector3(0f, 60f, 0f), PrimitiveType.Cube, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Remote_1", new Vector3(0.50f, 0.02f, -1.60f), new Vector3(0.20f, 0.03f, 0.07f), new Vector3(0f, 10f, 0f), PrimitiveType.Cube, clothScrap, cross, director, audio, cartonTarget);
            CreateClutter("Cable_1", new Vector3(-2.60f, 0.02f, 2.80f), new Vector3(0.30f, 0.02f, 0.03f), new Vector3(0f, 70f, 0f), PrimitiveType.Cube, clothScrap, cross, director, audio, cartonTarget);

            // 桌上两件（浮在灰层之上，一进门就能看到"这屋子很乱"）
            CreateClutter("Newspaper_D", new Vector3(1.00f, 0.80f, 2.55f), new Vector3(0.30f, 0.02f, 0.22f), new Vector3(0f, 20f, 0f), PrimitiveType.Cube, paperScrap, cross, director, audio, cartonTarget);
            CreateClutter("Magazine_D", new Vector3(-1.00f, 0.80f, 2.55f), new Vector3(0.28f, 0.02f, 0.20f), new Vector3(0f, -15f, 0f), PrimitiveType.Cube, paperScrap, cross, director, audio, cartonTarget);

            // ---------- 第三阶段：铁盒被旧衣服盖住 ----------
            GameObject lockBoxGo = GameObject.Find("LockBox");
            if (lockBoxGo != null)
            {
                BoxCollider boxCol = lockBoxGo.GetComponent<BoxCollider>();
                if (boxCol == null) boxCol = lockBoxGo.AddComponent<BoxCollider>();
                boxCol.size = new Vector3(0.30f, 0.18f, 0.22f);
                boxCol.center = new Vector3(0f, 0.04f, 0f);

                PromptSource boxPrompt = lockBoxGo.AddComponent<PromptSource>();
                IronBoxInteract iron = lockBoxGo.AddComponent<IronBoxInteract>();
                iron.crosshair = cross;
                iron.director = director;
                iron.prompt = boxPrompt;
                iron.audio = audio;
                iron.enabled = false;        // 掀开衣服后才可用
                boxPrompt.Clear();

                Material clothMat = GetOrCreateMaterial("M_Paper", new Color(0.86f, 0.83f, 0.74f), 0.02f);
                GameObject coverGo = MakeBox("CoverCloth", lockBoxGo.transform,
                    new Vector3(0f, 0.072f, 0f), new Vector3(0.34f, 0.025f, 0.26f), clothMat);

                PromptSource clothPrompt = coverGo.AddComponent<PromptSource>();
                ClothCover cloth = coverGo.AddComponent<ClothCover>();
                cloth.crosshair = cross;
                cloth.director = director;
                cloth.prompt = clothPrompt;
                cloth.audio = audio;
                cloth.ironBox = iron;
            }

            // --- 音频频谱（倒带成功后跳出来） ---
            RectTransform specRoot = NewRect(canvas.transform, "Spectrum");
            Anchor(specRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 340f), new Vector2(780f, 140f));
            CanvasGroup specCg = specRoot.gameObject.AddComponent<CanvasGroup>();
            specCg.alpha = 0f;
            specCg.blocksRaycasts = false;

            const int barCount = 28;
            Image[] bars = new Image[barCount];
            for (int i = 0; i < barCount; i++)
            {
                RectTransform b = NewRect(specRoot, "Bar" + i);
                b.anchorMin = b.anchorMax = new Vector2(0f, 0f);
                b.pivot = new Vector2(0.5f, 0f);
                b.anchoredPosition = new Vector2(14f + i * 27f, 0f);
                b.sizeDelta = new Vector2(9f, 4f);

                Image img = b.gameObject.AddComponent<Image>();
                img.color = new Color(1f, 0.82f, 0.55f, 0.9f);
                img.raycastTarget = false;
                bars[i] = img;
            }

            AudioVisualizer viz = specRoot.gameObject.AddComponent<AudioVisualizer>();
            viz.source = audio != null ? audio.HumSource : null;
            viz.bars = bars;
            viz.group = specCg;
            _spectrum = viz;

            director.monologue = os;
            director.transition = tr;
            director.crosshair = cross;

            // --- 开场黑幕（比准星低一层，准星始终可见） ---
            RectTransform introRt = NewRect(canvas.transform, "IntroBlack");
            StretchFull(introRt);
            Image introImg = introRt.gameObject.AddComponent<Image>();
            introImg.color = Color.black;
            introImg.raycastTarget = false;
            CanvasGroup introCg = introRt.gameObject.AddComponent<CanvasGroup>();
            introCg.alpha = 0f;
            introCg.blocksRaycasts = false;

            IntroSequence intro = introRt.gameObject.AddComponent<IntroSequence>();
            intro.black = introCg;
            intro.monologue = os;
            intro.line = DialogueConfig.Get(DialogueKeys.IntroStartBox, "毕业五年，第 1024 次打包……");
            _pendingIntro = intro;

            // --- 主菜单（开始游戏 / 继续游戏 / 设置） ---
            RectTransform menuRt = NewRect(canvas.transform, "MainMenu");
            StretchFull(menuRt);
            Image menuBg = menuRt.gameObject.AddComponent<Image>();
            menuBg.color = new Color(0.035f, 0.035f, 0.045f, 1f);
            menuBg.raycastTarget = true;
            CanvasGroup menuCg = menuRt.gameObject.AddComponent<CanvasGroup>();
            menuCg.alpha = 1f;

            Text menuTitle = NewText(menuRt, "Title", "第 1024 次 搬 家", 72,
                new Color(0.95f, 0.93f, 0.88f, 1f), font);
            Anchor(menuTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 200f), new Vector2(1400f, 120f));
            menuTitle.raycastTarget = false;

            Text menuSub = NewText(menuRt, "Subtitle", "一 个 关 于 搬 家 的 记 忆", 30,
                new Color(1f, 1f, 1f, 0.42f), font);
            Anchor(menuSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 110f), new Vector2(1400f, 60f));
            menuSub.raycastTarget = false;

            Text startLabel, continueLabel;
            Button startBtn = MakeButton(menuRt, "StartButton", "开 始 游 戏",
                new Vector2(0f, 0f), new Vector2(380f, 80f), font, out startLabel);
            Button continueBtn = MakeButton(menuRt, "ContinueButton", "继 续 游 戏",
                new Vector2(0f, -100f), new Vector2(380f, 80f), font, out continueLabel);

            Text menuSaveInfo = NewText(menuRt, "SaveInfo", "", 22, new Color(1f, 1f, 1f, 0.4f), font);
            Anchor(menuSaveInfo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -178f), new Vector2(900f, 40f));
            menuSaveInfo.raycastTarget = false;

            Text menuHint = NewText(menuRt, "Hint", "戴上耳机，点击开始", 24, new Color(1f, 1f, 1f, 0.3f), font);
            Anchor(menuHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -260f), new Vector2(1000f, 50f));
            menuHint.raycastTarget = false;

            // 主界面右上角的设置齿轮
            RectTransform menuGearRt = NewRect(menuRt, "SettingsButton");
            Anchor(menuGearRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-56f, -56f), new Vector2(64f, 64f));
            Image menuGearBg = menuGearRt.gameObject.AddComponent<Image>();
            menuGearBg.color = new Color(1f, 1f, 1f, 0.10f);
            Button menuGear = menuGearRt.gameObject.AddComponent<Button>();
            menuGear.targetGraphic = menuGearBg;
            RectTransform menuGearIconRt = NewRect(menuGearRt, "Icon");
            StretchFull(menuGearIconRt);
            menuGearIconRt.offsetMin = new Vector2(15f, 15f);
            menuGearIconRt.offsetMax = new Vector2(-15f, -15f);
            Image menuGearIcon = menuGearIconRt.gameObject.AddComponent<Image>();
            menuGearIcon.sprite = UiIcons.Gear;
            menuGearIcon.color = new Color(1f, 1f, 1f, 0.85f);
            menuGearIcon.raycastTarget = false;

            MainMenu menu = menuRt.gameObject.AddComponent<MainMenu>();
            menu.panel = menuCg;
            menu.startButton = startBtn;
            menu.continueButton = continueBtn;
            menu.continueLabel = continueLabel;
            menu.saveInfoText = menuSaveInfo;
            menu.titleText = menuTitle;
            menu.subtitleText = menuSub;
            menu.hintText = menuHint;
            menu.intro = intro;
            menu.player = director.player;
            menu.crosshair = cross;
            menu.flow = director;
            menu.settingsButton = menuGear;

            // 开场改由主菜单触发（原来是一进场景就播）
            intro.autoStart = false;

            // --- 暂停 / 系统菜单（Esc 呼出）：毛玻璃背景 + 四个按钮 ---
            RectTransform pauseRt = NewRect(canvas.transform, "PauseMenu");
            StretchFull(pauseRt);
            Image pauseBg = pauseRt.gameObject.AddComponent<Image>();
            pauseBg.color = new Color(0.02f, 0.02f, 0.035f, 0.80f);
            pauseBg.raycastTarget = true;

            // 全屏模糊：GrabPass 抓当前画面做多抽样模糊，再叠半透明深色
            Material blurMat = GetBlurMaterial("M_UIBlur");
            if (blurMat != null) pauseBg.material = blurMat;

            CanvasGroup pauseCg = pauseRt.gameObject.AddComponent<CanvasGroup>();
            pauseCg.alpha = 0f;
            pauseCg.blocksRaycasts = false;

            Text pauseTitle = NewText(pauseRt, "Title", "暂 停", 56, new Color(0.95f, 0.93f, 0.88f, 1f), font);
            Anchor(pauseTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 250f), new Vector2(1000f, 90f));
            pauseTitle.raycastTarget = false;

            Text resumeLabel, saveLabel, homeLabel, quitLabel;
            Button resumeBtn = MakeButton(pauseRt, "ResumeButton", "继 续 游 戏",
                new Vector2(0f, 110f), new Vector2(360f, 72f), font, out resumeLabel);
            Button saveBtn = MakeButton(pauseRt, "SaveButton", "保 存 游 戏",
                new Vector2(0f, 20f), new Vector2(360f, 72f), font, out saveLabel);
            Button homeBtn = MakeButton(pauseRt, "HomeButton", "返 回 主 界 面",
                new Vector2(0f, -70f), new Vector2(360f, 72f), font, out homeLabel);
            Button quitBtn = MakeButton(pauseRt, "QuitButton", "退 出 游 戏",
                new Vector2(0f, -160f), new Vector2(360f, 72f), font, out quitLabel);

            // 文字右边挪一点，给左边的小房子图标让位
            homeLabel.rectTransform.offsetMin = new Vector2(56f, 0f);

            RectTransform homeIconRt = NewRect(homeBtn.transform, "HouseIcon");
            Anchor(homeIconRt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(44f, 0f), new Vector2(40f, 40f));
            Image homeIcon = homeIconRt.gameObject.AddComponent<Image>();
            homeIcon.raycastTarget = false;
            homeIcon.color = new Color(1f, 0.96f, 0.88f, 0.95f);

            Text toastText = NewText(pauseRt, "Toast", "", 26, new Color(0.85f, 0.95f, 0.85f, 0.95f), font);
            Anchor(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -240f), new Vector2(600f, 50f));
            toastText.raycastTarget = false;

            Text pauseHintText = NewText(pauseRt, "Hint", "按住 Ctrl 可以呼出鼠标来点按钮", 22,
                new Color(1f, 1f, 1f, 0.35f), font);
            Anchor(pauseHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(1200f, 40f));
            pauseHintText.raycastTarget = false;

            // --- 右上角常驻工具条：存档标志 + 设置齿轮 + 小房子（小房子呼出暂停菜单） ---
            RectTransform topBarRt = NewRect(canvas.transform, "TopBar");
            Anchor(topBarRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-150f, -52f), new Vector2(300f, 76f));

            // 存档标志（小房子左边）
            RectTransform badgeRt = NewRect(topBarRt, "SaveBadge");
            Anchor(badgeRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-192f, -38f), new Vector2(120f, 64f));

            RectTransform badgeIconRt = NewRect(badgeRt, "Icon");
            Anchor(badgeIconRt, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, 26f), new Vector2(30f, 30f));
            Image badgeIcon = badgeIconRt.gameObject.AddComponent<Image>();
            badgeIcon.sprite = UiIcons.Save;
            badgeIcon.raycastTarget = false;
            badgeIcon.color = new Color(1f, 1f, 1f, 0.22f);

            Text badgeLabel = NewText(badgeRt, "Label", "未存档", 14, new Color(1f, 1f, 1f, 0.25f), font);
            Anchor(badgeLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(150f, 22f));
            badgeLabel.alignment = TextAnchor.MiddleCenter;
            badgeLabel.raycastTarget = false;

            // 设置齿轮
            RectTransform gearRt = NewRect(topBarRt, "SettingsButton");
            Anchor(gearRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-92f, -38f), new Vector2(52f, 52f));
            Image gearBg = gearRt.gameObject.AddComponent<Image>();
            gearBg.color = new Color(1f, 1f, 1f, 0.10f);
            Button gearBtn = gearRt.gameObject.AddComponent<Button>();
            gearBtn.targetGraphic = gearBg;
            RectTransform gearIconRt = NewRect(gearRt, "Icon");
            StretchFull(gearIconRt);
            gearIconRt.offsetMin = new Vector2(12f, 12f);
            gearIconRt.offsetMax = new Vector2(-12f, -12f);
            Image gearIcon = gearIconRt.gameObject.AddComponent<Image>();
            gearIcon.sprite = UiIcons.Gear;
            gearIcon.color = new Color(1f, 1f, 1f, 0.85f);
            gearIcon.raycastTarget = false;

            // 小房子：呼出暂停菜单
            RectTransform houseRt = NewRect(topBarRt, "HomeShortcut");
            Anchor(houseRt, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-36f, -38f), new Vector2(58f, 58f));
            Image houseBg = houseRt.gameObject.AddComponent<Image>();
            houseBg.color = new Color(1f, 1f, 1f, 0.10f);
            Button houseBtn = houseRt.gameObject.AddComponent<Button>();
            houseBtn.targetGraphic = houseBg;
            RectTransform houseIconRt2 = NewRect(houseRt, "Icon");
            StretchFull(houseIconRt2);
            houseIconRt2.offsetMin = new Vector2(13f, 13f);
            houseIconRt2.offsetMax = new Vector2(-13f, -13f);
            Image houseIcon2 = houseIconRt2.gameObject.AddComponent<Image>();
            houseIcon2.sprite = UiIcons.House;
            houseIcon2.raycastTarget = false;
            houseIcon2.color = new Color(1f, 0.97f, 0.9f, 0.95f);

            TopBarUI topBar = topBarRt.gameObject.AddComponent<TopBarUI>();
            topBar.saveIcon = badgeIcon;
            topBar.saveLabel = badgeLabel;
            topBar.houseButton = houseBtn;
            topBar.houseIcon = houseIcon2;
            topBar.gearButton = gearBtn;
            topBar.gearIcon = gearIcon;

            PauseMenu pause = pauseRt.gameObject.AddComponent<PauseMenu>();
            pause.panel = pauseCg;
            pause.panelImage = pauseBg;
            pause.resumeButton = resumeBtn;
            pause.saveButton = saveBtn;
            pause.homeButton = homeBtn;
            pause.quitButton = quitBtn;
            pause.homeIcon = homeIcon;
            pause.toastText = toastText;
            pause.player = director.player;
            pause.crosshair = cross;
            pause.hintText = pauseHintText;
            pause.audio = audio;
            pause.flow = director;

            // --- 设置面板：音量 / 亮度（主界面和游戏里共用一个） ---
            RectTransform settingsRt = NewRect(canvas.transform, "SettingsPanel");
            StretchFull(settingsRt);
            Image settingsBg = settingsRt.gameObject.AddComponent<Image>();
            settingsBg.color = new Color(0.02f, 0.02f, 0.035f, 0.80f);
            settingsBg.raycastTarget = true;
            if (blurMat != null) settingsBg.material = blurMat;

            CanvasGroup settingsCg = settingsRt.gameObject.AddComponent<CanvasGroup>();
            settingsCg.alpha = 0f;
            settingsCg.blocksRaycasts = false;

            Text settingsTitle = NewText(settingsRt, "Title", "设 置", 48,
                new Color(0.95f, 0.93f, 0.88f, 1f), font);
            Anchor(settingsTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 220f), new Vector2(800f, 80f));
            settingsTitle.raycastTarget = false;

            Text volCaption = NewText(settingsRt, "VolumeLabel", "音量", 28,
                new Color(1f, 1f, 1f, 0.75f), font);
            Anchor(volCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, 100f), new Vector2(160f, 40f));
            volCaption.alignment = TextAnchor.MiddleRight;
            volCaption.raycastTarget = false;

            Text volValue;
            Slider volSlider = MakeSlider(settingsRt, "VolumeSlider", new Vector2(60f, 100f),
                new Vector2(520f, 34f), 0.8f, font, out volValue);

            Text brightCaption = NewText(settingsRt, "BrightnessLabel", "亮度", 28,
                new Color(1f, 1f, 1f, 0.75f), font);
            Anchor(brightCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, 10f), new Vector2(160f, 40f));
            brightCaption.alignment = TextAnchor.MiddleRight;
            brightCaption.raycastTarget = false;

            Text brightValue;
            Slider brightSlider = MakeSlider(settingsRt, "BrightnessSlider", new Vector2(60f, 10f),
                new Vector2(520f, 34f), 0.85f, font, out brightValue);

            Text closeLabel;
            Button closeBtn = MakeButton(settingsRt, "CloseButton", "关 闭",
                new Vector2(0f, -160f), new Vector2(300f, 66f), font, out closeLabel);

            // 亮度遮罩：全屏黑，alpha 由设置驱动；排在黑幕/菜单之前，不会把菜单也压暗
            RectTransform dimRt = NewRect(canvas.transform, "BrightnessOverlay");
            StretchFull(dimRt);
            Image dimImg = dimRt.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.1f);
            dimImg.raycastTarget = false;
            dimRt.SetSiblingIndex(introRt.GetSiblingIndex());

            SettingsPanel settings = settingsRt.gameObject.AddComponent<SettingsPanel>();
            settings.panel = settingsCg;
            settings.volumeSlider = volSlider;
            settings.volumeValue = volValue;
            settings.brightnessSlider = brightSlider;
            settings.brightnessValue = brightValue;
            settings.closeButton = closeBtn;
            settings.brightnessOverlay = dimImg;

            topBar.pauseMenu = pause;
            topBar.settings = settings;
            menu.settings = settings;
            menu.topBar = topBarRt.gameObject;

            // 准星常驻：创建顺序本来就在 Flash 之后、IntroBlack/MainMenu/PauseMenu 之前，
            // 所以面板一盖就看不见，不需要 SetActive，也不要再动 sibling 顺序。
        }

        static IntroSequence _pendingIntro;

        static Canvas BuildUI(Font font,
            out CanvasGroup deskHint, out CanvasGroup exploreHint, out Text exploreHintText,
            out Text subtitle, out PasswordLockUI lockUi, out ContractScratchUI contractUi,
            out CanvasGroup fade, out FinaleController finale, out CanvasGroup posterPanel,
            out RawImage posterShot, out Text posterCaption, out Button collectButton)
        {
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
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<RuntimeFont>();

            // --- 密码锁 ---
            RectTransform lockRoot = NewRect(canvasGo.transform, "PasswordLock");
            Anchor(lockRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(520f, 96f));
            lockUi = lockRoot.gameObject.AddComponent<PasswordLockUI>();
            lockUi.slots = new Text[4];
            lockUi.answer = "1024";

            for (int i = 0; i < 4; i++)
            {
                RectTransform cell = NewRect(lockRoot, "Slot" + i);
                cell.sizeDelta = new Vector2(88f, 88f);
                cell.anchoredPosition = new Vector2(-180f + i * 120f, 0f);

                Image bg = cell.gameObject.AddComponent<Image>();
                bg.color = new Color(1f, 1f, 1f, 0.11f);
                bg.raycastTarget = false;

                RectTransform line = NewRect(cell, "Line");
                Anchor(line, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(0f, 2f));
                line.sizeDelta = new Vector2(-20f, 2f);
                Image lineImg = line.gameObject.AddComponent<Image>();
                lineImg.color = new Color(1f, 1f, 1f, 0.28f);
                lineImg.raycastTarget = false;

                Text t = NewText(cell, "Digit", "—", 44, new Color(1f, 1f, 1f, 0.38f), font);
                StretchFull(t.rectTransform);
                t.raycastTarget = false;

                lockUi.slots[i] = t;
            }

            // --- 字幕 ---
            subtitle = NewText(canvasGo.transform, "Subtitle", "", 30, new Color(1f, 1f, 1f, 0.94f), font);
            Anchor(subtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -215f), new Vector2(1400f, 160f));
            subtitle.raycastTarget = false;

            // --- 提示 ---
            exploreHintText = NewText(canvasGo.transform, "Hint_Explore", "① 纸箱旁有块抹布：准星对准它，按 F 拾取", 26,
                new Color(1f, 1f, 1f, 0.5f), font);
            Anchor(exploreHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 190f), new Vector2(1200f, 60f));
            exploreHintText.raycastTarget = false;
            exploreHint = exploreHintText.gameObject.AddComponent<CanvasGroup>();

            Text deskHintText = NewText(canvasGo.transform, "Hint_Desk", "左右拖动，把影子转到最亮的地方", 26,
                new Color(1f, 1f, 1f, 0.5f), font);
            Anchor(deskHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 190f), new Vector2(1200f, 60f));
            deskHintText.raycastTarget = false;
            deskHint = deskHintText.gameObject.AddComponent<CanvasGroup>();
            deskHint.alpha = 0f;

            // --- 擦灰面板 ---
            RectTransform contractRoot = NewRect(canvasGo.transform, "ContractPanel");
            StretchFull(contractRoot);
            Image contractBg = contractRoot.gameObject.AddComponent<Image>();
            contractBg.color = new Color(0.02f, 0.02f, 0.03f, 0.9f);
            contractBg.raycastTarget = false;
            CanvasGroup contractCg = contractRoot.gameObject.AddComponent<CanvasGroup>();
            contractCg.alpha = 0f;

            Text cTitle = NewText(contractRoot, "Title", "退 租 合 同", 34, new Color(1f, 1f, 1f, 0.75f), font);
            Anchor(cTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 320f), new Vector2(800f, 60f));
            cTitle.raycastTarget = false;

            RectTransform dustRect = NewRect(contractRoot, "Dust");
            Anchor(dustRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(620f, 620f));
            RawImage dust = dustRect.gameObject.AddComponent<RawImage>();
            dust.color = Color.white;
            dust.raycastTarget = false;

            Text cHint = NewText(contractRoot, "Hint", "按住左键，擦掉上面的灰", 24, new Color(1f, 1f, 1f, 0.45f), font);
            Anchor(cHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 380f), new Vector2(1000f, 50f));
            cHint.raycastTarget = false;

            Text cRevealed = NewText(contractRoot, "Revealed", "", 26, new Color(0.95f, 0.9f, 0.8f, 0.9f), font);
            Anchor(cRevealed.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -370f), new Vector2(1200f, 60f));
            cRevealed.raycastTarget = false;

            contractUi = contractRoot.gameObject.AddComponent<ContractScratchUI>();
            contractUi.panel = contractCg;
            contractUi.dustLayer = dust;
            contractUi.revealedText = cRevealed;
            contractUi.hintText = cHint;

            // --- 黑幕 ---
            RectTransform fadeRoot = NewRect(canvasGo.transform, "Fade");
            StretchFull(fadeRoot);
            Image fadeImg = fadeRoot.gameObject.AddComponent<Image>();
            fadeImg.color = new Color(0f, 0f, 0f, 1f);
            fadeImg.raycastTarget = false;
            fade = fadeRoot.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;

            // --- 终局 ---
            RectTransform finRoot = NewRect(canvasGo.transform, "Finale");
            StretchFull(finRoot);
            CanvasGroup finCg = finRoot.gameObject.AddComponent<CanvasGroup>();
            finCg.alpha = 0f;

            Text noteText = NewText(finRoot, "Note", "", 40, new Color(0.93f, 0.90f, 0.84f, 1f), font);
            Anchor(noteText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(1300f, 260f));
            noteText.raycastTarget = false;
            noteText.lineSpacing = 1.5f;

            RectTransform btnRect = NewRect(finRoot, "CollectButton");
            Anchor(btnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -240f), new Vector2(300f, 76f));
            Image btnImg = btnRect.gameObject.AddComponent<Image>();
            btnImg.color = new Color(1f, 1f, 1f, 0.16f);
            collectButton = btnRect.gameObject.AddComponent<Button>();
            collectButton.targetGraphic = btnImg;
            CanvasGroup btnCg = btnRect.gameObject.AddComponent<CanvasGroup>();
            btnCg.alpha = 0f;
            ColorBlock cb = collectButton.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0.16f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.3f);
            cb.pressedColor = new Color(1f, 1f, 1f, 0.45f);
            collectButton.colors = cb;

            Text btnLabel = NewText(btnRect, "Label", "【 打包带走 】", 30, new Color(1f, 1f, 1f, 0.85f), font);
            StretchFull(btnLabel.rectTransform);
            btnLabel.raycastTarget = false;

            Text finTitle = NewText(finRoot, "Title", "第 1024 次 搬 家", 26, new Color(1f, 1f, 1f, 0.32f), font);
            Anchor(finTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -160f), new Vector2(900f, 50f));
            finTitle.raycastTarget = false;

            // --- 记忆凭证 ---
            RectTransform posterRoot = NewRect(canvasGo.transform, "Poster");
            StretchFull(posterRoot);
            Image posterBg = posterRoot.gameObject.AddComponent<Image>();
            posterBg.color = new Color(0.04f, 0.04f, 0.05f, 0.97f);
            posterBg.raycastTarget = false;
            posterPanel = posterRoot.gameObject.AddComponent<CanvasGroup>();
            posterPanel.alpha = 0f;

            RectTransform shotRect = NewRect(posterRoot, "Shot");
            Anchor(shotRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 120f), new Vector2(880f, 495f));
            posterShot = shotRect.gameObject.AddComponent<RawImage>();
            posterShot.raycastTarget = false;

            posterCaption = NewText(posterRoot, "Caption", "", 26, new Color(0.9f, 0.88f, 0.82f, 0.9f), font);
            Anchor(posterCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -270f), new Vector2(1200f, 200f));
            posterCaption.raycastTarget = false;
            posterCaption.lineSpacing = 1.4f;

            finale = finRoot.gameObject.AddComponent<FinaleController>();
            finale.notePanel = finCg;
            finale.noteText = noteText;
            finale.buttonGroup = btnCg;
            finale.collectButton = collectButton;
            finale.posterPanel = posterPanel;
            finale.posterShot = posterShot;
            finale.posterCaption = posterCaption;
            finale.noteContent = "哪怕在这个城市只有 1KB 的立足之地，\n我也要存下 1GB 的梦想。";

            return canvas;
        }

        // ================= 小工具 =================

        static Font LoadUiFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 32);
            return f;
        }

        static RectTransform NewRect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text NewText(Transform parent, string name, string content, int size, Color color, Font font)
        {
            RectTransform rt = NewRect(parent, name);
            Text t = rt.gameObject.AddComponent<Text>();
            t.text = content;
            t.fontSize = size;
            t.color = color;
            t.font = font;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>一件杂物：左键拾取、走到纸箱前左键扔进去（纯干扰项）。</summary>
        static void CreateClutter(string name, Vector3 position, Vector3 scale, Vector3 euler,
            PrimitiveType shape, Material mat, CrosshairUI cross, GameFlowDirector director,
            AudioDirector audio, GameObject carton)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;

            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;

            PromptSource prompt = go.AddComponent<PromptSource>();
            ClutterPickup clutter = go.AddComponent<ClutterPickup>();
            clutter.crosshair = cross;
            clutter.director = director;
            clutter.player = director.player != null ? director.player.transform : null;
            clutter.holdAnchor = Camera.main != null ? Camera.main.transform : null;
            clutter.storeTarget = carton != null ? carton.transform : null;

            // 也接受开局放下的那只搬家大纸箱
            GameObject carriedBox = GameObject.Find("Box_Carry");
            clutter.storeTarget2 = carriedBox != null ? carriedBox.transform : null;
            clutter.prompt = prompt;
            clutter.audio = audio;
        }

        /// <summary>半透明文字按钮（暂停菜单用）。</summary>
        static Button MakeButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
            Font font, out Text labelText)
        {
            RectTransform rt = NewRect(parent, name);
            Anchor(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.12f);

            Button btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = new Color(1f, 1f, 1f, 0.12f);
            cb.highlightedColor = new Color(1f, 1f, 1f, 0.26f);
            cb.pressedColor = new Color(1f, 1f, 1f, 0.36f);
            cb.selectedColor = new Color(1f, 1f, 1f, 0.12f);
            btn.colors = cb;

            labelText = NewText(rt, "Label", label, 30, new Color(1f, 1f, 1f, 0.92f), font);
            StretchFull(labelText.rectTransform);
            labelText.raycastTarget = false;
            return btn;
        }

        /// <summary>滑条（音量 / 亮度）：轨道 + 填充 + 圆形手柄 + 右侧百分比。</summary>
        static Slider MakeSlider(Transform parent, string name, Vector2 pos, Vector2 size, float value,
            Font font, out Text valueLabel)
        {
            RectTransform root = NewRect(parent, name);
            Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);

            RectTransform bg = NewRect(root, "Background");
            StretchFull(bg);
            bg.offsetMin = new Vector2(0f, 10f);
            bg.offsetMax = new Vector2(0f, -10f);
            Image bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.color = new Color(1f, 1f, 1f, 0.14f);

            RectTransform fillArea = NewRect(root, "Fill Area");
            StretchFull(fillArea);
            fillArea.offsetMin = new Vector2(0f, 10f);
            fillArea.offsetMax = new Vector2(0f, -10f);

            RectTransform fill = NewRect(fillArea, "Fill");
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Image fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = new Color(1f, 1f, 1f, 0.55f);

            RectTransform handleArea = NewRect(root, "Handle Slide Area");
            StretchFull(handleArea);

            RectTransform handle = NewRect(handleArea, "Handle");
            handle.sizeDelta = new Vector2(24f, 24f);
            Image handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.sprite = UiIcons.Dot;
            handleImg.color = new Color(1f, 1f, 1f, 0.95f);

            Slider s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill;
            s.handleRect = handle;
            s.targetGraphic = handleImg;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.value = value;
            s.transition = Selectable.Transition.None;

            valueLabel = NewText(root, "Value", Mathf.RoundToInt(value * 100f) + "%", 22,
                new Color(1f, 1f, 1f, 0.6f), font);
            Anchor(valueLabel.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(58f, 0f), new Vector2(96f, 30f));
            valueLabel.raycastTarget = false;

            return s;
        }

        static GameObject MakeBox(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject MakeChild(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject go = MakeBox(name, parent, localPos, scale, mat);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string leaf = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        static Material GetOrCreateMaterial(string name, Color color, float smoothness)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
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

        static Material GetOrCreateEmissiveMaterial(string name, Color color)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                existing.EnableKeyword("_EMISSION");
                existing.SetColor("_EmissionColor", color);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Standard");
            Material m = new Material(shader);
            m.color = color;
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material GetSpotMaterial(string name, Texture2D tex)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_MainTex", tex);
            m.color = new Color(1f, 0.92f, 0.72f, 0.10f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// 放在 Resources 里的纯白材质 —— Resources 目录的内容一定会进包，
        /// 这样剪影相机用的 Unlit/Color 才不会被构建裁掉。
        /// </summary>
        static void EnsureSilhouetteWhiteMaterial()
        {
            EnsureFolder("Assets/Resources");
            string path = "Assets/Resources/SilhouetteWhite.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material m = new Material(shader);
            m.color = Color.white;
            AssetDatabase.CreateAsset(m, path);
        }

        /// <summary>墙上投影板用的透明材质（Sprites/Default：无光照、可透明、支持 _MainTex）。</summary>
        static Material GetTransparentMaterial(string name, Color color)
        {
            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Transparent");
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            EditorUtility.SetDirty(m);
            return m;
        }

        static Texture2D GetRadialTexture(string name, int size)
        {
            EnsureFolder(TexDir);
            string path = TexDir + "/" + name + ".asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * a;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>
        /// 用源物体自己的网格生成一层"灰尘壳"：顶点先按 localScale 烘成实际尺寸，
        /// 再沿法线外扩 offset 米。这样灰尘完全贴合桌面几何（高低起伏都跟着），
        /// 不再是飘在桌上的一块 Quad。网格存成资产，场景保存后才不会丢引用。
        /// </summary>
        static GameObject BuildDustShell(GameObject source, string name, float offset, Material mat)
        {
            Mesh src = null;
            MeshFilter srcFilter = source.GetComponent<MeshFilter>();
            if (srcFilter != null) src = srcFilter.sharedMesh;

            Mesh mesh;
            if (src == null)
            {
                // 兜底：源物体没有网格时退化成一个 1 米立方体
                mesh = new Mesh();
                mesh.name = name + "_Mesh";
            }
            else
            {
                Vector3 scale = source.transform.localScale;
                Vector3[] verts = src.vertices;
                Vector3[] normals = src.normals;
                if (normals == null || normals.Length != verts.Length)
                {
                    src.RecalculateNormals();
                    normals = src.normals;
                }

                for (int i = 0; i < verts.Length; i++)
                    verts[i] = Vector3.Scale(verts[i], scale) + normals[i] * offset;

                EnsureFolder("Assets/Meshes");
                string path = "Assets/Meshes/" + name + "_Mesh.asset";
                mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    mesh = new Mesh();
                    mesh.name = name + "_Mesh";
                    AssetDatabase.CreateAsset(mesh, path);
                }

                mesh.Clear();
                mesh.vertices = verts;
                mesh.normals = normals;
                mesh.uv = src.uv;
                mesh.uv2 = src.uv2;
                mesh.triangles = src.triangles;
                mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh);
                AssetDatabase.SaveAssets();
            }

            GameObject go = new GameObject(name);
            go.transform.position = source.transform.position;
            go.transform.rotation = source.transform.rotation;
            go.transform.localScale = Vector3.one;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            MeshCollider mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

            return go;
        }

        /// <summary>暂停菜单的毛玻璃材质：Move1024/UIBlur（GrabPass 模糊 + 半透明深色）。</summary>
        static Material GetBlurMaterial(string name)
        {
            Shader shader = Shader.Find("Move1024/UIBlur");
            if (shader == null)
            {
                Debug.LogWarning("[1024] 找不到 Move1024/UIBlur 着色器，暂停菜单退回纯色遮罩");
                return null;
            }

            EnsureFolder(MatDir);
            string path = MatDir + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != shader)
            {
                m.shader = shader;
            }

            m.SetColor("_Color", new Color(0.02f, 0.02f, 0.035f, 0.72f));
            m.SetFloat("_BlurSize", 3.0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>桌面上那层灰的贴图：灰底 + 噪点 + 横向纤维。运行时 DustErase 会复制一份再擦。</summary>
        static Texture2D GetDustTexture(string name)
        {
            EnsureFolder(TexDir);
            string path = TexDir + "/" + name + ".asset";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            const int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] px = new Color32[size * size];
            System.Random rng = new System.Random(20260101);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float n = (float)rng.NextDouble();
                    float fiber = Mathf.PerlinNoise(x * 0.05f, y * 0.55f);
                    float v = 0.60f + n * 0.14f + fiber * 0.16f;
                    byte c = (byte)Mathf.Clamp(v * 255f, 0f, 255f);
                    px[y * size + x] = new Color32((byte)(c * 0.97f), (byte)(c * 0.94f), (byte)(c * 0.89f), 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>灰尘材质：Sprites/Default（无光照、可透明、Cull Off），挂上灰尘贴图。</summary>
        static Material GetDustMaterial(string name, Texture2D tex)
        {
            // 不用纯白：Sprites/Default 不吃光照；纯白像一块发亮的板子，太暗又和木桌面混在一起
            Material m = GetTransparentMaterial(name, new Color(0.76f, 0.77f, 0.80f, 1f));
            m.mainTexture = tex;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}