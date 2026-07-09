using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed class CombatCardSelectionPrediction(
    CombatPredictionSimulator simulator,
    SimPlayerCombatState playerCombatState,
    PredictedCard source,
    CardPlay cardPlay,
    List<PredictedCard> selectedCards)
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(CardModel card)
    {
        return Predict(card, target: null).ToHoverTips();
    }

    public static CombatCardSelectionPredictionResult Predict(CardModel card, Creature? target)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(RandomForeseerSettings.EnableCombatCardSelectionPrediction) ||
            !IsSupported(card, target) ||
            !CombatPredictionSimulator.TryCreate(card.Owner, out var simulator))
        {
            return CombatCardSelectionPredictionResult.Empty;
        }

        var playerCombatState = simulator.State.GetPlayerCombatState(card.Owner);
        var predictedCard = playerCombatState.FindCard(card) ?? new PredictedCard(card);

        var selectedCards = new List<PredictedCard>();

        simulator.ManualPlay(predictedCard, target, (_, cardPlay) =>
        {
            new CombatCardSelectionPrediction(simulator, playerCombatState, predictedCard, cardPlay, selectedCards)
                .Simulate();
        });

        return new(selectedCards.Select(card => card.Preview).ToList(), simulator.Snapshot());
    }

    private static bool IsSupported(CardModel card, Creature? target)
    {
        return card switch
        {
            // Cards that can be predicted from hand hover without a selected target.
            Anointed or
            HiddenGem or
            TrueGrit { IsUpgraded: false } => true,

            // Cards that require an explicit target.
            Cinder or
            DrainPower or
            SeekerStrike or
            Thrash or
            Uproar => card.IsValidTarget(target),

            _ => false
        };
    }

    private void Simulate()
    {
        switch (source.Preview)
        {
            case Anointed:
                SimulateAnointed();
                break;
            case Cinder:
                SimulateCinder();
                break;
            case DrainPower:
                SimulateDrainPower();
                break;
            case HiddenGem:
                SimulateHiddenGem();
                break;
            case SeekerStrike:
                SimulateSeekerStrike();
                break;
            case Thrash:
                SimulateThrash();
                break;
            case TrueGrit { IsUpgraded: false }:
                SimulateTrueGrit();
                break;
            case Uproar:
                SimulateUproar();
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported card type for combat card selection prediction: {source.Preview.Id}");
        }
    }

    private PredictedCard? SelectRandomHandCard(Func<CardModel, bool> filter)
    {
        var candidates = playerCombatState.Hand.Cards.Where(card => filter(card.Preview));
        return simulator.Rng.CombatCardSelection.NextItem(candidates);
    }

    private void SimulateTargetedAttack(int hitCount = 1)
    {
        simulator.SimulateTargetedAttack(source, cardPlay, hitCount);
    }

    private void SimulateAnointed()
    {
        var cardsInHandAfterPlay = playerCombatState.Hand.Cards.Count;
        var count = CardPile.MaxCardsInHand - cardsInHandAfterPlay;
        if (count <= 0)
        {
            return;
        }

        var cardsToAdd = playerCombatState.DrawPile.Cards
            .Where(card => card.Preview.Rarity == CardRarity.Rare)
            .TakeRandom(count, simulator.Rng.CombatCardSelection)
            .ToList();

        simulator.AddToPile(cardsToAdd, PileType.Hand);
        selectedCards.AddRange(cardsToAdd);
    }

    private void SimulateCinder()
    {
        SimulateTargetedAttack();

        if (SelectRandomHandCard(_ => true) is { } card)
        {
            simulator.Exhaust(card);
            selectedCards.Add(card);
        }
    }

    private void SimulateDrainPower()
    {
        SimulateTargetedAttack();

        var cardsToUpgrade = playerCombatState.DiscardPile.Cards
            .Where(card => card.Preview.IsUpgradable)
            .TakeRandom(source.Preview.DynamicVars.Cards.IntValue, simulator.Rng.CombatCardSelection)
            .ToList();

        foreach (var card in cardsToUpgrade)
        {
            card.Upgrade();
        }

        selectedCards.AddRange(cardsToUpgrade);
    }

    private void SimulateHiddenGem()
    {
        var drawPile = playerCombatState.DrawPile;
        if (drawPile.IsEmpty)
        {
            return;
        }

        var eligibleCards = drawPile.Cards
            .Where(card =>
                !card.GetKeywords(simulator.State).Contains(CardKeyword.Unplayable) &&
                card.Preview.Type is not CardType.Status and not CardType.Curse &&
                card.Preview.GetEnchantedReplayCount() < 1)
            .ToList();
        var preferredCards = eligibleCards
            .Where(card => card.Preview.Type is CardType.Attack or CardType.Skill or CardType.Power)
            .ToList();

        var predicted = simulator.Rng.CombatCardSelection.NextItem(
            preferredCards.Count == 0 ? eligibleCards : preferredCards);
        if (predicted is not null)
        {
            predicted.MutablePreview.BaseReplayCount += source.Preview.DynamicVars["Replay"].IntValue;
            selectedCards.Add(predicted);
        }
    }

    private void SimulateSeekerStrike()
    {
        if (selectedCards.Count > 0)
        {
            // The player selects a card each time it is played, and the draw pile will change after the selection,
            // but we cannot know which card the player selected, so we can only predict once.
            return;
        }

        SimulateTargetedAttack();

        var cardOptions = playerCombatState.DrawPile.Cards
            .ToList()
            .StableShuffle(simulator.Rng.CombatCardSelection)
            .Take(source.Preview.DynamicVars.Cards.IntValue)
            .ToList();

        selectedCards.AddRange(cardOptions);
    }

    private void SimulateThrash()
    {
        SimulateTargetedAttack(hitCount: 2);

        var cardToExhaust = SelectRandomHandCard(card => card.Type == CardType.Attack);
        if (cardToExhaust is null)
        {
            return;
        }

        var damage = default(decimal);
        var dynamicVars = cardToExhaust.Preview.DynamicVars;
        if (dynamicVars.ContainsKey("CalculatedDamage"))
        {
            using (simulator.PushSource(cardToExhaust.Original))
            {
                damage = dynamicVars.CalculatedDamage.SimulateCalculate(simulator, null);
            }
        }
        else if (dynamicVars.ContainsKey("Damage"))
        {
            damage = dynamicVars.Damage.BaseValue;
        }
        else if (dynamicVars.ContainsKey("OstyDamage"))
        {
            damage = dynamicVars.OstyDamage.BaseValue;
        }
        else
        {
            Entry.Logger.Warn(
                $"Exhausted attack card {cardToExhaust.Preview.Id.Entry} did not have an appropriate DamageVar");
        }

        damage = Hook.ModifyDamage(
            simulator.State.CombatState.RunState,
            simulator.State.CombatState,
            target: null,
            dealer: cardToExhaust.Preview.Owner.Creature,
            damage,
            ValueProp.Move,
            cardToExhaust.Preview,
            cardPlay: null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out var _);

        var previewCard = (Thrash)source.MutablePreview;
        previewCard.DynamicVars.Damage.BaseValue += damage;
        previewCard.ExtraDamage += damage;

        simulator.Exhaust(cardToExhaust);
        selectedCards.Add(cardToExhaust);
    }

    private void SimulateTrueGrit()
    {
        simulator.GainBlock(source.Preview.Owner.Creature, source.Preview.DynamicVars.Block, source);

        if (SelectRandomHandCard(_ => true) is { } card)
        {
            simulator.Exhaust(card);
            selectedCards.Add(card);
        }
    }

    private void SimulateUproar()
    {
        SimulateTargetedAttack(hitCount: 2);

        var attackCards = playerCombatState.DrawPile.Cards
            .Where(card => card.Preview.Type == CardType.Attack)
            .ToList();

        var predicted = attackCards
            .Where(card => !card.GetKeywords(simulator.State).Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(simulator.Rng.Shuffle)
            .FirstOrDefault();

        predicted ??= attackCards
            .StableShuffle(simulator.Rng.Shuffle)
            .FirstOrDefault();

        if (predicted is not null)
        {
            selectedCards.Add(predicted);
            simulator.AutoPlay(predicted);
        }
    }
}

internal sealed record CombatCardSelectionPredictionResult(
    IReadOnlyList<CardModel> SelectedCards,
    PredictionRisk Risk)
{
    public static CombatCardSelectionPredictionResult Empty { get; } = new([], PredictionRisk.None);

    public bool IsEmpty => SelectedCards.Count == 0;

    public IReadOnlyList<IHoverTip> ToHoverTips()
    {
        if (IsEmpty)
        {
            return [];
        }

        var tips = PredictionHoverTips.Cards(SelectedCards).ToList();
        PredictionHoverTips.AddDriftWarningIfNeeded(tips, "card_selection", Risk);
        return tips;
    }
}
