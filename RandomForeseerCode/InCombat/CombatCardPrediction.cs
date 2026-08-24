using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.InCombat.Extensions;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.InCombat;

/// <summary>
/// Provides the single simulation facade used by combat-card HoverTips and target-aware UI surfaces.
/// </summary>
internal static class CombatCardPrediction
{
    private static readonly Dictionary<Type, ChoiceSourceBehavior> ChoiceSourceBehaviors = new()
    {
        [typeof(Abundance)] = new(SetsCardToFreeThisTurn: true),
        [typeof(Discovery)] = new(SetsCardToFreeThisTurn: true),
        [typeof(Quasar)] = new(),
        [typeof(Splash)] = new(SetsCardToFreeThisTurn: true),

        [typeof(AttackPotion)] = new(SetsCardToFreeThisTurn: true),
        [typeof(ColorlessPotion)] = new(SetsCardToFreeThisTurn: true),
        [typeof(PowerPotion)] = new(SetsCardToFreeThisTurn: true),
        [typeof(SkillPotion)] = new(SetsCardToFreeThisTurn: true),

        [typeof(Toolbox)] = new()
    };

    /// <summary>
    /// Builds prediction HoverTips for an in-combat card without requiring a selected target.
    /// </summary>
    /// <remarks>
    /// Card-play projection is shown only where an untargeted prediction can be resolved. Combat transform
    /// prediction remains a separate presentation path and is appended after the unified card-play result.
    /// </remarks>
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        if (!CombatManager.Instance.IsInProgress ||
            !card.IsMutable || card is not { Owner.Creature.CombatState: not null })
        {
            return [];
        }

        List<IHoverTip> predictionTips = [];

        if (ShouldShowCombatPlayPrediction(card))
        {
            try
            {
                var hoverTips = CombatCardPredictionController.TryGetActiveHoverTips(card, out var activeHoverTips)
                    ? activeHoverTips
                    : Predict(card, target: null)?.HoverTips ?? [];
                predictionTips.AddRange(hoverTips);
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Combat card play prediction failed for {card.Id}: {ex}");
                ModTelemetry.CaptureException(
                    ex,
                    "combat_card_prediction",
                    "build_play_hover_tips",
                    TelemetryContext.ForModel(card));
            }
        }

        try
        {
            predictionTips.AddRange(CombatTransformPrediction.GetCardHoverTips(card));
        }
        catch (Exception ex)
        {
            Entry.Logger.Warn($"Combat transform prediction failed for {card.Id}: {ex}");
            ModTelemetry.CaptureException(
                ex,
                "combat_card_prediction",
                "build_transform_hover_tips",
                TelemetryContext.ForModel(card));
        }

        return predictionTips;
    }

    /// <summary>
    /// Simulates one card play and projects every enabled card-play prediction feature from the same history.
    /// </summary>
    /// <param name="card">The live mutable card whose original identity anchors the prediction trace.</param>
    /// <param name="target">The selected target, or <see langword="null"/> when the card can resolve one automatically.</param>
    /// <returns>
    /// The completed presentation projection, or <see langword="null"/> when the card cannot be mirrored, its target cannot be
    /// resolved, or the simulated play is invalid.
    /// </returns>
    /// <remarks>Simulation and projection exceptions are intentionally handled by the calling UI injection boundary.</remarks>
    public static CombatPredictionProjection? Predict(CardModel card, Creature? target)
    {
        if (!CombatManager.Instance.IsInProgress ||
            card.Owner.Creature.CombatState is not { } combatState ||
            !CompatibilityUtils.IsPredictionAllowedForType(card.GetType()) ||
            !card.TryResolveTarget(ref target))
        {
            return null;
        }

        var simulator = new CombatPredictionSimulator(combatState);
        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);

        if (TryGetChoiceSimulationPlan(card, out var choiceSimulation))
        {
            FastForwardChoiceResult(simulator, predictedCard, choiceSimulation);
        }

        var shouldPredict = ModData.Settings.FairModeEnabled
            ? predictedCard.GetPile(simulator.State) is { Type: PileType.Hand } &&
              simulator.CanPlay(predictedCard)
            : !predictedCard.GetKeywords(simulator).Contains(CardKeyword.Unplayable);
        if (!shouldPredict)
        {
            return null;
        }

        simulator.ManualPlay(predictedCard, target, out var frame);
        return CombatPredictionProjector.Project(simulator.History, frame);
    }

    private static bool ShouldShowCombatPlayPrediction(CardModel card)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.CardPlayPredictionEnabled)
        {
            return false;
        }

        if (TryGetChoiceSimulationPlan(card, out _))
        {
            // Cards registered from supported FromChooseACardScreen callers are generated cards with
            // an owner, but they are not in any combat pile yet. Treat them like cards that will enter hand
            // after the player chooses them, so they can show the same play predictions as hand cards.
            return true;
        }

        if (card.Pile is not { Type: PileType.Hand })
        {
            // Only show combat card-play predictions for cards in the player's hand.
            return false;
        }

        if (NPlayerHand.Instance is { } hand && hand.GetCardHolder(card) is { } localHolder)
        {
            // For the local hand UI, only show play predictions in normal play mode, not selection modes.
            return hand.CurrentMode == NPlayerHand.Mode.Play && localHolder is NHandCardHolder;
        }

        // If no local holder exists, fall back to allowing prediction. This preserves existing
        // behavior for non-local or integration-provided hand card views.
        return true;
    }

    private static bool TryGetChoiceSimulationPlan(CardModel card, out ChoiceSimulationPlan plan)
    {
        plan = default;
        if (!ChooseACardPredictionContext.TryGet(card, out var source) ||
            !ChoiceSourceBehaviors.TryGetValue(source.GetType(), out var behavior))
        {
            return false;
        }

        var creator = source switch
        {
            CardModel sourceCard => sourceCard.Owner,
            PotionModel sourcePotion => sourcePotion.Owner,
            RelicModel sourceRelic => sourceRelic.Owner,
            _ => null
        };
        if (creator is null)
        {
            return false;
        }

        plan = new ChoiceSimulationPlan(creator, behavior);
        return true;
    }

    private static void FastForwardChoiceResult(
        CombatPredictionSimulator simulator,
        PredictedCard selectedCard,
        ChoiceSimulationPlan plan)
    {
        // Mirrors the post-selection branches of the supported CardModel.OnPlay, PotionModel.OnUse, and
        // Toolbox.BeforeHandDraw callers. Vanilla mutates the live selected card and adds it to live combat piles;
        // the prediction applies those effects only to its detached card and shadow piles.
        if (plan.Behavior.SetsCardToFreeThisTurn)
        {
            selectedCard.SetToFreeThisTurn();
        }

        simulator.AddGeneratedCardToCombat(
            selectedCard,
            PileType.Hand,
            plan.Creator,
            resultKind: CardGenerationResultKind.Fixed);
    }

    private readonly record struct ChoiceSimulationPlan(Player Creator, ChoiceSourceBehavior Behavior);

    private readonly record struct ChoiceSourceBehavior(bool SetsCardToFreeThisTurn = false);
}
