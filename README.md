# Random Foreseer

语言 / Languages：中文 | [English](README.en.md)

《杀戮尖塔 2》的随机数预测模组。它会在不推进真实随机数的前提下，提前显示部分随机结果，减少确认前的盲猜。

## 功能

- **预测变牌结果**：在变牌确认预览中显示当前随机数状态将生成的确切卡牌。
- **预测随机给牌药水**：在随机给牌药水的提示中显示即将出现的卡牌。
- **预测战斗随机印牌**：战斗中悬停手牌里的随机印牌卡时，显示即将生成的卡牌；慷慨捐助会显示选择其它队友时的可能结果。
- **预测浮木重掷奖励**：悬停卡牌奖励的“重掷”按钮时，显示重掷后将出现的卡牌。
- **预测局外遗物结果**：悬停涅奥及其它先古之民遗物选项、遗物奖励和商店遗物时，显示获得后立即发生的随机结果。
- **预测事件选项结果**：悬停非先古之民事件选项时，显示即时随机奖励、随机升级/降级和随机后续选项；审判无辜选项会包含新加入疑虑的变牌结果。
- **冻结之眼**：战斗中查看抽牌堆时，按实际抽牌顺序显示卡牌。

这些功能都可以在模组设置页中单独开关，也可以通过单人/多人模式总开关整体关闭。公平模式默认开启，只预测可以通过保存和读档获取的信息。

## 当前支持的预测

### 变牌

- 星盘（Astrolabe）
- 新叶（New Leaf）
- 混沌芳香（Aroma of Chaos）
- 无尽传送带（Endless Conveyor）
- 变形灵林谷（Morphic Grove）
- 共生体（Symbiote）
- 审判（The Trial）
- 低语空谷（Whispering Hollow）

### 随机给牌药水

- 攻击药水（Attack Potion）
- 技能药水（Skill Potion）
- 能力药水（Power Potion）
- 无色药水（Colorless Potion）
- 宇宙药剂（Cosmic Concoction）
- 欧洛巴斯之酸（Orobic Acid）

### 战斗随机印牌

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

### 卡牌奖励

- 浮木（Driftwood）重掷

### 非先古之民事件

- 混沌芳香、战痕累累的训练假人、脑蛭、色彩哲学家、玩偶室、光与暗的门扉、无尽传送带、被寄生的自动机械、冷光合唱团、变形灵林谷、药水快递员、重拳出击、长者兰伟德、镜中倒影  影倒中镜、满屋芝士、圆桌茶会、滑脚木桥、共生体、真理石板、药水的未来？、传说是真的、这个还是那个？、打造时间、垃圾堆、审判、无休之处、战史学家 付袭、欢迎来到旺购百货、泉水、低语空谷事件选项的即时随机结果。

### 局外遗物

- 涅奥（Neow）和其它先古之民遗物选项的即时随机结果
- 星盘、炼金箱、召唤铃铛、玻璃眼珠、潘多拉魔盒、沙堡、卷轴箱、海玻璃、原初之爪、玩具盒、大锅、星系仪、芳香蘑菇、战纹涂料、磨刀石等遗物的拾起结果
- 遗物奖励中的即时随机结果
- 商店遗物中的即时随机结果

## 安装

1. 安装并启用 `STS2-RitsuLib`。
2. 将本模组发布包中的 `RandomForeseer` 目录放入游戏的 `mods` 目录。
3. 启动游戏，在模组列表中确认 `RandomForeseer` 已加载。

当前 manifest 目标：

| 项 | 值 |
|---|---|
| 最低游戏版本 | `0.106.0` |
| RitsuLib 依赖 | `0.3.8` |

## 设置

进入 RitsuLib 的模组设置页，找到 **随机数预测**：

| 设置项 | 作用 |
|---|---|
| 启用单人模式预测 | 控制单人模式中是否显示任何预测结果，默认开启 |
| 启用多人模式预测 | 控制多人模式中是否显示任何预测结果，默认开启 |
| 启用公平模式 | 只预测可以通过保存和读档获取的信息，默认开启 |
| 预测变牌结果 | 控制变牌确认预览是否显示预测结果 |
| 预测药水随机牌 | 控制随机给牌药水是否显示预测卡牌 |
| 预测战斗随机印牌 | 控制战斗中手牌的随机印牌卡是否显示预测卡牌 |
| 预测浮木重掷奖励 | 控制浮木重掷卡牌奖励时是否显示预测卡牌 |
| 预测局外遗物结果 | 控制先古之民遗物选项、遗物奖励和商店遗物是否显示即时随机结果 |
| 预测事件选项结果 | 控制非先古之民事件选项是否显示即时随机结果 |
| 滑脚木桥重掷预览次数 | 控制滑脚木桥“抓紧”向后显示多少次重新随机的牌，默认 5 |
| 启用冻结之眼 | 控制抽牌堆查看界面是否按实际抽牌顺序显示 |

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
RandomForeseerCode/                 C# 源码
RandomForeseer/localization/        设置页本地化
RandomForeseer.csproj               C# 项目与构建配置
RandomForeseer.json                 Mod manifest
project.godot                       PCK 导出用 Godot 项目
```
