using System.Runtime.CompilerServices;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen;
using RandomForeseer.Common;
using RandomForeseer.InCombat;
using RandomForeseer.OutOfCombat;

namespace RandomForeseer.Integrations.LemonSpire;

internal enum LemonSpirePredictionKind
{
    HandCard,
    AncientRelicChoice,
    ShopRelic,
    ShopPotion
}

internal sealed class LemonSpirePredictionContext(Player player, AbstractModel model, LemonSpirePredictionKind kind)
{
    public IReadOnlyList<IHoverTip> GetHoverTips()
    {
        return (kind, model) switch
        {
            (LemonSpirePredictionKind.HandCard, CardModel card)
                => CombatCardPrediction.GetHoverTips(card),

            (LemonSpirePredictionKind.AncientRelicChoice or LemonSpirePredictionKind.ShopRelic, RelicModel relic)
                => RelicPickupPrediction.GetHoverTips(player, relic),

            (LemonSpirePredictionKind.ShopPotion, PotionModel potion)
                => PotionPrediction.GetHoverTips(player, potion),

            _ => []
        };
    }
}

internal static class LemonSpireControlHoverTips
{
    private static readonly ConditionalWeakTable<Control, LemonSpirePredictionContext> Contexts = [];

    public static void Register(Control control, Player player, AbstractModel model, LemonSpirePredictionKind kind)
    {
        Contexts.Remove(control);
        Contexts.Add(control, new LemonSpirePredictionContext(player, model, kind));
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control control)
    {
        return Contexts.TryGetValue(control, out var context)
            ? context.GetHoverTips()
            : [];
    }
}

internal static class LemonSpireControlTree
{
    public static HashSet<Control> SnapshotDescendants(Control parent)
    {
        return Descendants(parent).ToHashSet();
    }

    public static IEnumerable<Control> NewDescendants(Control parent, HashSet<Control> before)
    {
        return Descendants(parent)
            .Where(control => !before.Contains(control));
    }

    public static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (var child in parent.GetChildren().OfType<Control>())
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}

internal static class LemonSpireTypes
{
    public const string ModId = "lemonSpire2";
    public const string PatchCategory = Entry.ModId + ".LemonSpire";

    public const string AncientRelicChoiceProviderName =
        "lemonSpire2.PlayerStateEx.PanelProvider.AncientRelicChoiceProvider";

    public const string HandCardProviderName =
        "lemonSpire2.PlayerStateEx.PanelProvider.HandCardProvider";

    public const string ShopProviderName =
        "lemonSpire2.PlayerStateEx.PanelProvider.ShopProvider";

    public static Type? Get(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        if (type != null)
        {
            return type;
        }

        try
        {
            return Assembly.Load(ModId).GetType(fullName);
        }
        catch
        {
            return null;
        }
    }
}

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
