using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;

namespace RandomForeseer.RandomForeseerCode.Integrations.LemonSpire.Patches;

[HarmonyPatchCategory(LemonSpireTypes.PatchCategory)]
[HarmonyPatch]
internal static class LemonSpireHandCardPredictionPatch
{
    private static bool Prepare()
    {
        return LemonSpireTypes.Get(LemonSpireTypes.HandCardProviderName) != null;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            LemonSpireTypes.Get(LemonSpireTypes.HandCardProviderName),
            "UpdateContent");
    }

    private static void Postfix(Player player, Control content)
    {
        foreach (var entry in LemonSpireControlTree.Descendants(content).OfType<NDeckHistoryEntry>())
        {
            LemonSpireControlHoverTips.Register(entry, player, entry.Card, LemonSpirePredictionKind.HandCard);
        }
    }
}

[HarmonyPatchCategory(LemonSpireTypes.PatchCategory)]
[HarmonyPatch]
internal static class LemonSpireAncientRelicChoicePredictionPatch
{
    private static bool Prepare()
    {
        return LemonSpireTypes.Get(LemonSpireTypes.AncientRelicChoiceProviderName) != null;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            LemonSpireTypes.Get(LemonSpireTypes.AncientRelicChoiceProviderName),
            "AddRelicItem");
    }

    private static void Prefix(HBoxContainer row, out HashSet<Control> __state)
    {
        __state = LemonSpireControlTree.SnapshotDescendants(row);
    }

    private static void Postfix(HBoxContainer row, Player player, RelicModel relic, HashSet<Control> __state)
    {
        foreach (var holder in LemonSpireControlTree.NewDescendants(row, __state))
        {
            if (holder is NRelicBasicHolder)
            {
                LemonSpireControlHoverTips.Register(holder, player, relic, LemonSpirePredictionKind.AncientRelicChoice);
                return;
            }
        }
    }
}

[HarmonyPatchCategory(LemonSpireTypes.PatchCategory)]
[HarmonyPatch]
internal static class LemonSpireShopRelicPredictionPatch
{
    private static bool Prepare()
    {
        return LemonSpireTypes.Get(LemonSpireTypes.ShopProviderName) != null;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            LemonSpireTypes.Get(LemonSpireTypes.ShopProviderName),
            "AddRelicItem");
    }

    private static void Prefix(HBoxContainer row, out HashSet<Control> __state)
    {
        __state = LemonSpireControlTree.SnapshotDescendants(row);
    }

    private static void Postfix(HBoxContainer row, Player player, HashSet<Control> __state)
    {
        foreach (var holder in LemonSpireControlTree.NewDescendants(row, __state))
        {
            if (holder is NRelicBasicHolder { _model: { } relic })
            {
                LemonSpireControlHoverTips.Register(holder, player, relic, LemonSpirePredictionKind.ShopRelic);
                return;
            }
        }
    }
}

[HarmonyPatchCategory(LemonSpireTypes.PatchCategory)]
[HarmonyPatch]
internal static class LemonSpireShopPotionPredictionPatch
{
    private static bool Prepare()
    {
        return LemonSpireTypes.Get(LemonSpireTypes.ShopProviderName) != null;
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            LemonSpireTypes.Get(LemonSpireTypes.ShopProviderName),
            "AddPotionItem");
    }

    private static void Prefix(HBoxContainer row, out HashSet<Control> __state)
    {
        __state = LemonSpireControlTree.SnapshotDescendants(row);
    }

    private static void Postfix(HBoxContainer row, Player player, HashSet<Control> __state)
    {
        foreach (var holder in LemonSpireControlTree.NewDescendants(row, __state))
        {
            if (holder is NPotionHolder { Potion.Model: { } potion })
            {
                LemonSpireControlHoverTips.Register(holder, player, potion, LemonSpirePredictionKind.ShopPotion);
                return;
            }
        }
    }
}
