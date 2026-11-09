using System.Diagnostics;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.Common;
using RandomForeseer.InCombat.Extensions;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

/// <summary>
/// Contains the generated card snapshots shared by combat simulation and out-of-combat potion previews.
/// </summary>
/// <param name="Cards">Generated prediction-owned cards in vanilla RNG order.</param>
/// <param name="AddsToHand">
/// Whether the potion adds every generated card to the combat hand instead of presenting player-choice options.
/// </param>
internal sealed record PotionCardGenerationResult(
    IReadOnlyList<PredictedCard> Cards,
    bool AddsToHand);

internal static class CardGenerationPotionMirrors
{
    public static void OnUse(PotionModel potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        var result = Generate(potion, player, context.Rng.CombatCardGeneration)
            ?? throw new UnreachableException($"No card generation policy for registered potion {potion.Id}.");

        if (result.Cards.Count == 0)
        {
            return;
        }

        if (result.AddsToHand)
        {
            context.Simulator.AddGeneratedCardsToCombat(result.Cards, PileType.Hand, potion.Owner);
        }
        else
        {
            context.History.CardGenerationOptions(result.Cards);
            // The options are deterministic, but the selected card and its addition to hand remain unresolved.
            context.History.RecordRisk(PredictionRiskReason.UnresolvedPlayerChoice);
        }
    }

    /// <summary>
    /// Generates prediction-owned cards without advancing real RNG or applying combat piles and hooks.
    /// </summary>
    /// <param name="potion">The supported mutable potion whose runtime behavior determines the result shape.</param>
    /// <param name="target">The player whose unlocks, card pool constraints and ownership apply.</param>
    /// <param name="rng">A prediction-owned clone of the target run's combat-card-generation RNG.</param>
    /// <returns>The generated result, or <see langword="null"/> for an unsupported potion type.</returns>
    public static PotionCardGenerationResult? Generate(PotionModel potion, Player target, Rng rng)
    {
        return potion switch
        {
            AttackPotion => new(GenerateCharacterCards(target, CardType.Attack, 3, rng), AddsToHand: false),
            SkillPotion => new(GenerateCharacterCards(target, CardType.Skill, 3, rng), AddsToHand: false),
            PowerPotion => new(GenerateCharacterCards(target, CardType.Power, 3, rng), AddsToHand: false),
            ColorlessPotion => new(GenerateColorlessCards(target, 3, rng), AddsToHand: false),
            CosmicConcoction => new(
                [.. GenerateColorlessCards(target, potion.DynamicVars.Cards.IntValue, rng)
                    .Select(static card => card.Upgrade())],
                AddsToHand: true),
            OrobicAcid => new(GenerateOrobicAcidCards(target, rng), AddsToHand: true),
            _ => null
        };
    }

    private static List<PredictedCard> GenerateCharacterCards(
        Player player,
        CardType type,
        int count,
        Rng rng)
    {
        return [.. player.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == type)
            .GetDistinctForCombat(player, count, rng)];
    }

    private static List<PredictedCard> GenerateColorlessCards(Player player, int count, Rng rng)
    {
        return [.. player.GetUnlockedColorlessCards().GetDistinctForCombat(player, count, rng)];
    }

    private static List<PredictedCard> GenerateOrobicAcidCards(Player player, Rng rng)
    {
        List<PredictedCard> cards = [];
        CardType[] types = [CardType.Attack, CardType.Skill, CardType.Power];

        foreach (var type in types)
        {
            cards.AddRange(player.GetUnlockedCharacterCards()
                .Where(candidate => candidate.Type == type)
                .GetDistinctForCombat(player, 1, rng));
        }

        foreach (var card in cards)
        {
            card.SetToFreeThisTurn();
        }

        return cards;
    }
}
