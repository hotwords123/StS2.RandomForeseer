using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.RandomForeseerCode.Telemetry;

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
            screen is not { Pile.Type: PileType.Draw, _grid: { } grid })
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
