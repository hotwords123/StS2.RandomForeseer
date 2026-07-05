# AfterCardEnteredCombat hook

Mirror file: none. The card-pile simulator currently adds generated cards to simulated piles but does not dispatch this hook.

## Hook spec

- `AbstractModel.AfterCardEnteredCombat(CardModel)`

## Original listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `Hexed` | 邪咒 | When this afflicted card enters combat and its owner no longer has `HexPower`, clears the affliction. | Not mirrored. Affliction clearing on combat entry is currently absent. |
| `GhostSeed` | 幽灵种子 | Owner's basic Strike/Defend cards that are not already locally Ethereal gain Ethereal. | Not mirrored. Keyword application on combat entry is currently absent. |
| `BansheesCry` | 女妖之嚎 | When this non-clone card enters combat, reduces this-combat cost by its energy amount for each Ethereal card owner played this combat. | Not mirrored. Requires shadow card-play history and preview cost mutation. |
| `GalvanicPower` | 流电 | Unafflicted Power cards entering combat gain `Galvanized`. | Not mirrored. Affliction application on combat entry is currently absent. |
| `HexPower` | 恶咒 | Owner's cards entering combat gain `Hexed` if unafflicted. | Not mirrored. Affliction application and Hexed-driven Ethereal are currently absent. |
| `PhantomBladesPower` | 幻影之刃 | Owner's Shivs entering combat gain Retain. | Not mirrored. Keyword application on generated Shivs is currently absent. |
| `RingingPower` | 昏眩 | Owner's unafflicted cards entering combat gain `Ringing`. | Not mirrored. This can affect `ShouldPlay` later in the turn. |
| `SmoggyPower` | 烟雾弥漫 | Owner's unafflicted Skill cards entering combat gain `Smog` if owner has started a Skill this turn. | Not mirrored. Requires shadow card-play-started history and affliction application. |
| `SwordSagePower` | 剑圣 | Owner's non-clone `SovereignBlade` entering combat gains Replay equal to the power amount. | Not mirrored. Replay count mutation on generated `SovereignBlade` is currently absent. |
| `TangledPower` | 缠结 | Owner's unafflicted Attack cards entering combat gain `Entangled`. | Not mirrored. This can affect energy cost. |
| `VitalSparkPower` | 活力火花 | Unafflicted Skill cards entering combat gain `Tainted` with this power's amount. | Not mirrored. Affliction application on combat entry is currently absent. |
| `Flatten` | 重压 | When this card enters combat after Osty has attacked this turn, sets this-turn cost to 0. | Not mirrored for combat entry. The separate `AfterAttack` cost reduction is handled in `attack-hooks.md`. |
| `Midnight` | 午夜 | When this non-clone card enters combat, reduces this-combat cost by the number of cards exhausted this combat. | Not mirrored for combat entry. The separate per-exhaust reduction is handled in `after-card-exhausted.md`. |
| `Pinpoint` | 精密瞄准 | When this non-clone card enters combat, reduces this-turn cost by the number of Skills owner played this turn. | Not mirrored. Requires shadow card-play-finished history. |
| `Stomp` | 踩踏 | When this non-clone card enters combat, reduces this-turn cost by the number of Attacks owner played this turn. | Not mirrored. Requires shadow card-play-finished history. |

## Parity notes

- Vanilla dispatches this hook from `CardPileCmd.Add` only when `oldPile == null`, the target is a combat pile, and the card is not merely changing owners.
- `AddGeneratedCard(s)ToCombat` reaches this hook through `Add`, before `AfterCardChangedPiles` and before `AfterCardGeneratedForCombat`.
- Most listeners mutate only the entering card: keywords, afflictions, cost, or replay count. Current generated-card prediction surfaces tolerate this drift, so dispatch is intentionally deferred.

## Mock model list

- None.
