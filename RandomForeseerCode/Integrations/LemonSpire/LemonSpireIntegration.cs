using System.Runtime.CompilerServices;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.Common;
using RandomForeseer.InCombat;
using RandomForeseer.OutOfCombat;

namespace RandomForeseer.Integrations.LemonSpire;

/// <summary>Owns the prediction inputs and validity rules for one lemonSpire surface.</summary>
internal abstract class LemonSpirePredictionContext
{
    public abstract IReadOnlyList<IHoverTip> GetHoverTips();

    internal sealed class HandCard(CardModel card) : LemonSpirePredictionContext
    {
        public override IReadOnlyList<IHoverTip> GetHoverTips() => CombatCardPrediction.GetHoverTips(card);
    }

    internal sealed class AncientRelicChoice(Player player, RelicModel relic) : LemonSpirePredictionContext
    {
        public override IReadOnlyList<IHoverTip> GetHoverTips()
        {
            return IsStale(player)
                ? []
                : RelicPickupPrediction.GetHoverTips(player, relic);
        }

        private static bool IsStale(Player player)
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

    internal sealed class ShopRelic(Player player, RelicModel relic) : LemonSpirePredictionContext
    {
        public override IReadOnlyList<IHoverTip> GetHoverTips() => RelicPickupPrediction.GetHoverTips(player, relic);
    }

    internal sealed class ShopPotion(Player player, PotionModel potion) : LemonSpirePredictionContext
    {
        public override IReadOnlyList<IHoverTip> GetHoverTips() => PotionPrediction.GetHoverTips(player, potion);
    }

    internal sealed class BlitzMerchant(BlitzMerchantEntry entry) : LemonSpirePredictionContext
    {
        public BlitzMerchantEntry Entry { get; } = entry;

        public override IReadOnlyList<IHoverTip> GetHoverTips() => BlitzMerchantPrediction.GetHoverTips(Entry);
    }
}

internal static class LemonSpireControlHoverTips
{
    private static readonly ConditionalWeakTable<Control, LemonSpirePredictionContext> Contexts = [];

    static LemonSpireControlHoverTips()
    {
        BlitzMerchantPurchasePredictionState.Changed += RefreshBlitzMerchantHoverTips;
    }

    public static void Register(Control control, LemonSpirePredictionContext context)
    {
        Contexts.AddOrUpdate(control, context);
    }

    public static IReadOnlyList<IHoverTip> GetHoverTips(Control control)
    {
        return Contexts.TryGetValue(control, out var context)
            ? context.GetHoverTips()
            : [];
    }

    private static void RefreshBlitzMerchantHoverTips(Player player)
    {
        foreach (var (control, context) in Contexts)
        {
            if (context is not LemonSpirePredictionContext.BlitzMerchant { Entry: var entry } ||
                entry.Player != player)
            {
                continue;
            }

            try
            {
                if (!GodotObject.IsInstanceValid(control) || !control.IsInsideTree() ||
                    !NHoverTipSet._activeHoverTips.ContainsKey(control))
                {
                    continue;
                }

                // A teammate can begin a purchase while its old prediction is still under the local cursor.
                // Rebuild the active tip immediately; the pending gate keeps only vanilla tips during settlement.
                NHoverTipSet.Remove(control);
                if (entry.IsStocked && control.IsVisibleInTree())
                {
                    var alignment = control.GetIndex() % 3 == 2 ? HoverTipAlignment.Left : HoverTipAlignment.Right;
                    NHoverTipSet.CreateAndShow(control, entry.HoverTips, alignment)?.SetFollowOwner();
                }
            }
            catch (Exception ex)
            {
                // UI refresh failures must not change the original purchase Task's result or cleanup.
                Entry.Logger.Warn($"Could not refresh lemonSpire Blitz merchant hover tips: {ex}");
            }
        }
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

    public const string BlitzShopEntryPreviewName =
        "lemonSpire2.PlayerStateEx.PanelProvider.BlitzShopEntryPreview";

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
