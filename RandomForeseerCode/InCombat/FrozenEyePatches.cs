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
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.RandomForeseerCode.Localization;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Utils.HarmonyIl;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CardPileUtils
{
    public static bool TryGetDrawPileOwner(CardPile pile, [NotNullWhen(true)] out Player? player)
    {
        player = CombatManager.Instance.LiveCombatState?.Players
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

    public static void SetPredictedShuffleCards(NCardGrid grid, IEnumerable<CardModel> predictedCards)
    {
        PredictedShuffleCardsByGrid.AddOrUpdate(grid, [.. predictedCards]);
    }
}

internal static class FrozenEyeDrawPileView
{
    public static bool TryRefresh(NCardPileScreen screen)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.FrozenEyeEnabled ||
            screen is not { Pile.Type: PileType.Draw, _grid: NCardGrid grid })
        {
            return false;
        }

        var previewCards = screen.Pile.Cards;

        if (GetShufflePrediction(screen) is { Count: > 0 } cards)
        {
            FrozenEyeDrawPileViewState.SetPredictedShuffleCards(grid, cards);
            previewCards = [..previewCards, ..cards];
        }
        else
        {
            FrozenEyeDrawPileViewState.SetPredictedShuffleCards(grid, []);
        }

        grid.SetCards(previewCards, PileType.Draw, [SortingOrders.Ascending]);
        return true;
    }

    private static IReadOnlyList<CardModel> GetShufflePrediction(NCardPileScreen screen)
    {
        if (!ModData.Settings.ShufflePredictionEnabled ||
            !CardPileUtils.TryGetDrawPileOwner(screen.Pile, out var player) ||
            player.Creature.CombatState is not { } combatState ||
            combatState.CurrentSide != player.Creature.Side)
        {
            return [];
        }

        try
        {
            var simulator = new CombatPredictionSimulator(combatState);
            var playerState = simulator.State.GetPlayerCombatState(player);
            if (playerState.DiscardPile.IsEmpty)
            {
                return [];
            }

            playerState.DrawPile.Clear();
            simulator.Shuffle(player);
            return [.. playerState.DrawPile.Cards.SelectPreviews()];
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Failed to predict draw pile shuffle for {player.Creature.Name}: {ex}");
            ModTelemetry.CaptureException(ex, "frozen_eye_prediction", "predict_draw_pile_shuffle");
            return [];
        }
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
            if (!ModData.Settings.ShufflePredictionEnabled)
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
            ModTelemetry.CaptureException(
                ex,
                "frozen_eye_patch",
                "rewrite_empty_draw_pile_open",
                TelemetryContext.ForMethod(original));
            return instructionList;
        }
    }

    private static bool ShouldTreatPileAsEmpty(CardPile pile)
    {
        if (!pile.IsEmpty)
        {
            return false;
        }

        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.FrozenEyeEnabled || !settings.ShufflePredictionEnabled ||
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
        try
        {
            __result = (__instance.LocTable, __instance.LocEntryKey) switch
            {
                ("static_hover_tips", "DRAW_PILE.description") => ReplaceDrawPileDescription(__result),
                ("gameplay_ui", "DRAW_PILE_INFO") => ReplaceDrawPileInfo(__result),
                _ => __result
            };
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Failed to replace raw text for {__instance}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "frozen_eye_patch",
                "replace_draw_pile_text",
                new { __instance.LocTable, __instance.LocEntryKey });
        }
    }

    private static string ReplaceDrawPileDescription(string text)
    {
        var settings = ModData.Settings;
        if (settings is not { IsPredictionEnabled: true, FrozenEyeEnabled: true })
        {
            return text;
        }

        var mainDescription = text.Split("\n\n", 2)[0];
        var viewDescription = ModLocalization.Text("frozen_eye.draw_pile_hover_view").GetRawText();

        return $"{mainDescription}\n\n{viewDescription}";
    }

    private static string ReplaceDrawPileInfo(string text)
    {
        var settings = ModData.Settings;
        if (settings is not { IsPredictionEnabled: true, FrozenEyeEnabled: true })
        {
            return text;
        }

        var firstLine = text.Split('\n', 2)[0];
        var orderInfoKey = settings.ShufflePredictionEnabled
            ? "frozen_eye.draw_pile_info_order_with_shuffle_prediction"
            : "frozen_eye.draw_pile_info_order";
        var orderInfo = ModLocalization.Text(orderInfoKey).GetRawText();

        return $"{firstLine}\n{orderInfo}";
    }
}
