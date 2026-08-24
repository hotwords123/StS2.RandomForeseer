# Merchant card creation result hook

Simulation-facing hook facade: `OutOfCombat/Mirrors/HookMirrors.cs`.

Mirror files:

- `OutOfCombat/Mirrors/Hooks/CardCreation/ModifyMerchantCardCreationResultsMirrors.cs`
- `OutOfCombat/Mirrors/Hooks/CardCreation/CardCreationResultUtils.cs`

## Hook spec

- `AbstractModel.ModifyMerchantCardCreationResults(Player, List<CardCreationResult>)`
- `AbstractModel.ModifyMerchantCardPool(Player, IEnumerable<CardModel>)`
- `AbstractModel.ModifyMerchantCardRarity(Player, CardRarity)`
- `AbstractModel.ModifyMerchantPrice(Player, MerchantEntry, decimal)`
- `AbstractModel.ShouldRefillMerchantEntry(MerchantEntry, Player)`

Vanilla dispatches this hook after the merchant card and its otherwise ineffective upgrade roll are generated, and
before the entry price is rolled. The original listeners clone cards through the live `RunState`, so merchant restock
prediction must use exact mirrors against detached preview cards instead of invoking the original hook.

## Listeners

| Model | 中文名 | Original effect | Current mirror status |
| --- | --- | --- | --- |
| `FrozenEgg` | 冻结之蛋 | Upgrades generated Power cards. | Implemented. |
| `MoltenEgg` | 熔火之蛋 | Upgrades generated Attack cards. | Implemented. |
| `ToxicEgg` | 毒素之蛋 | Upgrades generated Skill cards. | Implemented. |
| `FresnelLens` | 菲涅耳透镜 | Adds Nimble to valid generated cards. | Implemented. |

## Parity notes

- `OutOfCombat.Mirrors.HookMirrors.ModifyMerchantCardCreationResults` preserves the original full listener pass and
  delegates exact-type dispatch to `ModifyMerchantCardCreationResultsMirrors`.
- The three Egg handlers intentionally do not apply the card-reward-only `NoHookUpgrades` gate; vanilla merchant
  creation invokes their merchant override unconditionally for the owning player.
- Egg upgrades and Fresnel Lens enchantment reuse `CardCreationResultUtils`, shared with the card reward
  mirrors while each hook retains its own applicability gates.
- Merchant card pool, rarity, price, and refill predicates are manually enumerated by the out-of-combat Hook facade,
  preserving original chaining and short-circuit behavior while allowing compatibility filtering. Upgrade odds use
  the same centralized card-reward facade.
- Unsupported gameplay overrides are reported through the standard method mirror registry. The context records the
  unsupported risk, but merchant restock prediction currently has no separate risk-tip projection path.

## Mock model list

- None.
