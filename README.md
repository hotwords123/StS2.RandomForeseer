# Random Foreseer

语言 / Languages：中文 | [English](README.en.md)

《杀戮尖塔 2》的随机数预测模组。它会在不推进真实随机数的前提下，提前显示部分随机结果，而不必进行保存和读档。

更新日志：[CHANGELOG.md](CHANGELOG.md)

Steam 创意工坊：[随机数预测](https://steamcommunity.com/sharedfiles/filedetails/?id=3747531952)

创意工坊包会根据当前游戏版本自动加载最新兼容的 Mod 版本；GitHub Release 提供对应版本的单一标准包。

## 功能

- **预测牌组变化结果**：在变牌选择网格悬停提示和确认预览中显示当前随机数状态将生成的确切卡牌。
- **预测随机印牌药水**：在随机印牌药水的提示中显示即将出现的卡牌。
- **预测随机生成药水**：在混沌药水和炼制药水的提示中显示即将获得的药水。
- **预测战斗随机印牌**：战斗中悬停手牌里的随机印牌卡时，显示即将生成的卡牌。
- **预测战斗随机选牌**：战斗中悬停手牌里的随机选牌卡或卡牌目标时，显示或高亮当前随机数状态将选中的牌；可能受副作用影响的预测会显示可关闭的警告。
- **预测随机目标攻击**：战斗中悬停支持的随机敌人目标攻击牌时，显示当前随机数状态将命中的目标，并在血条上显示预测。
- **预测充能球效果**：战斗中悬停支持的充能球触发卡牌或卡牌目标时，显示充能球将命中的目标，并在血条上显示预测。
- **预测回合结束效果**：战斗中显示当前所有结束回合玩家的支持伤害效果总计；预测图标和血条预测可分别设置为悬停结束回合按钮时显示，或玩家回合内始终显示；悬停目标生物或预测图标时会显示逐条伤害来源详情。
- **预测抽牌堆自动出牌**：战斗中悬停破灭、倾泻和精炼混沌时，显示将从抽牌堆打出的牌。
- **预测药水抽牌**：战斗中悬停支持的抽牌药水时，显示将抽到的牌；抽牌堆不足时会预览洗牌后的后续结果。
- **预测卡牌抽牌**：战斗中悬停重启和计算下注时，显示将抽到的牌；抽牌堆不足时会预览洗牌后的后续结果。
- **预测战斗随机变牌**：战斗中“熵”选择手牌变牌时，显示即将变化得到的卡牌。
- **预测浮木重掷奖励**：悬停卡牌奖励的“重掷”按钮时，显示重掷后将出现的卡牌。
- **预测佩尔之翼献祭奖励**：悬停触发奖励的“献祭”按钮时，显示佩尔之翼将给予的遗物。
- **预测遗物拾起效果**：遗物提示（包括先古之民选项）会显示拾起时立即发生的随机卡牌、遗物、药水、诅咒和变牌结果。
- **预测休息处结果**：悬停休息处选项时，显示捕梦网、小邮箱、铲子等遗物将产生的随机结果。
- **预测事件选项结果**：悬停非先古之民事件选项时，显示即时随机奖励、随机升级/降级和随机后续选项。
- **水晶球透视**：在水晶球事件小游戏中透过未揭开的迷雾显示物品位置和类型。
- **预测下一阶段先古之民和 Boss**：前两阶段的 Boss 奖励界面会在状态栏显示下一阶段起点的先古之民和终点 Boss。
- **冻结之眼**：战斗中查看抽牌堆时，按实际抽牌顺序显示卡牌，并在玩家回合预览弃牌堆洗入后的顺序。

这些功能都可以在模组设置页中单独开关，也可以通过单人/多人模式总开关整体关闭。公平模式默认开启，只预测可以通过保存和读档获取的信息。

## 当前支持的预测

### 牌组变化

- 星盘（Astrolabe）
- 新叶（New Leaf）
- 混沌芳香（Aroma of Chaos）
- 无尽传送带（Endless Conveyor）
- 变形灵林谷（Morphic Grove）
- 共生体（Symbiote）
- 审判（The Trial）
- 低语空谷（Whispering Hollow）

### 随机印牌药水

- 攻击药水（Attack Potion）
- 技能药水（Skill Potion）
- 能力药水（Power Potion）
- 无色药水（Colorless Potion）
- 宇宙药剂（Cosmic Concoction）
- 欧洛巴斯之酸（Orobic Acid）

### 随机生成药水

- 混沌药水（Entropic Brew，战斗内外，包括商店里）
- 炼制药水（Alchemize）

### 战斗随机印牌

- 富足（Abundance）
- 新生之喜（Bundle of Joy）
- 发现（Discovery）
- 声东击西（Distraction）
- 地狱之刃（Infernal Blade）
- 花样百出（Jack of All Trades）
- 大奖（Jackpot）
- 慷慨捐助（Largesse）
- 君权自授（Manifest Authority）
- 羽化（Metamorphosis）
- 类星体（Quasar）
- 飞溅（Splash）
- 添柴（Stoke）
- 白噪声（White Noise）
- 疯狂科学（Mad Science，仅混沌附加效果）

### 战斗随机选牌

- 坚毅（True Grit，未升级）
- 余烬（Cinder）
- 痛殴（Thrash）
- 未掘宝石（Hidden Gem）
- 能量汲取（Drain Power）
- 天选（Anointed）
- 探寻打击（Seeker Strike，显示随机候选）
- 骚动（Uproar）
- 横祸（Catastrophe）
- 狠揍（Beat Down）

### 随机目标攻击

- 散射炮（Flak Cannon）
- 连续反弹（Ricochet）
- 狂乱撕扯（Rip and Tear）
- 星尘（Stardust）
- 扫荡凝视（Sweeping Gaze）
- 飞剑回旋镖（Sword Boomerang）
- 连射（Volley）

### 充能球效果

- 球状闪电（Ball Lightning）
- 混沌（Chaos）
- 冰寒（Chill）
- 寒流（Cold Snap）
- 吞噬暗影（Consuming Shadow）
- 冷静头脑（Coolheaded）
- 漆黑（Darkness）
- 双重释放（Dualcast）
- 聚变（Fusion）
- 冰川（Glacier）
- 玻璃工艺（Glasswork）
- 冰之长枪（Ice Lance）
- 引火（Ignition）
- 陨石打击（Meteor Strike）
- 多重释放（Multi-Cast）
- 空值（Null）
- 四重释放（Quadcast）
- 彩虹（Rainbow）
- 折射（Refract）
- 暗影之盾（Shadow Shield）
- 打碎（Shatter）
- 旋转工艺（Spinner，升级后）
- 暴风雨（Tempest）
- 特斯拉线圈（Tesla Coil）
- 电流相生（Voltaic）
- 电击（Zap）

### 抽牌堆自动出牌

- 破灭（Havoc）
- 倾泻（Cascade）
- 精炼混沌（Distilled Chaos）

### 药水抽牌

- 迅捷药水（Swift Potion）
- 明晰提取物（Clarity Extract）
- 痊愈药水（Cure All）
- 发光水（Glowwater Potion）
- 异蛇之油（Snecko Oil，完整手牌和随机费用）
- 瓶装潜能（Bottled Potential）

### 卡牌抽牌

- 重启（Reboot）
- 计算下注（Calculated Gamble）

### 战斗随机变牌

- 熵（Entropy）

### 卡牌奖励

- 浮木（Driftwood）重掷
- 佩尔之翼（Pael's Wing）献祭奖励

### 非先古之民事件

- 混沌芳香、战痕累累的训练假人、脑蛭、色彩哲学家、茂密的植被、玩偶室、光与暗的门扉、无尽传送带、被寄生的自动机械、冷光合唱团、变形灵林谷、药水快递员、重拳出击、长者兰伟德、镜中倒影  影倒中镜、满屋芝士、圆桌茶会、滑脚木桥、共生体、真理石板、药水的未来？、传说是真的、这个还是那个？、打造时间、垃圾堆、审判、无休之处、战史学家 付袭、欢迎来到旺购百货、泉水、低语空谷事件选项的即时随机结果。

### 遗物拾起效果

- 涅奥（Neow）和其它先古之民遗物选项的即时随机结果
- 大锅、星系仪、芳香蘑菇、战纹涂料、磨刀石等遗物的拾起结果
- 遗物奖励中的即时随机结果
- 商店遗物中的即时随机结果
- 遗物交换商事件中交换得到的遗物即时随机结果

## Integrations

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
| 当前版本 | `0.11.0` |
| 最低游戏版本 | `0.109.0` |
| RitsuLib 依赖 | `0.4.58` |

## 设置

进入 RitsuLib 的模组设置页，找到 **随机数预测**：

| 设置项 | 作用 |
|---|---|
| 启用单人模式预测 | 控制单人模式中是否显示任何预测结果，默认开启 |
| 启用多人模式预测 | 控制多人模式中是否显示任何预测结果，默认开启 |
| 启用公平模式 | 只预测可以通过保存和读档获取的信息，默认开启 |
| 显示预测偏移警告 | 控制可能受副作用影响的预测是否显示警告提示，默认开启 |
| 预测变牌结果 | 控制变牌选择网格悬停提示和确认预览是否显示预测结果 |
| 预测战斗随机变牌 | 控制战斗内变牌选择是否显示预测卡牌 |
| 预测药水随机牌 | 控制随机印牌药水是否显示预测卡牌 |
| 预测随机生成药水 | 控制混沌药水和炼制药水是否显示预测药水 |
| 预测战斗随机印牌 | 控制战斗中手牌的随机印牌卡是否显示预测卡牌 |
| 预测战斗随机选牌 | 控制战斗中手牌的随机选已有牌效果是否显示预测卡牌和高亮 |
| 预测充能球效果 | 控制支持的充能球触发卡牌是否显示充能球效果将命中的目标 |
| 预测随机目标攻击 | 控制支持的随机敌人目标攻击牌是否显示将命中的目标 |
| 预测回合结束效果 | 控制是否显示支持的回合结束伤害预测 |
| 回合结束预测图标显示时机 | 控制回合结束伤害预测图标在悬停结束回合按钮时显示，还是玩家回合内始终显示 |
| 回合结束血条预测显示时机 | 控制回合结束伤害何时显示在目标血条上，默认玩家回合内始终显示 |
| 伤害预测血条颜色 | 控制伤害预测显示在目标血条上的颜色，默认黄色 |
| 预测抽牌堆自动出牌 | 控制破灭、倾泻和精炼混沌是否显示将从抽牌堆打出的牌 |
| 预测药水抽牌 | 控制支持的药水抽牌效果是否显示预测卡牌 |
| 预测卡牌抽牌 | 控制重启和计算下注是否显示预测卡牌 |
| 预测浮木重掷奖励 | 控制浮木重掷卡牌奖励时是否显示预测卡牌 |
| 预测佩尔之翼献祭奖励 | 控制触发佩尔之翼奖励的献祭按钮是否显示将获得的遗物 |
| 预测遗物拾起效果 | 控制遗物提示（包括先古之民选项）是否显示拾起时立即发生的随机卡牌、遗物、药水、诅咒和变牌结果 |
| 预测休息处结果 | 控制休息处选项是否显示捕梦网、小邮箱、铲子等遗物的即时随机结果 |
| 预测事件选项结果 | 控制非先古之民事件选项是否显示即时随机结果 |
| 启用水晶球透视 | 控制水晶球事件小游戏是否透过未揭开的迷雾显示物品 |
| 滑脚木桥重掷预览次数 | 控制滑脚木桥“抓紧”向后显示多少次重新随机的牌，默认 5 |
| 预测下一阶段先古之民和 Boss | 控制 Boss 奖励界面的状态栏是否显示下一阶段的先古之民和 Boss |
| 启用冻结之眼 | 控制抽牌堆查看界面是否按实际抽牌顺序显示 |
| 预测洗牌后牌序 | 控制冻结之眼抽牌堆界面是否预览弃牌堆洗入后的顺序 |

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
RandomForeseerCode/Debug/ - 调试入口和测试用奖励界面
RandomForeseerCode/Entry.cs - 模组入口与 Harmony patch 注册
RandomForeseerCode/InCombat/ - 战斗内卡牌、药水和冻结之眼预测
RandomForeseerCode/Integrations/ - 与其它模组的可选联动补丁
RandomForeseerCode/OutOfCombat/ - 战斗外事件、奖励、商店、休息处和变牌预测
RandomForeseerCode/OutOfCombat/Events/ - 非先古之民事件选项预测
RandomForeseerCode/RandomForeseerSettings.cs - 设置项定义、持久化和功能开关
project.godot - PCK 导出用 Godot 项目
scripts/release.ps1 - 本地构建、打包和发版脚本
workshop/loader/ - Steam 创意工坊多版本包加载器
```
