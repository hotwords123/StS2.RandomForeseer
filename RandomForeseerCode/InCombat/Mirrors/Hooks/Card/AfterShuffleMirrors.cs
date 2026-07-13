using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, AfterShuffleMirrorContext>;

internal static class AfterShuffleMirrors
{
    private static readonly MirrorMethodSpec AfterShuffle = MirrorMethodSpec.Hook(
        nameof(AbstractModel.AfterShuffle),
        [typeof(PlayerChoiceContext), typeof(Player)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, AfterShuffleMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(AfterShuffle);

        registry.Register<BiiigHug>(HandleBiiigHug);
        registry.Register<StratagemPower>(HandleStratagemPower);
        registry.Register<TheAbacus>(HandleTheAbacus);

        return registry;
    }

    private static void HandleBiiigHug(BiiigHug relic, AfterShuffleMirrorContext context)
    {
        if (relic.Owner == context.Player)
        {
            var soot = PredictedCard.Create(ModelDb.Card<Soot>(), context.Player);
            context.Simulator.AddGeneratedCardToCombat(
                soot,
                PileType.Draw,
                context.Player,
                CardPilePosition.Random);
        }
    }

    private static void HandleStratagemPower(StratagemPower power, AfterShuffleMirrorContext context)
    {
        if (power.Owner?.Player == context.Player)
        {
            // Stratagem opens a combat-pile card selection and moves chosen cards to hand;
            // combat pile selection is not mirrored here.
            context.MarkCurrentSourceRisky();
        }
    }

    private static void HandleTheAbacus(TheAbacus relic, AfterShuffleMirrorContext context)
    {
        if (relic.Owner == context.Player)
        {
            context.Simulator.GainBlock(relic.Owner.Creature, relic.DynamicVars.Block);
        }
    }
}

internal sealed class AfterShuffleMirrorContext : CombatPredictionMirrorContext
{
    public required Player Player { get; init; }
}
