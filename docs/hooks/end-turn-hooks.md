# End turn hooks

Mirror file: `InCombat/Hooks/EndTurnHooks.cs`.

## Hook specs

- `AbstractModel.AfterAutoPostPlayPhaseEntered(PlayerChoiceContext, Player)`
- `AbstractModel.BeforeSideTurnEndVeryEarly(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.BeforeSideTurnEndEarly(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`
- `AbstractModel.BeforeSideTurnEnd(PlayerChoiceContext, CombatSide, IEnumerable<Creature>)`

## AfterAutoPostPlayPhaseEntered listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `HowlFromBeyond` | 彼岸咆哮 | If in exhaust pile, auto-plays for owner. | Calls simulator `AutoPlay`, which currently marks risk. Precise mirror needs autoplay simulation. |
| `IAmInvincible` | 所向无敌 | If top of owner draw pile, auto-plays from draw pile. | Calls `AutoPlayFromDrawPile`, currently risk-heavy. Precise mirror needs autoplay simulation. |
| `StampedePower` | 惊逃 | Auto-plays playable Attacks in hand. | Marks risk and calls `AutoPlay` for candidates. Precise mirror needs autoplay simulation. |

## BeforeSideTurnEndVeryEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Orichalcum` | 奥利哈钢 | Records whether owner had no block before early end-turn block effects. | Implemented with state-store flag. |
| `FakeOrichalcum` | 奥利哈钢？？？ | Same as Orichalcum. | Implemented with state-store flag. |
| `AsleepPower` | 沉睡 | Removes Plating near wake-up timing. | Ignored. Power removal is unsupported and does not directly affect currently modeled predictions. |

## BeforeSideTurnEndEarly listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `PlatingPower` | 覆甲 | Owner gains block before damage effects. | Implemented via `GainBlock`. |
| `RegenPower` | 再生 | If the owner is a living participant, heals owner by amount and decrements Regen. | Risk only. Heal and power decrement are TODO; StS2 v0.108.0 places this before normal side-turn-end effects such as Doom, so end-turn death/HP forecasts can drift when Regen is present. |
| `PaelsEye` | 佩尔之眼 | If owner played no cards, exhausts all cards in hand before granting an extra turn. | Implemented for immediate hand exhaust and exhaust hooks; still marks risk because extra-turn scheduling is not modeled. |

## BeforeSideTurnEnd listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Orichalcum` | 奥利哈钢 | If very-early flag is set, owner gains block. | Implemented. |
| `FakeOrichalcum` | 奥利哈钢？？？ | Same as Orichalcum. | Implemented. |
| `CloakClasp` | 斗篷扣 | Owner gains block per card in hand. | Implemented. |
| `RippleBasin` | 波纹水盆 | Owner gains block if no Attack was played this turn. | Implemented. Uses live combat history like original. |
| `HailstormPower` | 冰雹风暴 | If owner has enough Frost orbs, damages all hittable enemies. | Implemented via `Damage`. |
| `ScreamingFlagon` | 尖叫酒壶 | If owner hand is empty, damages all hittable enemies. | Implemented via `Damage`. |
| `StoneCalendar` | 历石 | On configured turn, damages all hittable enemies. | Implemented via `Damage`. |
| `TheBombPower` | 炸弹 | At final countdown, damages all hittable enemies. | Implemented via `Damage`. |
| `DoomPower` | 灾厄 | Kills the first doomed creature on the side. | Risk only. Kill/death/combat-structure simulation is incomplete. |
| `Regret` | 悔恨 | Stores hand size so its turn-end-in-hand effect later deals unblockable self damage. | Currently ignored. This can affect HP/death and damage hooks; it should be risk or a mirror if end-turn-in-hand damage becomes part of prediction. |
| `DiamondDiadem` | 钻石头冠 | If few enough cards were played, applies `DiamondDiademPower`, then resets its counter. | Currently ignored. Apply Power is unsupported, but silently ignoring can miss later power effects. |
| `PaelsTears` | 佩尔之泪 | Gains energy. | Ignored. Energy does not affect current predictions. |
| `ChainsOfBindingPower` | 魂缚锁链 | Clears Bound from hand/discard/draw cards and resets internal flag. | Implemented for mirror pile state by clearing Bound on `PredictedCard` previews. Live internal flag reset is not mutated. |
| `SandpitPower` | 沙坑 | Updates creature positions on enemy side turn end. | Ignored. Enemy-side positioning does not affect current player-turn predictions. |

## Parity notes

- End-turn block and all-enemy damage handlers are consistent with original predicates, aside from VFX/SFX/status UI.
- StS2 v0.108.0 renamed the side-wide turn-end hooks from `BeforeTurnEnd` /
  `AfterTurnEnd` to `BeforeSideTurnEnd` / `AfterSideTurnEnd`. The simulator
  mirrors the player phase-one `BeforeSideTurnEnd` path; phase-two
  `AfterSideTurnEnd` remains outside this prediction surface.
- Damage handlers inherit current `Damage` post-hook omissions.
- Autoplay handlers are intentionally incomplete and should stay marked risky until `CombatPredictionSimulator.Execute/AutoPlay` can mirror real card play effects.
- Current ignored registrations for `Regret` and `DiamondDiadem` are acceptable for the present scope: `Regret` only records hand size in this hook, and `DiamondDiademPower` matters on later enemy attacks. They should be revisited if prediction expands across that boundary.
- `RegenPower` is a v0.108.0 ordering-sensitive listener: it now heals in `BeforeSideTurnEndEarly`, before `DoomPower`'s normal `BeforeSideTurnEnd` kill check. The current mirror marks matching cases risky and has a code TODO for shadow healing and power decrement.

## Mock model list

- `MockPhaseObserverPower`
