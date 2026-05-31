# RandomForeseer

语言 / Languages：中文 | [English](README.en.md)

一个可复制、可构建的 RitsuLib Mod 模板，提供通用的 Godot/C# 工程结构、示例内容和静态占位资源。

**模板包含：**

- 一个 `[ModInitializer]` 入口，外加最小自定义角色（含角色卡池、遗物池、药水池）。
- 4 张初始打击、4 张初始防御和 1 个初始遗物示例。
- 最小 Godot 静态占位场景：战斗模型、能量表盘、角色选择背景、商店和火堆。
- 从原版资源复制并按模板命名的占位 PNG，复制模板后可直接替换。
- 中英文基础本地化文件。
- 完整的 Godot 项目、导出配置、Mod manifest 和 MSBuild 构建脚本。

## 学习资源

- [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)：Slay the Spire 2 Mod 的共享框架库，本模板基于它提供内容注册、角色脚手架和 Godot 资源接入能力。
- [RitsuLib 文档地址](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials/tree/master/RitsuLib)：按文件阅读教程和示例。
- [Slay the Spire 2 Modding Tutorials 网页版](https://glitchedreme.github.io/SlayTheSpire2ModdingTutorials/index.html)：完整教程站点。
- 模板 Wiki（以 Rider 为主线）：[中文首页](https://github.com/alkaid616/RandomForeseer/wiki/Home) | [English Home](https://github.com/alkaid616/RandomForeseer/wiki/Home-EN)。

## 安装与使用

这个项目可以通过两种方式获得：使用 NuGet 模板自动生成，或者手动复制目录。

### 方式 A：使用 NuGet 模板（推荐）

```powershell
# 安装模板
dotnet new install STS2.RitsuLib.ModTemplate

# 创建新 Mod
dotnet new ritsulibmod -n MyMod

# 卸载模板
dotnet new uninstall STS2.RitsuLib.ModTemplate
```

`dotnet new ritsulibmod -n MyMod` 会生成名为 `MyMod` 的工程，并把模板中的 `RandomForeseer`、示例类名、资源文件名、资源目录、manifest 名称、namespace 和本地化 id 同步替换成新名称。

### 方式 B：手动复制模板

1. 复制整个目录并改名为你的 Mod 名称。
2. 修改 `RandomForeseer.json` 里的 `id`、`name`、`author`、`description`。
3. 修改 `RandomForeseerCode/Entry.cs` 里的 `ModId`。
4. 如需彻底改名，同时修改 `.csproj`、`.sln`、`project.godot` 的项目名和命名空间。
5. 把资源目录 `RandomForeseer/` 改成你的 `ModId`，并同步更新代码中的 `Entry.ResPath` 相关路径。

## 配置本机路径

```powershell
Copy-Item .\local.props.template .\local.props
```

在 `local.props` 中设置以下值（文件已在 `.gitignore`，不要提交）：

| 字段 | 说明 |
|---|---|
| `Sts2Dir` | Slay the Spire 2 安装目录 |
| `Sts2DataDir` | 游戏 dll 目录，通常是 `$(Sts2Dir)/data_sts2_windows_x86_64` |
| `GodotExe` | 用于导出 pck 的 MegaDot/Godot 可执行文件 |
| `RitsuLibDeployDir` | RitsuLib 本机部署目录，默认 `$(Sts2Dir)/mods/STS2-RitsuLib`。这是 RitsuLib 包/构建逻辑把 RitsuLib 复制到游戏 mods 目录的位置，**不是当前 Mod 自身的输出目录** |

## RitsuLib 版本兼容性

> ⚠️ **重要：发布前请校对 manifest 与 csproj 版本对齐**
>
> `RandomForeseer.json` 中 `dependencies[STS2-RitsuLib].version` **必须**与 `.csproj` 里 `STS2.RitsuLib` 包实际编译使用的版本一致。模板构建时会自动同步该依赖版本；`min_game_version` 和有意声明较低运行时下限的场景仍需人工确认。详细步骤见下方 [发布前 checklist：版本对齐](#发布前-checklist版本对齐)。

### 当前版本快照（截至 2026-05-22）

| 项 | 值 |
|---|---|
| STS2 游戏当前版本 | `0.106.0` |
| RitsuLib 当前版本 | `0.3.0` |
| 模板 manifest 状态 | `min_game_version` 与 `dependencies[STS2-RitsuLib].version` 已对齐 |

### 版本对应表

下表汇总主要边界版本对应的 STS2 主要目标版本，整理自 [STS2-RitsuLib Releases](https://github.com/BAKAOLC/STS2-RitsuLib/releases) 的公告。未在表中显式列出的小版本沿用所在区间；遇到边界版本时以对应 release notes 为准。

| RitsuLib 版本 | 主要目标 STS2 版本 | 兼容（Compat）包 |
|---|---|---|
| `v0.3.0+`（2026-05-22 起） | `0.106.0` | `0.103.2`；删除了 `0.104.0` 兼容支持 |
| `v0.2.29` ~ `v0.2.40` | `0.105.1` | `0.104.0`、`0.103.2` |
| `v0.2.27` ~ `v0.2.28` | `0.105.0` | `0.104.0`、`0.103.2` |
| `v0.2.0` ~ `v0.2.26` | `0.104.0` | 自 `v0.2.6` 起实验性提供 `0.103.2`；同步移除 `0.99.1` 兼容 |
| `v0.0.x` / `v0.1.x` | `0.99.1` 及更早 | — |

### 包选择：主线与兼容包

模板默认引用主线 `STS2.RitsuLib`，跟踪 NuGet 最新版本：

```xml
<PackageReference Include="STS2.RitsuLib" Version="*" GeneratePathProperty="true" />
```

**三个 RitsuLib 包一次只能启用一个。** 仍针对老分支的代码，注释主线包并启用对应兼容包：

```xml
<!-- STS2 0.104.0 兼容分支（v0.3.0 起已停止维护） -->
<PackageReference Include="STS2.RitsuLib.Compat.0.104.0" Version="*" />

<!-- STS2 0.103.2 兼容分支 -->
<PackageReference Include="STS2.RitsuLib.Compat.0.103.2" Version="*" />
```

兼容包只是选择对应游戏分支，并不会恢复所有旧 API；部分老 Mod 仍然需要修改并重新编译。

项目还引用 `Nothing.STS2RitsuLib.ModAnalyzers` —— 一个 AI 编写的辅助分析器，开发期会提示 RitsuLib Mod 模板中常见的 manifest 和资源配置问题。

### 发布前 checklist：版本对齐

> **`.csproj` 里的 `PackageReference` 只控制编译时拉取；`RandomForeseer.json` 的 `dependencies` 是游戏加载器在运行时校验的。模板会在构建时把 `STS2-RitsuLib` 依赖版本同步为实际解析到的 NuGet 版本，但 `min_game_version` 仍需人工确认。**

如果发布时 manifest 没有同步到编译使用的新版 RitsuLib，玩家装了旧版 RitsuLib 仍能通过 manifest 校验、运行时却会因 API 缺失或签名变化崩掉；反过来 manifest 写得过新，会让本来能跑的玩家被错误拒绝。

每次发布前请：

1. 构建后确认 `RandomForeseer.json` 的 `dependencies[STS2-RitsuLib].version` 已同步为实际解析到的 `STS2.RitsuLib` 版本。
2. 切换到兼容包（`Compat.0.104.0` / `Compat.0.103.2`）时，把 `min_game_version` 同步调到对应分支；`dependencies[].id` 保持 `STS2-RitsuLib`（兼容包对外暴露的 mod id 不变）。
3. 如果 manifest 版本是作为"运行时下限"而不是编译版本（例如声明 `0.3.0+` 都可用），在发布说明里明确，并自己测过下限能跑通。

### 升级注意事项

#### 升级到 RitsuLib `v0.3.0` / STS2 `0.106.0`

主要变化（来自 [v0.3.0 release notes](https://github.com/BAKAOLC/STS2-RitsuLib/releases/tag/v0.3.0)）：

- **破坏性变更**：移除 `RunSidecar` 相关设计，完全被 `RunSavedData` 取代。
- 新增 `TargetType` 注册能力，支持自定义 `TargetType`。
- 加强 Loader 的加载目标检测：分支版本文件使用哈希校验，未匹配的版本被丢弃。
- 移除 `0.104.0` 兼容支持。

#### 升级到 RitsuLib `v0.2.27` / STS2 `0.105.0`（历史）

仍从更早分支（`v0.2.0` ~ `v0.2.26` / STS2 `0.104.0`）迁移时请检查：

- 版本条件编译改为累积区间宏 `STS2_AT_LEAST_<ver>`；旧的 `STS2_V_<ver>` 不再推荐。
- AnyPlayer / AnyAny 目标逻辑调整；旧卡牌目标、基础构造函数签名和注册逻辑要按新 API 检查。
- 卡牌右下角支持额外图标数量标签，并处理与原版 UI 的冲突；自定义 UI 或图标补丁需确认显示层级和位置。
- 保留/flush 相关 hook 和 event 有替换、移除或 `[Obsolete]` 标记；旧代码使用 `CardRetainedEvent`、`CardsFlushedEvent` 或旧 `Hook.*` 入口需迁移。
- `Badge`、`BadgeRuntimeTemplate`、`BadgePool.CreateAll` 和 `ModBadgeTemplate` 构造签名调整；旧代码可能需更新以避免 `MissingMethodException`。

## 构建

| 命令 | 行为 |
|---|---|
| `dotnet build .\RandomForeseer.csproj` | 完整构建：编译 + `CopyMod` + `ExportPCK` |
| `... /p:RunPckExport=false` | 跳过 PCK 导出（不需要 `GodotExe`） |
| `... /p:CopyModOnBuild=false` | 跳过复制到游戏 mods 目录（产物只留在 `bin/`） |
| `... /p:RunPckExport=false /p:CopyModOnBuild=false` | 仅验证 C# 编译 |

完整构建会在 `Build` 之后运行两个 MSBuild target：

- **`CopyMod`**：复制 dll 和 manifest 到游戏的 `mods/RandomForeseer` 目录。
- **`ExportPCK`**：调用 `GodotExe` 导出 pck 到同一个 Mod 目录。

> `RitsuLibDeployDir` 只控制 RitsuLib 框架自身的部署位置；当前 Mod 的 dll、manifest 和 pck 由 `ModOutputDir` 控制（默认 `$(Sts2Dir)/mods/$(MSBuildProjectName)`）。

## 目录结构

```text
RandomForeseer/
├── RandomForeseerCode/   # C# 源码
├── RandomForeseer/       # Godot 资源、本地化和占位场景
├── RandomForeseer.csproj
├── RandomForeseer.json   # Mod manifest
├── project.godot
└── local.props.template
```

`res://RandomForeseer/...` 是 Godot/PCK 内的资源路径，对应仓库里的 `RandomForeseer/` 资源目录，**不是 C# namespace**。通过 NuGet 模板创建项目时，这些目录名、文件名和 namespace 会按新 Mod 名同步替换。

## 模板内容

### 示例角色

| 项 | 值 |
|---|---|
| 类型 | `RandomForeseerCharacter` |
| 预期 id | `RANDOM_FORESEER_CHARACTER_RANDOM_FORESEER_CHARACTER` |
| starter 牌组 | 4 × `RandomForeseerStrike`、4 × `RandomForeseerDefend`、1 × `RandomForeseerRelic` |
| 资源配置 | `CharacterAssetProfile`；模板只指定静态占位资源，未指定的音频/拖尾/转场等字段从 `PlaceholderCharacterId` 回退 |

### 示例卡牌与遗物

| 类型 | 池 | 预期 id |
|---|---|---|
| `RandomForeseerStrike`（攻击） | 角色卡池 | `RANDOM_FORESEER_CARD_RANDOM_FORESEER_STRIKE` |
| `RandomForeseerDefend`（技能） | 角色卡池 | `RANDOM_FORESEER_CARD_RANDOM_FORESEER_DEFEND` |
| `RandomForeseerRelic` | `RandomForeseerRelicPool` | `RANDOM_FORESEER_RELIC_RANDOM_FORESEER_RELIC` |

### 静态占位资源

**图片**（`res://RandomForeseer/images/...`）：

- `cards/RandomForeseerStrike.png`、`cards/RandomForeseerDefend.png`：示例卡图。
- `relics/RandomForeseerRelic.png`：示例遗物图标。
- `characters/RandomForeseer_character_*.png`：角色头像、角色选择图、地图标记和能量图标。

**场景**（`res://RandomForeseer/scenes/characters/...`）：

| 场景文件 | 用途 | 占位结构 |
|---|---|---|
| `RandomForeseer_character.tscn` | 战斗人物 | `%Visuals`、`%Bounds`、`%IntentPos`、`%CenterPos`、`%TalkPos` |
| `RandomForeseer_energy_counter.tscn` | 能量表盘 | `%EnergyVfxBack`、`%Layers`、`%RotationLayers`、`%EnergyVfxFront`、`Label` |
| `RandomForeseer_merchant.tscn` | 商店人物 | — |
| `RandomForeseer_rest_site.tscn` | 火堆人物 | `%ControlRoot`、`%SelectionReticle`、`%Hitbox`、`%ThoughtBubbleRight`、`%ThoughtBubbleLeft` |
| `RandomForeseer_character_select_bg.tscn` | 角色选择背景 | — |

这些资源只用于保证模板可见、可替换，不追求原版动画效果。复制模板后替换为自己的素材即可；如果改了路径，同步更新对应 `AssetProfile`。

## Manifest 格式

`RandomForeseer.json` 是 Mod 的清单文件，游戏加载器在启动时读取它来识别 Mod、检查依赖、决定是否加载。完整示例：

```json
{
  "id": "RandomForeseer",
  "name": "RandomForeseer",
  "pck_name": "RandomForeseer",
  "author": "Author",
  "description": "A starter Slay the Spire 2 mod template built on RitsuLib.",
  "version": "0.0.0",
  "has_pck": true,
  "has_dll": true,
  "affects_gameplay": true,
  "min_game_version": "0.106.0",
  "dependencies": [
    { "id": "STS2-RitsuLib", "version": "0.3.0" }
  ]
}
```

### 字段说明

| 字段 | 类型 | 说明 |
|---|---|---|
| `id` | string | Mod 唯一标识。**必须与 `Entry.ModId` 完全一致**，也建议与 `mods/<id>` 目录名一致。游戏内依赖、本地化前缀和资源路径都依赖这个值 |
| `name` | string | Mod 列表中的显示名，可包含空格和中文 |
| `pck_name` | string | `.pck` 文件名（不含扩展名）。**必须与 `.csproj` 实际导出的 PCK 文件名一致**，否则即使 `has_pck=true` 也加载不到资源 |
| `author` | string | 作者名，显示用 |
| `description` | string | Mod 简介，显示在 Mod 列表 |
| `version` | string | 此 Mod 自身的版本号，建议 SemVer（`主.次.修`），每次发布前更新 |
| `has_pck` | bool | 是否分发 `.pck`。纯代码 Mod 可置 `false` 并跳过 `ExportPCK` |
| `has_dll` | bool | 是否分发 `.dll`。纯资源 Mod 可置 `false` |
| `affects_gameplay` | bool | 是否影响游戏玩法。开启后游戏会在存档/成就等处做相应标记；仅纯视觉/本地化可设 `false` |
| `min_game_version` | string | 兼容的最低 STS2 版本，低于该版本拒绝加载。**应与 `.csproj` 选用的 RitsuLib 包面向的游戏分支匹配**（见上文 [RitsuLib 版本兼容性](#ritsulib-版本兼容性)） |
| `dependencies` | array | 依赖列表。每项使用 `id` + `version`。**旧版单对象 `min_version` 写法已不支持** |
| `dependencies[].id` | string | 被依赖 Mod 的 `id`。RitsuLib 框架的 id 是 `STS2-RitsuLib` |
| `dependencies[].version` | string | 被依赖 Mod 的最低版本。**`STS2-RitsuLib` 的版本必须与 `.csproj` 编译时的 NuGet 版本严格一致**，详见上文 [发布前 checklist：版本对齐](#发布前-checklist版本对齐) |

## 开发提示

- 新内容优先写 `AssetProfile`；个别历史兼容字段才考虑覆写 `Custom...Path`。
- 角色资源字段没写时，RitsuLib 会从 `PlaceholderCharacterId` 对应的原版角色配置补齐。
- 资源路径要以 `res://` 开头，并确认 PCK 内目录名和大小写正确。
- `.tscn` 场景需要确认已打包进 Mod 资源；需绑定脚本时，写本地包装类并在 `Entry.Initialize()` 调用 `EnsureGodotScriptsRegistered(...)`。
