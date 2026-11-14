using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;

namespace RandomForeseer.Integrations.LemonSpire.Patches;

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

    private static void Postfix(Control content)
    {
        foreach (var entry in LemonSpireControlTree.Descendants(content).OfType<NDeckHistoryEntry>())
        {
            LemonSpireControlHoverTips.Register(entry, new LemonSpirePredictionContext.HandCard(entry.Card));
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
                LemonSpireControlHoverTips.Register(
                    holder,
                    new LemonSpirePredictionContext.AncientRelicChoice(player, relic));
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
                LemonSpireControlHoverTips.Register(holder, new LemonSpirePredictionContext.ShopRelic(player, relic));
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
                LemonSpireControlHoverTips.Register(holder, new LemonSpirePredictionContext.ShopPotion(player, potion));
                return;
            }
        }
    }
}

[HarmonyPatchCategory(LemonSpireTypes.PatchCategory)]
[HarmonyPatch]
internal static class LemonSpireBlitzMerchantPredictionPatch
{
    private static bool Prepare()
    {
        return TargetMethod() is not null;
    }

    private static MethodBase? TargetMethod()
    {
        var type = LemonSpireTypes.Get(LemonSpireTypes.BlitzShopEntryPreviewName);
        return type != null
            ? AccessTools.Method(type, "Create", [typeof(BlitzMerchantEntry)])
            : null;
    }

    private static void Postfix(Control __result, BlitzMerchantEntry entry)
    {
        LemonSpireControlHoverTips.Register(__result, new LemonSpirePredictionContext.BlitzMerchant(entry));
    }
}
