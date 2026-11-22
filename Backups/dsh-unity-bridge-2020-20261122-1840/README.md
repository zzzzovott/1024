# DSH Unity Bridge — Unity 2020.3 兼容备份

- 备份时间:2026-11-22 18:40
- 上游插件:`dsh-unity-2022` v0.1.0
  (`C:\Users\chenw\.dsh\profiles\desktop\node_modules\dsh-unity-2022\unity-package`)
- 落地位置:`C:\Users\chenw\1024\Packages\com.dsh.unity-bridge`

## 内容

| 目录 | 说明 |
|---|---|
| `package/` | 已适配 Unity 2020 的桥接包,即 `Packages/com.dsh.unity-bridge` 的完整副本(含 `.meta`) |
| `config/` | 两个 profile 的 `cordis.patch.yml`(含 `unityPath` 配置) |

## 相对上游 0.1.0 的改动

1. `package.json`:`"unity": "2022.3"` + `"unityRelease": "62f3"` → `"unity": "2020.3"`。
   否则 Unity 2020.3.49 判定不兼容,包不会被加载。
2. `Editor/BridgeEval.cs`:编译器解析分层。2022 用 `DotNetSdkRoslyn/csc.dll` +
   `NetCoreRuntime/dotnet.exe`;2020 用 `Tools/Roslyn/csc.exe`(自包含运行时);再退
   `MonoBleedingEdge/bin/mono` + `csc.dll`。
3. `Editor/BridgeEval.cs`:`-langversion:9` 改为按编辑器版本取值(2022+ 用 `9`,2020 用 `8.0`,
   更早用 `7.3`)。Unity 2020 自带 Roslyn 3.5 不接受 `9`,会直接报 CS1617,导致 `eval` 全废。
4. `Editor/BridgeEval.cs`:BCL 引用在缺少 `UnityReferenceAssemblies/unity-4.8-api` 时,回退到
   `MonoBleedingEdge/lib/mono/4.7.1-api` + 其 `Facades`。
5. `Editor/BridgeEval.cs`:写死的 `UNITY_2022_3_OR_NEWER` / `UNITY_2022_3_62` 宏改为按
   `Application.unityVersion` 生成 `*_OR_NEWER` 链 + 精确版本宏。
6. `Editor/BridgeCommands.cs`:`status` 的 `isRequiredUnityVersion` 由精确比较 `"2022.3.62f3"`
   改为 `>= 2020.3`,并新增 `minimumUnityVersion` 字段。
7. `Editor/BridgeDiscovery.cs`:新增 `UnityVersion` 解析与 `IsAtLeast`,供上面两处共用。
8. `Editor/BridgeServer.cs`:新增 `_secondaryProcess` 守卫(`Application.isBatchMode`)。
   Unity 2020 派生的 `AssetImportWorker` 子进程同样会执行 `[InitializeOnLoadMethod]`,
   会启动自己的桥并用“主线程不 tick”的端口覆盖发现文件,导致 `editor_blocked`。
9. `Editor/Json.cs`、`Editor/BridgeEval.cs`:注释更新为同时说明 2020.3 / 2022.3。

## 配置

在两个 profile 的 patch 文件里追加(已包含在 `config/`):

```yaml
- id: unity-2022
  config:
    unityPath: D:/Unity/2020.3.49f1c1/Editor/Unity.exe
```

- `%USERPROFILE%\.dsh\profiles\desktop\cordis.patch.yml`(当前活动 profile)
- `%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml`

## 恢复方法

1. 包:把 `package/` 整个复制回 `<项目>\Packages\com.dsh.unity-bridge`(覆盖)。
2. 配置:`config/desktop-cordis.patch.yml` → `%USERPROFILE%\.dsh\profiles\desktop\cordis.patch.yml`;
   `config/web-cordis.patch.yml` → `%USERPROFILE%\.dsh\profiles\web\cordis.patch.yml`。
3. 配置会热加载;必要时重启 DSH。

## 注意

- **不要改 `package.json` 的 `version`,保持 `0.1.0`。** 宿主 `unity_open` 的
  `installBridgePackage` 在版本不一致、或内置包文件更新时,会用内置的 **2022 版**整目录覆盖
  `Packages/com.dsh.unity-bridge`。保持同版本且本地文件更新时,它会报 `up-to-date` 并跳过。
- 若发现包被覆盖,直接用本备份的 `package/` 覆盖回去即可。
- 验证环境:Unity `2020.3.49f1c1`。整包用该编辑器自带 Roslyn 编译 0 error / 0 warning;
  并通过 `unity_status`、`create_object`、`find_objects`、`screenshot` 实测。
