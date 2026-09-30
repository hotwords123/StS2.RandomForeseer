using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;
using RandomForeseer.RandomForeseerCode.InCombat.Simulation;

namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks.TurnEnd;

using Registry = MethodMirrorRegistry<AbstractModel, BeforeFlushMirrorContext>;

/// <summary>
/// Mirrors <see cref="AbstractModel.BeforeFlush"/> and <see cref="AbstractModel.BeforeFlushLate"/>.
/// </summary>
internal static class BeforeFlushMirrors
{
    private static readonly MirrorMethodSpec BeforeFlush = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeFlush),
        [typeof(PlayerChoiceContext), typeof(Player)]);

    private static readonly MirrorMethodSpec BeforeFlushLate = MirrorMethodSpec.Hook(
        nameof(AbstractModel.BeforeFlushLate),
        [typeof(PlayerChoiceContext), typeof(Player)]);

    private static readonly Registry BeforeFlushRegistry = CreateRegistry();
    private static readonly Registry BeforeFlushLateRegistry = new(BeforeFlushLate);

    public static void Invoke(AbstractModel listener, BeforeFlushMirrorContext context)
    {
        BeforeFlushRegistry.Invoke(listener, context);
    }

    public static void InvokeLate(AbstractModel listener, BeforeFlushMirrorContext context)
    {
        BeforeFlushLateRegistry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(BeforeFlush);

        registry.Register<SlumberingEssence>(HandleSlumberingEssence);

        return registry;
    }

    private static void HandleSlumberingEssence(SlumberingEssence enchantment, BeforeFlushMirrorContext context)
    {
        if (context.Player != enchantment.Card.Owner ||
            context.State.FindCard(enchantment.Card) is not { } card ||
            card.GetPile(context.State) is not { Type: PileType.Hand })
        {
            return;
        }

        card.MutablePreview.EnergyCost.AddUntilPlayed(-1);
    }
}

internal sealed class BeforeFlushMirrorContext : CombatMirrorContext
{
    public required Player Player { get; init; }
}
