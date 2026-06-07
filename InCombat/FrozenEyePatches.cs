using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat;

[HarmonyPatch(typeof(NCardPileScreen), "OnPileContentsChanged")]
internal static class FrozenEyeCardPileScreenPatch
{
    private static readonly ConditionalWeakTable<NCardGrid, HashSet<CardModel>> PredictedShuffleCardsByGrid = [];

    private static bool Prefix(NCardPileScreen __instance)
    {
        return !TryRefreshDrawPileView(__instance);
    }

    public static bool TryRefreshDrawPileView(NCardPileScreen screen)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye) ||
            screen.Pile.Type != PileType.Draw ||
            screen._grid is not NCardGrid grid)
        {
            return false;
        }

        var previewCards = screen.Pile.Cards;

        if (TryGetShufflePrediction(screen, out var prediction))
        {
            SetPredictedShuffleCards(grid, prediction.Cards);
            previewCards = previewCards.Concat(prediction.Cards).ToList();
        }
        else
        {
            SetPredictedShuffleCards(grid, []);
        }

        grid.SetCards(previewCards, PileType.Draw, [SortingOrders.Ascending]);
        return true;
    }

    private static bool TryGetShufflePrediction(
        NCardPileScreen screen,
        out DrawPilePredictionResult prediction)
    {
        prediction = DrawPilePredictionResult.Empty;

        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableShufflePrediction))
        {
            return false;
        }

        if (!TryGetDrawPileOwner(screen, out var player) ||
            player.Creature.CombatState?.CurrentSide != player.Creature.Side)
        {
            return false;
        }

        prediction = DrawPilePrediction.PredictShuffleAfterDrawPileDepleted(player);
        return prediction.Cards.Count > 0;
    }

    public static bool TryGetDrawPileOwner(NCardPileScreen screen, [NotNullWhen(true)] out Player? player)
    {
        player = CombatManager.Instance._state?.Players
            .FirstOrDefault(candidate => candidate.PlayerCombatState?.DrawPile == screen.Pile);
        return player != null;
    }

    public static bool TryGetPredictedShuffleCards(NCardGrid grid, [NotNullWhen(true)] out HashSet<CardModel>? predictedCards)
    {
        return PredictedShuffleCardsByGrid.TryGetValue(grid, out predictedCards);
    }

    private static void SetPredictedShuffleCards(NCardGrid grid, IReadOnlyList<CardModel> predictedCards)
    {
        PredictedShuffleCardsByGrid.Remove(grid);
        PredictedShuffleCardsByGrid.Add(grid, predictedCards.ToHashSet());
    }
}

[HarmonyPatch(typeof(NCardPileScreen))]
internal static class FrozenEyeCardPileScreenRefreshPatch
{
    private static readonly ConditionalWeakTable<NCardPileScreen, ScreenRefreshCallback> RefreshCallbacks = [];

    [HarmonyPatch(nameof(NCardPileScreen._EnterTree))]
    [HarmonyPostfix]
    private static void SubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!FrozenEyeCardPileScreenPatch.TryGetDrawPileOwner(__instance, out var player) ||
            player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        var callback = RefreshCallbacks.GetValue(__instance, screen => new ScreenRefreshCallback(screen));
        combatState.DiscardPile.ContentsChanged -= callback.Refresh;
        combatState.DiscardPile.ContentsChanged += callback.Refresh;
    }

    [HarmonyPatch(nameof(NCardPileScreen._ExitTree))]
    [HarmonyPostfix]
    private static void UnsubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!FrozenEyeCardPileScreenPatch.TryGetDrawPileOwner(__instance, out var player) ||
            player.PlayerCombatState is not { } combatState)
        {
            return;
        }

        if (RefreshCallbacks.TryGetValue(__instance, out var callback))
        {
            combatState.DiscardPile.ContentsChanged -= callback.Refresh;
        }
    }

    [HarmonyPatch(nameof(NCardPileScreen.AfterCapstoneOpened))]
    [HarmonyPostfix]
    private static void RefreshAfterOpened(NCardPileScreen __instance)
    {
        __instance.OnPileContentsChanged();
    }

    private sealed class ScreenRefreshCallback(NCardPileScreen screen)
    {
        public void Refresh()
        {
            if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye) ||
                !RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableShufflePrediction))
            {
                return;
            }

            FrozenEyeCardPileScreenPatch.TryRefreshDrawPileView(screen);
        }
    }
}

internal static class FrozenEyeCardGridModulatePatch
{
    private static readonly Color PredictedShuffleCardModulate = new(0.65f, 0.65f, 0.65f);
    private static readonly ConditionalWeakTable<NCard, StrongBox<Color>> OriginalModulates = [];

    [HarmonyPatch(typeof(NCardGrid), "InitGrid", [])]
    private static class InitGridPatch
    {
        private static void Postfix(NCardGrid __instance)
        {
            ApplyPredictionModulate(__instance, __instance.CurrentlyDisplayedCardHolders);
        }
    }

    [HarmonyPatch(typeof(NCardGrid), "AssignCardsToRow", [typeof(List<NGridCardHolder>), typeof(int)])]
    private static class AssignCardsToRowPatch
    {
        private static void Postfix(NCardGrid __instance, List<NGridCardHolder> row)
        {
            ApplyPredictionModulate(__instance, row);
        }
    }

    private static void ApplyPredictionModulate(NCardGrid grid, IEnumerable<NGridCardHolder> holders)
    {
        if (!FrozenEyeCardPileScreenPatch.TryGetPredictedShuffleCards(grid, out var predictedCards))
        {
            return;
        }

        foreach (var holder in holders)
        {
            if (holder.Visible && holder.CardModel is { } card && predictedCards.Contains(card))
            {
                ApplyPredictedModulate(holder);
            }
            else
            {
                RestoreModulate(holder);
            }
        }
    }

    private static void ApplyPredictedModulate(NGridCardHolder holder)
    {
        if (holder.CardNode is not { } cardNode)
        {
            return;
        }

        if (!OriginalModulates.TryGetValue(cardNode, out _))
        {
            OriginalModulates.Add(cardNode, new StrongBox<Color>(cardNode.Modulate));
        }

        cardNode.Modulate = PredictedShuffleCardModulate;
    }

    private static void RestoreModulate(NGridCardHolder holder)
    {
        if (holder.CardNode is not { } cardNode ||
            !OriginalModulates.TryGetValue(cardNode, out var state))
        {
            return;
        }

        cardNode.Modulate = state.Value;
        OriginalModulates.Remove(cardNode);
    }
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class FrozenEyeDrawPileRawTextPatch
{
    private static void Postfix(LocString __instance, ref string __result)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye))
        {
            return;
        }

        switch (__instance)
        {
            case { LocTable: "static_hover_tips", LocEntryKey: "DRAW_PILE.description" }:
            {
                var mainDescription = __result.Split("\n\n", 2)[0];
                var viewDescription = PredictionLocalization.Text("frozen_eye.draw_pile_hover_view").GetRawText();

                __result = $"{mainDescription}\n\n{viewDescription}";
                break;
            }
            case { LocTable: "gameplay_ui", LocEntryKey: "DRAW_PILE_INFO" }:
            {
                var firstLine = __result.Split('\n', 2)[0];
                var orderInfoKey = RandomForeseerSettings.IsPredictionFeatureEnabled(
                    RandomForeseerSettings.EnableShufflePrediction)
                    ? "frozen_eye.draw_pile_info_order_with_shuffle_prediction"
                    : "frozen_eye.draw_pile_info_order";
                var orderInfo = PredictionLocalization.Text(orderInfoKey).GetRawText();

                __result = $"{firstLine}\n{orderInfo}";
                break;
            }
        }
    }
}
