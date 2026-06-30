using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using RandomForeseer.RandomForeseerCode.Common;
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CardPileUtils
{
    public static bool TryGetDrawPileOwner(CardPile pile, [NotNullWhen(true)] out Player? player)
    {
        player = CombatManager.Instance._state?.Players
            .FirstOrDefault(candidate => candidate.PlayerCombatState?.DrawPile == pile);
        return player != null;
    }
}

internal static class FrozenEyeDrawPileViewState
{
    private static readonly ConditionalWeakTable<NCardGrid, HashSet<CardModel>> PredictedShuffleCardsByGrid = [];

    public static bool TryGetPredictedShuffleCards(NCardGrid grid, [NotNullWhen(true)] out HashSet<CardModel>? predictedCards)
    {
        return PredictedShuffleCardsByGrid.TryGetValue(grid, out predictedCards);
    }

    public static void SetPredictedShuffleCards(NCardGrid grid, IReadOnlyList<CardModel> predictedCards)
    {
        PredictedShuffleCardsByGrid.AddOrUpdate(grid, predictedCards.ToHashSet());
    }
}

internal static class FrozenEyeDrawPileView
{
    public static bool TryRefresh(NCardPileScreen screen)
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
            FrozenEyeDrawPileViewState.SetPredictedShuffleCards(grid, prediction.Cards);
            previewCards = previewCards.Concat(prediction.Cards).ToList();
        }
        else
        {
            FrozenEyeDrawPileViewState.SetPredictedShuffleCards(grid, []);
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

        if (!CardPileUtils.TryGetDrawPileOwner(screen.Pile, out var player) ||
            player.Creature.CombatState?.CurrentSide != player.Creature.Side)
        {
            return false;
        }

        prediction = DrawPilePrediction.PredictShuffleAfterDrawPileDepleted(player);
        return prediction.Cards.Count > 0;
    }
}

[HarmonyPatch(typeof(NCardPileScreen))]
internal static class FrozenEyeCardPileScreenPatches
{
    private static readonly ConditionalWeakTable<NCardPileScreen, ScreenRefreshCallback> RefreshCallbacks = [];

    [HarmonyPatch("OnPileContentsChanged")]
    [HarmonyPrefix]
    private static bool RefreshDrawPileView(NCardPileScreen __instance)
    {
        return !FrozenEyeDrawPileView.TryRefresh(__instance);
    }

    [HarmonyPatch(nameof(NCardPileScreen._EnterTree))]
    [HarmonyPostfix]
    private static void SubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!CardPileUtils.TryGetDrawPileOwner(__instance.Pile, out var player) ||
            player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        var callback = RefreshCallbacks.GetValue(__instance, screen => new ScreenRefreshCallback(screen));
        playerCombatState.DiscardPile.ContentsChanged -= callback.Refresh;
        playerCombatState.DiscardPile.ContentsChanged += callback.Refresh;
    }

    [HarmonyPatch(nameof(NCardPileScreen._ExitTree))]
    [HarmonyPostfix]
    private static void UnsubscribeDiscardPileChanges(NCardPileScreen __instance)
    {
        if (!CardPileUtils.TryGetDrawPileOwner(__instance.Pile, out var player) ||
            player.PlayerCombatState is not { } playerCombatState)
        {
            return;
        }

        if (RefreshCallbacks.TryGetValue(__instance, out var callback))
        {
            playerCombatState.DiscardPile.ContentsChanged -= callback.Refresh;
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

            FrozenEyeDrawPileView.TryRefresh(screen);
        }
    }
}

[HarmonyPatch(typeof(NCombatCardPile), "OnRelease")]
internal static class FrozenEyeEmptyDrawPileOpenPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
    {
        var instructionList = instructions.ToList();

        try
        {
            var rewriter = HarmonyIlRewriter.From(instructionList, original);
            rewriter
                .ReplaceCall(
                    "allow empty draw pile view when shuffle prediction can be shown",
                    AccessTools.PropertyGetter(typeof(CardPile), nameof(CardPile.IsEmpty)),
                    AccessTools.Method(typeof(FrozenEyeEmptyDrawPileOpenPatch), nameof(ShouldTreatPileAsEmpty)))
                .RequireExactly(1);
            return rewriter.InstructionsChecked("Frozen Eye empty draw pile open");
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Frozen Eye empty draw pile transpiler failed for {original.FullDescription()}: {ex}");
            return instructionList;
        }
    }

    private static bool ShouldTreatPileAsEmpty(CardPile pile)
    {
        if (!pile.IsEmpty)
        {
            return false;
        }

        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableFrozenEye) ||
            !RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableShufflePrediction) ||
            !CardPileUtils.TryGetDrawPileOwner(pile, out var player) ||
            player.Creature.CombatState?.CurrentSide != player.Creature.Side)
        {
            return true;
        }

        return player.PlayerCombatState?.DiscardPile.IsEmpty ?? true;
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
        if (!FrozenEyeDrawPileViewState.TryGetPredictedShuffleCards(grid, out var predictedCards))
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
