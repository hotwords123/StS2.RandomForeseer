using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Extensions;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

internal static class CardSelectionCardMirrors
{
    public static void AnointedOnPlay(Anointed card, CardOnPlayMirrorContext context)
    {
        var count = CardPile.MaxCardsInHand - context.OwnerState.Hand.Cards.Count;
        if (count <= 0)
        {
            return;
        }

        var cardsToAdd = context.OwnerState.DrawPile.Cards
            .Where(predictedCard => predictedCard.Preview.Rarity is CardRarity.Rare)
            .TakeRandom(count, context.Rng.CombatCardSelection)
            .ToList();
        if (cardsToAdd.Count == 0)
        {
            return;
        }

        context.Simulator.History.CardsSelected(cardsToAdd, context.OriginalCard);
        context.Simulator.AddToPile(cardsToAdd, PileType.Hand);
    }

    public static void BeatDownOnPlay(BeatDown card, CardOnPlayMirrorContext context)
    {
        var selectedCards = context.OwnerState.DiscardPile.Cards
            .Where(predictedCard =>
                predictedCard.Preview.Type == CardType.Attack &&
                !predictedCard.GetKeywords(context.State).Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(context.Rng.Shuffle)
            .Take(card.DynamicVars.Cards.IntValue)
            .ToList();
        if (selectedCards.Count == 0)
        {
            return;
        }

        context.Simulator.History.CardsSelected(selectedCards, context.OriginalCard);

        foreach (var selectedCard in selectedCards)
        {
            Creature? target = null;
            if (selectedCard.Preview.TargetType == TargetType.AnyEnemy)
            {
                // BeatDown.OnPlay resolves this target before CardCmd.AutoPlay checks whether the
                // selected card can play, so preserve that CombatTargets RNG consumption order.
                target = context.Rng.CombatTargets.NextItem(context.State.HittableEnemies);
            }

            context.Simulator.AutoPlay(selectedCard, target);
        }
    }

    public static void CatastropheOnPlay(Catastrophe card, CardOnPlayMirrorContext context)
    {
        for (var i = 0; i < card.DynamicVars.Cards.IntValue; i++)
        {
            var drawPileCards = context.OwnerState.DrawPile.Cards;
            var selectedCard = drawPileCards
                .Where(predictedCard =>
                    !predictedCard.GetKeywords(context.State).Contains(CardKeyword.Unplayable))
                .ToList()
                .StableShuffle(context.Rng.Shuffle)
                .FirstOrDefault();

            selectedCard ??= drawPileCards
                .ToList()
                .StableShuffle(context.Rng.Shuffle)
                .FirstOrDefault();

            if (selectedCard is null)
            {
                break;
            }

            context.Simulator.History.CardsSelected([selectedCard], context.OriginalCard);
            context.Simulator.AutoPlay(selectedCard);
        }
    }

    public static void CinderOnPlay(Cinder card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);

        if (SelectRandomHandCard(context, static _ => true) is { } selectedCard)
        {
            context.Simulator.History.CardsSelected([selectedCard], context.OriginalCard);
            context.Simulator.Exhaust(selectedCard);
        }
    }

    public static void DrainPowerOnPlay(DrainPower card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);

        var cardsToUpgrade = context.OwnerState.DiscardPile.Cards
            .Where(predictedCard => predictedCard.Preview.IsUpgradable)
            .TakeRandom(card.DynamicVars.Cards.IntValue, context.Rng.CombatCardSelection)
            .ToList();
        if (cardsToUpgrade.Count == 0)
        {
            return;
        }

        foreach (var cardToUpgrade in cardsToUpgrade)
        {
            cardToUpgrade.Upgrade();
        }

        context.Simulator.History.CardsSelected(cardsToUpgrade, context.OriginalCard);
    }

    public static void HiddenGemOnPlay(HiddenGem card, CardOnPlayMirrorContext context)
    {
        var drawPile = context.OwnerState.DrawPile;
        if (drawPile.IsEmpty)
        {
            return;
        }

        var eligibleCards = drawPile.Cards
            .Where(predictedCard =>
                !predictedCard.GetKeywords(context.State).Contains(CardKeyword.Unplayable) &&
                predictedCard.Preview.Type is not CardType.Status and not CardType.Curse &&
                predictedCard.Preview.GetEnchantedReplayCount() < 1)
            .ToList();
        var preferredCards = eligibleCards
            .Where(predictedCard =>
                predictedCard.Preview.Type is CardType.Attack or CardType.Skill or CardType.Power)
            .ToList();

        var selectedCard = context.Rng.CombatCardSelection.NextItem(
            preferredCards.Count == 0 ? eligibleCards : preferredCards);
        if (selectedCard is null)
        {
            return;
        }

        selectedCard.MutablePreview.BaseReplayCount += card.DynamicVars["Replay"].IntValue;
        context.Simulator.History.CardsSelected([selectedCard], context.OriginalCard);
    }

    public static void SeekerStrikeOnPlay(SeekerStrike card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context);

        var alreadyPredicted = context.Simulator.History
            .OfType<CombatPredictionCardSelectionOptionsEntry>()
            .Any(entry => ReferenceEquals(entry.SourceModel, context.OriginalCard));
        if (alreadyPredicted)
        {
            // The first unresolved choice changes the draw pile, so later replay options and their
            // CombatCardSelection RNG consumption cannot be predicted from the current shadow state.
            return;
        }

        var cardOptions = context.OwnerState.DrawPile.Cards
            .ToList()
            .StableShuffle(context.Rng.CombatCardSelection)
            .Take(card.DynamicVars.Cards.IntValue)
            .ToList();
        if (cardOptions.Count == 0)
        {
            return;
        }

        context.Simulator.History.CardSelectionOptions(cardOptions, context.OriginalCard);
        // Vanilla next asks the player to choose an option and moves it to hand. Record the options
        // first so that this unresolved choice does not contaminate their risk checkpoint.
        context.MarkCurrentSourceRisky();
    }

    public static void ThrashOnPlay(Thrash card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context, hitCount: 2);

        var cardToExhaust = SelectRandomHandCard(context, cardModel => cardModel.Type == CardType.Attack);
        if (cardToExhaust is null)
        {
            return;
        }

        context.Simulator.History.CardsSelected([cardToExhaust], context.OriginalCard);

        var damage = default(decimal);
        var dynamicVars = cardToExhaust.Preview.DynamicVars;
        if (dynamicVars.ContainsKey("CalculatedDamage"))
        {
            using (context.Simulator.PushSource(cardToExhaust.Original))
            {
                damage = dynamicVars.CalculatedDamage.SimulateCalculate(context.Simulator, null);
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
            context.State.CombatState.RunState,
            context.State.CombatState,
            target: null,
            dealer: cardToExhaust.Preview.Owner.Creature,
            damage,
            ValueProp.Move,
            cardSource: cardToExhaust.Preview,
            cardPlay: null,
            ModifyDamageHookType.All,
            CardPreviewMode.None,
            out var _);

        card.DynamicVars.Damage.BaseValue += damage;
        card.ExtraDamage += damage;

        context.Simulator.Exhaust(cardToExhaust);
    }

    public static void TrueGritOnPlay(TrueGrit card, CardOnPlayMirrorContext context)
    {
        context.Simulator.GainBlock(card.Owner.Creature, card.DynamicVars.Block, context.Card);

        if (card.IsUpgraded)
        {
            if (!context.OwnerState.Hand.IsEmpty)
            {
                // Vanilla asks the player which hand card to exhaust. The choice and resulting
                // pile state cannot be determined during prediction.
                context.MarkCurrentSourceRisky();
            }
            return;
        }

        if (SelectRandomHandCard(context, static _ => true) is { } selectedCard)
        {
            context.Simulator.History.CardsSelected([selectedCard], context.OriginalCard);
            context.Simulator.Exhaust(selectedCard);
        }
    }

    public static void UproarOnPlay(Uproar card, CardOnPlayMirrorContext context)
    {
        SimulateTargetedAttack(context, hitCount: 2);

        var attackCards = context.OwnerState.DrawPile.Cards
            .Where(predictedCard => predictedCard.Preview.Type == CardType.Attack)
            .ToList();

        var selectedCard = attackCards
            .Where(predictedCard => !predictedCard.GetKeywords(context.State).Contains(CardKeyword.Unplayable))
            .ToList()
            .StableShuffle(context.Rng.Shuffle)
            .FirstOrDefault();

        selectedCard ??= attackCards
            .StableShuffle(context.Rng.Shuffle)
            .FirstOrDefault();

        if (selectedCard is null)
        {
            return;
        }

        context.Simulator.History.CardsSelected([selectedCard], context.OriginalCard);
        context.Simulator.AutoPlay(selectedCard);
    }

    private static PredictedCard? SelectRandomHandCard(
        CardOnPlayMirrorContext context,
        Func<CardModel, bool> filter)
    {
        var candidates = context.OwnerState.Hand.Cards.Where(card => filter(card.Preview));
        return context.Rng.CombatCardSelection.NextItem(candidates);
    }

    private static void SimulateTargetedAttack(
        CardOnPlayMirrorContext context,
        int hitCount = 1)
    {
        context.Simulator.SimulateTargetedAttack(context.Card, context.CardPlay, hitCount);
    }
}
