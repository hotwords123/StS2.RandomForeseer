# Random Foreseer

语言 / Languages：中文 | [English](README.en.md)

《杀戮尖塔 2》的随机数预测模组。它会在不推进真实随机数的前提下，提前显示部分随机结果，而不必进行保存和读档。

更新日志：[CHANGELOG.md](CHANGELOG.md)

Steam 创意工坊：[随机数预测](https://steamcommunity.com/sharedfiles/filedetails/?id=3747531952)

创意工坊包会根据当前游戏版本自动加载最新兼容的 Mod 版本；GitHub Release 提供对应版本的单一标准包。

## 功能

### 局外预测

- **预测牌组变化结果**：在变牌选择网格悬停提示和确认预览中显示当前随机数状态将生成的确切卡牌。
- **预测下一阶段先古之民和 Boss**：前两阶段的 Boss 奖励界面会在状态栏显示下一阶段起点的先古之民和终点 Boss。
- **预测事件选项结果**：悬停非先古之民事件选项时，显示即时随机奖励、随机升级/降级和随机后续选项。
- **水晶球透视**：在水晶球事件小游戏中透过未揭开的迷雾显示物品位置和类型。
- **预测遗物拾起效果**：遗物提示（包括先古之民选项）会显示拾起时立即发生的随机卡牌、遗物、药水、诅咒和变牌结果。
- **预测送货员补货**：拥有送货员时，悬停商店中可以购买的卡牌、药水或遗物会显示购买后补货的商品。
- **预测浮木重掷奖励**：悬停卡牌奖励的“重掷”按钮时，显示重掷后将出现的卡牌。
- **预测佩尔之翼献祭奖励**：悬停触发奖励的“献祭”按钮时，显示佩尔之翼将给予的遗物。
- **预测休息处结果**：悬停休息处选项时，显示捕梦网、小邮箱、铲子等遗物将产生的随机结果。

### 局内预测

- **预测战斗随机变牌**：战斗中“熵”选择手牌变牌时，显示即将变化得到的卡牌。
- **预测回合结束效果**：战斗中显示当前所有结束回合玩家的支持伤害效果总计；预测图标和血条预测可分别设置为悬停结束回合按钮时显示，或玩家回合内始终显示；悬停目标生物或预测图标时会显示逐条伤害来源详情。
- **冻结之眼**：战斗中查看抽牌堆时，按实际抽牌顺序显示卡牌，并在玩家回合预览弃牌堆洗入后的顺序。
- **预测战斗随机印牌**：战斗中悬停手牌里的随机印牌卡时，显示即将生成的卡牌。
- **预测卡牌抽牌**：战斗中悬停已支持的抽牌卡牌时，显示将抽到的牌；抽牌堆不足时会预览洗牌后的后续结果。
- **预测战斗随机选牌**：战斗中悬停手牌里的随机选牌卡或卡牌目标时，显示或高亮当前随机数状态将选中的牌；可能受副作用影响的预测会显示可关闭的警告。
- **预测充能球效果**：战斗中悬停支持的充能球触发卡牌、药水或相应目标时，显示充能球将命中的目标，并在血条上显示预测。
- **预测随机印牌药水**：在随机印牌药水的提示中显示即将出现的卡牌。
- **预测药水抽牌**：战斗中悬停支持的抽牌药水时，显示将抽到的牌；抽牌堆不足时会预览洗牌后的后续结果。
- **预测随机生成药水**：在混沌药水和炼制药水的提示中显示即将获得的药水。
- **预测抽牌堆自动出牌**：战斗中悬停破灭、倾泻和精炼混沌时，显示将从抽牌堆打出的牌。
- **预测随机目标攻击**：战斗中悬停支持的随机敌人目标攻击牌时，显示当前随机数状态将命中的目标，并在血条上显示预测。

这些功能都可以在模组设置页中单独开关，也可以通过单人/多人模式总开关整体关闭。公平模式默认开启，只预测可以通过保存和读档获取的信息。

## 模组联动

### lemonSpire2

安装 lemonSpire2 时，队友面板会复用 Random Foreseer 的现有预测：

- 队友手牌中的战斗随机印牌、随机选牌、抽牌堆自动出牌、卡牌抽牌和随机生成药水预测
- 队友先古遗物奖励的拾起效果预测
- 队友商店遗物和商店药水的即时随机结果预测

## 安装

1. 安装并启用 `STS2-RitsuLib`。
2. 将本模组发布包中的 `RandomForeseer` 目录放入游戏的 `mods` 目录。
3. 启动游戏，在模组列表中确认 `RandomForeseer` 已加载。

当前 manifest 目标：

| 项 | 值 |
|---|---|
| 当前版本 | `0.13.5` |
| 最低游戏版本 | `0.111.0` |
| RitsuLib 依赖 | `0.5.12` |

## 从源码构建

首次构建前复制本机路径配置：

```powershell
Copy-Item .\local.props.template .\local.props
```

在 `local.props` 中配置：

| 字段 | 说明 |
|---|---|
| `Sts2Dir` | Slay the Spire 2 安装目录 |
| `Sts2DataDir` | 游戏 dll 目录，通常是 `$(Sts2Dir)/data_sts2_windows_x86_64` |
| `GodotExe` | 用于导出 pck 的 MegaDot/Godot 可执行文件 |
| `RitsuLibDeployDir` | RitsuLib 的本机部署目录 |

常用构建命令：

```powershell
dotnet build .\RandomForeseer.csproj
```

只验证 C# 编译、不复制到游戏目录、不导出 PCK：

```powershell
dotnet build .\RandomForeseer.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

完整构建会将 dll、manifest 和 pck 部署到 `$(Sts2Dir)/mods/RandomForeseer`。

## 项目结构

```text
RandomForeseer.csproj - C# 项目与构建配置
RandomForeseer.json - Mod manifest
RandomForeseer/localization/ - 模组设置和界面本地化资源
RandomForeseerCode/ - C# 源码
RandomForeseerCode/Common/ - 通用预测 HoverTip、RNG 和本地化工具
RandomForeseerCode/Data/ - 设置数据、持久化和迁移
RandomForeseerCode/Debug/ - 调试入口和测试用奖励界面
RandomForeseerCode/Entry.cs - 模组入口与 Harmony patch 注册
RandomForeseerCode/InCombat/ - 战斗内卡牌、药水和冻结之眼预测
RandomForeseerCode/Integrations/ - 与其它模组的可选联动补丁
RandomForeseerCode/Localization/ - 模组本地化注册和文本访问
RandomForeseerCode/OutOfCombat/ - 战斗外事件、奖励、商店、休息处和变牌预测
RandomForeseerCode/OutOfCombat/Events/ - 非先古之民事件选项预测
RandomForeseerCode/Settings/ - 设置页面注册和 UI bindings
project.godot - PCK 导出用 Godot 项目
scripts/ - 本地开发、维护和发版脚本
workshop/loader/ - Steam 创意工坊多版本包加载器
```
