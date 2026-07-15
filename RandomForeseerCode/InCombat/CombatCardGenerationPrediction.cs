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

    public static IReadOnlyList<IHoverTip> GetPotionHoverTips(PotionModel potion)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnablePotionCardPrediction) ||
            !ShouldShowPotionCardPrediction(potion))
        {
            return [];
        }

        return PredictionHoverTips.Cards(PredictPotionCards(potion));
    }

    private static bool ShouldShowPotionCardPrediction(PotionModel potion)
    {
        return RandomForeseerSettings.IsFairPredictionAllowed(PredictionFairness.UnfairInAllModes) ||
            CombatManager.Instance.IsInProgress && !potion.Owner.Creature.IsDead;
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

        var history = simulator.History
            .OfType<CombatPredictionCardGenerationEntry>()
            .Where(entry => ReferenceEquals(entry.SourceModel, card))
            .ToList();
        var cardBundles = history
            .Select(entry => entry.Cards)
            .ToList();

        return new(cardBundles, simulator.History.GetRisk(history));
    }

    private static bool IsSupported(CardModel card)
    {
        return card is
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

    private static IReadOnlyList<CardModel> PredictPotionCards(PotionModel potion)
    {
        var owner = potion.Owner;
        var previewRng = owner.RunState.Rng.CombatCardGeneration.Clone();

        return potion switch
        {
            AttackPotion => PredictCharacterCards(owner, CardType.Attack, 3, previewRng),
            SkillPotion => PredictCharacterCards(owner, CardType.Skill, 3, previewRng),
            PowerPotion => PredictCharacterCards(owner, CardType.Power, 3, previewRng),
            ColorlessPotion => PredictColorlessCards(owner, 3, previewRng),
            CosmicConcoction => PredictColorlessCards(owner, potion.DynamicVars.Cards.IntValue, previewRng)
                .Select(PredictionUtils.ToUpgradedCard)
                .ToList(),
            OrobicAcid => new[] { CardType.Attack, CardType.Skill, CardType.Power }
                .SelectMany(type => PredictCharacterCards(owner, type, 1, previewRng))
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
