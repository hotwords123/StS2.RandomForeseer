# Changelog

## Unreleased

### feat

- 新增充能球效果预测，支持混沌、漆黑、双重释放、聚变、冰川、玻璃工艺、多重释放、四重释放、彩虹、暗影之盾、旋转工艺（升级后）、暴风雨、电流相生和电击。<br>
  Added orb effect prediction for Chaos, Darkness, Dualcast, Fusion, Glacier, Glasswork, Multi-Cast, Quadcast, Rainbow, Shadow Shield, Spinner (upgraded), Tempest, Voltaic, and Zap.

- 新增回合结束效果预测，会按目标显示所有结束回合玩家的支持伤害效果总计，并可设置为悬停结束回合按钮时显示或玩家回合内始终显示。<br>
  Added end-turn effect prediction, showing aggregated supported damage for all players ending their turn, with display options for End Turn button hover or always during the player turn.

- 遗物交换商事件中，交换得到的遗物现在也会显示拾起效果预测。<br>
  Relic Trader now also shows pickup effect predictions for the relic received from a trade.

## v0.6.3

### feat

- 战斗中支持对三选一界面的卡牌也显示预测。<br>
  Added prediction for cards shown in choose-one screens during combat.

- 真理石板选项现在会同时预览后续随机升级结果，直到最后一次升级所有卡牌之前。<br>
  Tablet of Truth options now also preview later random upgrades until before the final upgrade-all choice.

### fix

- 修复单组卡牌预测顺序被错误反转的问题。<br>
  Fixed single-bundle card predictions being shown in reversed order.

- 修复局外变牌预测中，玩家返回重新选择时预测错误的问题。<br>
  Fixed incorrect out-of-combat transform predictions after returning to reselect cards.

## v0.6.2

### feat

- 新增水晶球事件透视，可透过未揭开的迷雾查看小游戏中的物品位置和类型。(@GuMengSama)<br>
  Added Crystal Sphere clairvoyance, showing item locations and types through unrevealed fog in the minigame. (@GuMengSama)

- 改进局内和局外变牌悬停预测，会按选择位置显示同一张牌的所有可能结果，并将非当前位置结果变暗。<br>
  Improved in-combat and out-of-combat transform hover prediction to show all position results for the hovered card, dimming non-current positions.

### fix

- 修复小型扭蛋预测错误地受公平模式限制的问题。<br>
  Fixed Small Capsule prediction being incorrectly gated by fair mode.

## v0.6.1

### fix

- 修复奖励页中同页还有其它未领取遗物时，华美发束预测可能错误显示的问题。<br>
  Fixed Silken Tress prediction on reward screens where other unclaimed relics can shift the immediate pickup result.

### perf

- 适配《杀戮尖塔 2》0.107.1，并优化随机数状态克隆以减少预测开销。<br>
  Adapted to Slay the Spire 2 0.107.1 and optimized RNG state cloning to reduce prediction overhead.

## v0.6.0

### feat

- 新增抽牌预测，支持迅捷药水、明晰提取物、痊愈药水、发光水、异蛇之油、瓶装潜能、重启和计算下注；抽牌堆不足时会预览洗牌后的后续结果，异蛇之油还会显示完整手牌和随机费用。<br>
  Added draw prediction for Swift Potion, Clarity Extract, Cure All, Glowwater Potion, Snecko Oil, Bottled Potential, Reboot, and Calculated Gamble, including post-shuffle draws when the draw pile is short and full-hand/random-cost previews for Snecko Oil.

- 改进冻结之眼，玩家回合查看抽牌堆时会以亮度降低的卡牌预览当前弃牌堆洗入后的顺序；抽牌堆为空但可显示洗牌预览时也允许打开抽牌堆界面。<br>
  Improved Frozen Eye so the draw pile screen previews the discard pile's shuffled-in order with dimmed cards during the player's turn, and can open an empty draw pile when a shuffle preview is available.

- 新增 lemonSpire2 联动，队友面板中的手牌、先古之民遗物奖励、商店遗物和商店药水会复用 Random Foreseer 的现有预测提示。<br>
  Added lemonSpire2 integration, reusing Random Foreseer's existing predictions for teammate hand cards, Ancient relic rewards, merchant relics, and merchant potions.

- 新增茂密的植被休息奖励预测。<br>
  Added Dense Vegetation rest reward prediction.

- 改进预测偏移警告，统一为全局开关，并在可识别时显示可能导致预测偏移的具体来源。<br>
  Improved drift warnings with a shared global toggle and source names when the possible cause can be identified.

### fix

- 修复奖励药水不显示预测、历史记录中的药水错误显示预测的问题。<br>
  Fixed potion predictions missing on reward potions and appearing incorrectly in run history.

- 修复休息处铲子挖掘预测只显示遗物本体、不显示遗物拾起即时效果的问题。<br>
  Fixed rest-site Shovel dig prediction showing only the relic itself instead of also showing immediate pickup effects.

- 修复无休之处“就这样休息”选项错误显示遗物预测的问题。<br>
  Fixed Unrest Site relic prediction appearing on the Rest Anyways option.

- 修复遗物提示中的预测卡牌在空间不足时没有应用回退布局的问题。<br>
  Fixed predicted cards in relic tooltips not using the fallback layout when space is limited.

## v0.5.0

### feat

- 新增抽牌堆自动出牌预测，支持破灭、倾泻和精炼混沌，并可在抽牌堆不足时预览洗牌后的后续结果。<br>
  Added draw-pile autoplay prediction for Havoc, Cascade, and Distilled Chaos, including post-shuffle previews when the draw pile is short.

### fix

- 修复药水随机印牌预测未正确受公平模式限制的问题。<br>
  Fixed potion card predictions not being properly gated by fair mode.

- 修复预览模型克隆对 canonical 实例的依赖，避免部分悬停来源无法生成预测。<br>
  Fixed preview model cloning requiring canonical instances, which prevented predictions from being produced for some hover sources.

- 修复完整熵变牌选择悬停预测、生成卡牌原始费用显示和战斗预测悬停阶段判断等问题。<br>
  Fixed full Entropy transform hover prediction, original cost display for generated cards, and combat prediction hover phase checks.

- 修复部分事件预测的公平模式或战斗结束效果处理，包括重拳出击奖励和事件选项中的战斗结束随机结果。<br>
  Fixed fair-mode or combat-end-effect handling for some event predictions, including Punch Off rewards and combat-end random results from event options.

## v0.4.0

### feat

- 新增随机生成药水预测，支持混沌药水和炼制药水，并覆盖商店里的混沌药水。<br>
  Added potion generation prediction for Entropic Brew and Alchemize, including Entropic Brew in merchant stock.

- 新增战斗随机变牌预测，战斗中“熵”选择手牌变牌时显示即将变化得到的卡牌。<br>
  Added combat transform prediction, showing the cards that Entropy will transform selected hand cards into during combat.

- 改进变牌选择网格悬停预测，已选中牌按选择顺序显示结果，未选中牌按下一个选择位置显示结果。<br>
  Improved transform selection grid hover prediction so selected cards show results in selection order and unselected cards show the next-position result.

### fix

- 修复预测卡牌悬停提示的水平重叠和垂直裁剪回退布局问题。<br>
  Fixed horizontal overlap and vertical clamping fallback issues in predicted card hover tips.

- 修复拖动战斗手牌时预测提示被隐藏的问题。<br>
  Fixed combat card prediction tips being hidden while dragging cards.

- 适配《杀戮尖塔 2》0.107.0。<br>
  Adapted predictions for Slay the Spire 2 0.107.0.

## v0.3.0

### feat

- 新增变牌选择网格悬停预测，在选择前显示按当前选择位置计算的变牌结果。<br>
  Added transform selection grid hover prediction, showing transform results for the current selection position before confirming.

- 新增宝箱房遗物拾取预测，悬停宝箱中的遗物时显示获得后的即时随机结果。<br>
  Added treasure room relic pickup prediction, showing immediate random results when hovering chest relics.

- 新增华美发束卡牌奖励预测。<br>
  Added Silken Tress card reward prediction.

- 新增休息处铲子挖掘遗物和其它休息处随机结果预测。<br>
  Added Shovel dig relic prediction and other rest-site random result predictions.

### fix

- 修复预测卡牌悬停提示可能重叠或过早换列的问题。<br>
  Fixed predicted card hover tips that could overlap or wrap to a side column too early.

- 修复单张卡牌包预测显示为多层分组的问题。<br>
  Fixed single-card bundle predictions being displayed as nested groups.

## v0.2.0

### feat

- 新增战斗随机选牌预测，支持坚毅、余烬、痛殴、未掘宝石、能量汲取、天选、探寻打击和骚动。<br>
  Added combat card selection prediction for True Grit, Cinder, Thrash, Hidden Gem, Drain Power, Anointed, Seeker Strike, and Uproar.

- 新增随机选牌预测警告，用于提示可能因伤害、格挡、死亡、抽牌或自动出牌等副作用发生偏移的预测。<br>
  Added warning tips for selection predictions that can shift because of side effects such as damage, block, death, draw, or auto-played cards.

- 新增慷慨捐助的队友选择随机印牌预测。<br>
  Added Largesse combat card generation prediction for teammate choices.

### fix

- 改进预测卡牌悬停提示布局，为预测卡牌预览增加侧边间距。<br>
  Improved prediction hover tip layout with extra side spacing for predicted card previews.

### refactor

- 重构预测界面文本的共享本地化处理，统一中文和英文提示文本。<br>
  Refactored shared prediction UI localization for cleaner Chinese and English text handling.

## v0.1.0

### feat

- 初版发布，支持在不推进真实随机数的前提下预览多类随机结果。<br>
  Initial release with RNG previews that do not advance the real game RNG.

- 支持变牌结果预测，覆盖星盘、新叶和多个事件来源的变牌确认预览。<br>
  Added transform prediction for Astrolabe, New Leaf, and multiple event transform confirmation previews.

- 支持随机给牌药水预测，显示随机药水即将生成的卡牌。<br>
  Added random-card potion prediction to show the cards generated by supported potions.

- 支持战斗随机印牌预测，悬停手牌中的随机印牌效果时显示即将生成的卡牌。<br>
  Added combat card generation prediction for supported in-hand random-card effects.

- 支持浮木卡牌奖励重掷预测，悬停重掷按钮时显示下一组奖励。<br>
  Added Driftwood card reward reroll prediction.

- 支持遗物拾起效果预测，覆盖先古之民遗物选项、遗物奖励和商店遗物的即时随机结果。<br>
  Added relic pickup effect prediction for Ancient relic options, relic rewards, and merchant relics.

- 支持非先古之民事件选项预测，显示即时随机奖励、随机升级/降级和后续随机选项。<br>
  Added non-Ancient event option prediction for immediate random rewards, random upgrades/downgrades, and random follow-up options.

- 支持冻结之眼，在战斗抽牌堆界面按实际抽牌顺序显示卡牌。<br>
  Added Frozen Eye support to show the combat draw pile in actual draw order.

- 新增模组设置和公平模式，可单独开关各类预测并限制只显示可通过保存和读档获取的信息。<br>
  Added mod settings and fair mode, including per-feature toggles and limits for information obtainable through Save & Load.
