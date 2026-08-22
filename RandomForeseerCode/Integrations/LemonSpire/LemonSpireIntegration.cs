using System.Runtime.CompilerServices;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat;
using RandomForeseer.RandomForeseerCode.OutOfCombat;

namespace RandomForeseer.RandomForeseerCode.Integrations.LemonSpire;

/// <summary>Identifies the lemonSpire surface that owns a prediction context.</summary>
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

            (LemonSpirePredictionKind.AncientRelicChoice, RelicModel relic)
                => IsAncientRelicChoiceStale(player)
                    ? []
                    : RelicPickupPrediction.GetHoverTips(player, relic),

            (LemonSpirePredictionKind.ShopRelic, RelicModel relic)
                => RelicPickupPrediction.GetHoverTips(player, relic),

            (LemonSpirePredictionKind.ShopPotion, PotionModel potion)
                => PotionPrediction.GetHoverTips(player, potion),

            _ => []
        };
    }

    private static bool IsAncientRelicChoiceStale(Player player)
    {
        try
        {
            var eventModel = RunManager.Instance.EventSynchronizer.GetEventForPlayer(player);
            if (eventModel is not AncientEventModel ancient)
            {
                return false;
            }

            return ancient.IsFinished ||
                   ancient.CurrentOptions.Any(option => option is { Relic: not null, WasChosen: true });
        }
        catch
        {
            return false;
        }
    }
}

internal static class LemonSpireControlHoverTips
{
    private static readonly ConditionalWeakTable<Control, LemonSpirePredictionContext> Contexts = [];

    public static void Register(Control control, Player player, AbstractModel model, LemonSpirePredictionKind kind)
    {
        Contexts.AddOrUpdate(control, new LemonSpirePredictionContext(player, model, kind));
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
