# Enchantment OnPlay mirror

## Vanilla entry

StS2 v0.111.0 calls `EnchantmentModel.OnPlay(PlayerChoiceContext, CardPlay?)` after the card's own `OnPlay` body and before the card's affliction. `CardModel.OnPlayWrapper` stops the remaining lifecycle if the owner dies during the enchantment effect.

Combat prediction dispatches the mutable preview enchantment through `EnchantmentOnPlayMirrors` at the same point. The registry uses exact runtime types, and the context maps an unchanged preview enchantment back to the original enchantment for trace ownership. Preview-only enchantments remain their own trace source.

## Registrations

| Enchantment | Mirrored behavior | Status |
| --- | --- | --- |
| `Adroit` | Gains the enchantment's block for the card owner with the predicted card and current `CardPlay` as sources. | Mirrored. |
| `Corrupted` | Deals 2 unblockable, unpowered Move damage to the owner with the predicted card and current `CardPlay` as sources. | Mirrored. |
| `Inky` | Applies Weak to the selected enemy or all enemies. | Power application is not represented in shadow combat state; records `MethodMirrorIncomplete`. |
| `Momentum` | Adds the enchantment amount to the mutable preview's accumulated extra damage for later plays in the series. | Mirrored. |
| `Sown` | While normal, grants its amount of energy; disables only when the card's AfterCardPlayed fires. | Mirrored across OnPlay and the after-card hook. |
| `Swift` | While normal, draws its amount from shadow piles; disables only when the card's AfterCardPlayed fires. | Mirrored across OnPlay and the after-card hook. |

`Sown` and `Swift` write the detached enchantment's `_status` field directly from their `AfterCardPlayed` mirrors.
Their OnPlay effects leave it normal during nested actions. Calling the original `Status` setter could invoke a
`StatusChanged` delegate copied from the live model and leak a UI side effect out of prediction.

## Dispatch and risk policy

Unregistered gameplay overrides follow the shared method-mirror policy and record `MethodNotMirrored`. Non-overrides use the empty base implementation without opening a method frame. The simulator checks shadow owner death immediately after dispatch and returns before affliction, finished history, and after-play hooks, matching vanilla's lifecycle boundary.
