using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.Card;

using Registry = ModelMethodMirrorRegistry<AbstractModel, ModifyShuffleOrderMirrorContext>;

internal static class ModifyShuffleOrderMirrors
{
    private static readonly MirrorMethodSpec ModifyShuffleOrder = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyShuffleOrder),
        [typeof(Player), typeof(List<CardModel>), typeof(bool)]);

    private static readonly Registry Registry = CreateRegistry();

    public static void Invoke(AbstractModel listener, ModifyShuffleOrderMirrorContext context)
    {
        Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ModifyShuffleOrder);
        registry.Register<PerfectFit>(HandlePerfectFit);
        return registry;
    }

    private static void HandlePerfectFit(PerfectFit enchantment, ModifyShuffleOrderMirrorContext context)
    {
        if (!context.IsInitialShuffle &&
            context.Cards.FirstOrDefault(card => card.References(enchantment.Card)) is { } card)
        {
            context.Cards.Remove(card);
            context.Cards.Insert(0, card);
        }
    }
}

internal sealed class ModifyShuffleOrderMirrorContext : CombatPredictionMirrorContext
{
    public required Player Player { get; init; }

    public required List<PredictedCard> Cards { get; init; }

    public required bool IsInitialShuffle { get; init; }
}
