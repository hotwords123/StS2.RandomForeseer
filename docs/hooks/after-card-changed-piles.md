# AfterCardChangedPiles hooks

Mirror file: none. The card-pile simulator currently mutates simulated piles directly and does not dispatch this hook.

## Hook specs

- `AbstractModel.AfterCardChangedPiles(CardModel, PileType oldPileType, AbstractModel? clonedBy)`
- `AbstractModel.AfterCardChangedPilesLate(CardModel, PileType oldPileType, AbstractModel? clonedBy)`

## Original listeners

| Model | Hook | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- | --- |
| `Hoarder` | Early | 囤积癖 | When a card is newly added to the deck, adds two extra copies unless this card is being cloned or skipped. | Out of scope for combat-pile simulation. Deck branch is intentionally not mirrored here. |
| `BingBong` | Early | 宾邦 | When owner's card is added to the deck, adds one extra copy unless this card is being cloned or skipped. | Out of scope for combat-pile simulation. Deck branch is intentionally not mirrored here. |
| `BookOfFiveRings` | Early | 五轮书 | Counts owner's deck additions; every 5 cards added heals owner. | Out of scope for combat-pile simulation. Deck branch is intentionally not mirrored here. |
| `DarkstonePeriapt` | Early | 黑石护符 | When owner gains a Curse into deck, raises max HP. | Out of scope for combat-pile simulation. Deck branch and max HP mutation are not mirrored here. |
| `LuckyFysh` | Early | 招财异鱼 | When owner adds a card to deck, grants gold. | Out of scope for combat-pile simulation. Deck branch and gold changes are not mirrored here. |
| `SovereignBlade` | Early | 君王之剑 | On this card moving piles, plays/clears Sovereign Blade combat VFX nodes for combat entry or exhaust. | Ignored. VFX node state does not affect prediction output. |
| `SoulFysh` | Late | 灵魂异鱼 | When local player's `Beckon` changes piles during active combat, updates a music parameter based on whether any `Beckon` remains in hand. | Ignored. Music state does not affect prediction output. |

## Parity notes

- For combat piles, currently reviewed vanilla listeners are either VFX/music-only or deck-only.
- `AfterCardChangedPiles` runs before `AfterCardChangedPilesLate` in vanilla. If a future mirror dispatches this hook, it must preserve that order.

## Mock model list

- `MockResetCombatOnShufflePower`
