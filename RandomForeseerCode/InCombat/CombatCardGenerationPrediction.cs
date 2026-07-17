using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Potions;
using MegaCrit.Sts2.Core.Random;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal static class CombatCardGenerationPrediction
{
    public static IReadOnlyList<IHoverTip> GetCardHoverTips(CardModel card)
    {
        return Predict(card, target: null)?.ToHoverTips() ?? [];
    }

    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionPredictionContext context)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) ||
            !ShouldShowPotionCardPrediction(context))
        {
            return [];
        }

        return PredictionHoverTips.Cards(PredictPotionCards(context));
    }

    private static bool ShouldShowPotionCardPrediction(PotionPredictionContext context)
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes) ||
            CombatManager.Instance.IsInProgress &&
            !context.SourceOwner.Creature.IsDead &&
            !context.Target.Creature.IsDead;
    }

    public static CombatCardGenerationPredictionResult? Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardPrediction) ||
            !IsSupported(card) ||
            !card.TryResolveTarget(ref target) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return null;
        }

        var predictedCard = simulator.State.FindCard(card) ?? new PredictedCard(card);
        simulator.ManualPlay(predictedCard, target);

        List<(int Index, IReadOnlyList<PredictedCard> Cards, CombatPredictionHistoryEntry Resolved)> entries =
        [
            ..simulator.History
                .OfType<CombatPredictionCardGeneratedEntry>()
                .Where(entry => ReferenceEquals(entry.Trace?.Source, card))
                .Select(entry =>
                {
                    var resolved = simulator.History.GetResolvedEntry<CombatPredictionCardGenerationResolvedEntry>(entry);
                    return (entry.Index, new[] { resolved.Card }, resolved);
                }),
            ..simulator.History
                .OfType<CombatPredictionCardGenerationOptionsEntry>()
                .Where(entry => ReferenceEquals(entry.Trace?.Source, card))
                .Select(entry => (entry.Index, entry.Cards, entry))
        ];
        entries.Sort(static (left, right) => left.Index.CompareTo(right.Index));

        return new(
            [.. entries.Select(entry => entry.Cards)],
            simulator.History.GetRisk([.. entries.Select(entry => entry.Resolved)]));
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
            Abundance or
            BundleOfJoy or
            Discovery or
            Distraction or
            InfernalBlade or
            JackOfAllTrades or
            Jackpot or
            Largesse or
            MadScience { TinkerTimeRider: TinkerTime.RiderEffect.Chaos } or
            ManifestAuthority or
            Metamorphosis or
            Quasar or
            Splash or
            Stoke or
            WhiteNoise;
    }

    private static IReadOnlyList<CardModel> PredictPotionCards(PotionPredictionContext context)
    {
        var source = context.Source;
        var target = context.Target;
        var previewRng = target.RunState.Rng.CombatCardGeneration.Clone();

        return source switch
        {
            AttackPotion => PredictCharacterCards(target, CardType.Attack, 3, previewRng),
            SkillPotion => PredictCharacterCards(target, CardType.Skill, 3, previewRng),
            PowerPotion => PredictCharacterCards(target, CardType.Power, 3, previewRng),
            ColorlessPotion => PredictColorlessCards(target, 3, previewRng),
            CosmicConcoction => PredictColorlessCards(target, source.DynamicVars.Cards.IntValue, previewRng)
                .Select(PredictionUtils.ToUpgradedCard)
                .ToList(),
            OrobicAcid => new[] { CardType.Attack, CardType.Skill, CardType.Power }
                .SelectMany(type => PredictCharacterCards(target, type, 1, previewRng))
                .ToList(),
            _ => []
        };
    }

    private static List<CardModel> PredictCharacterCards(Player player, CardType type, int count, Rng previewRng)
    {
        return player.GetUnlockedCharacterCards()
            .Where(candidate => candidate.Type == type)
            .TakeRandomDistinctForCombat(player, count, previewRng)
            .ToList();
    }

    private static List<CardModel> PredictColorlessCards(Player player, int count, Rng previewRng)
    {
        return player.GetUnlockedColorlessCards()
            .TakeRandomDistinctForCombat(player, count, previewRng)
            .ToList();
    }
}

internal sealed record CombatCardGenerationPredictionResult(
    IReadOnlyList<IReadOnlyList<PredictedCard>> CardBundles,
    PredictionRisk Risk)
{
    public bool IsEmpty => !CardBundles.Any(bundle => bundle.Count > 0);

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        var bundles = CardBundles
            .Select(bundle => bundle.Select(card => card.Preview).ToList())
            .ToList();
        var tips = PredictionHoverTips.CardBundles(bundles).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "card_generation", Risk);
        return tips;
    }
}
