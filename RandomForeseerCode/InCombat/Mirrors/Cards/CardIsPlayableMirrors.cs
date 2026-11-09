using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using RandomForeseer.Common;
using RandomForeseer.Common.Mirrors;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Cards;

using Registry = MethodMirrorRegistry<CardModel, CardIsPlayableMirrorContext, bool>;

// Mirrors CardModel.IsPlayable while replacing vanilla overrides that read live combat state.
internal static class CardIsPlayableMirrors
{
    private delegate bool IsPlayableDelegate(CardModel card);

    private static readonly MethodInfo IsPlayableGetterMethod =
        AccessTools.PropertyGetter(typeof(CardModel), "IsPlayable")
        ?? throw new UnreachableException("Could not find CardModel.IsPlayable getter.");

    private static readonly IsPlayableDelegate OriginalIsPlayableGetter =
        (IsPlayableDelegate)Delegate.CreateDelegate(typeof(IsPlayableDelegate), IsPlayableGetterMethod);

    private static readonly MirrorMethodSpec IsPlayable = new(
        typeof(CardModel),
        IsPlayableGetterMethod.Name,
        BindingFlags.Instance | BindingFlags.NonPublic,
        []);

    private static readonly Registry Registry = CreateRegistry();

    public static bool Invoke(CombatPredictionSimulator simulator, PredictedCard card)
    {
        var context = new CardIsPlayableMirrorContext
        {
            Simulator = simulator,
            Card = card
        };

        return Registry.TryInvokeRegistered(card.Preview, context, out var result)
            ? result.Value
            : OriginalIsPlayableGetter(card.Preview);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(IsPlayable);

        registry.Register<Clash>(HandleClash);
        registry.Register<GrandFinale>(HandleGrandFinale);
        registry.Register<HighFive>(HandleHighFive);

        return registry;
    }

    private static bool HandleClash(Clash card, CardIsPlayableMirrorContext context)
    {
        return context.State.GetPlayerCombatState(card.Owner).Hand.Cards
            .All(handCard => handCard.Preview.Type == CardType.Attack);
    }

    private static bool HandleGrandFinale(GrandFinale card, CardIsPlayableMirrorContext context)
    {
        return context.State.GetPlayerCombatState(card.Owner).DrawPile.IsEmpty;
    }

    private static bool HandleHighFive(HighFive card, CardIsPlayableMirrorContext context)
    {
        return card.Owner.Osty is { } osty && context.State.GetCreature(osty).IsAlive;
    }
}

internal sealed class CardIsPlayableMirrorContext : CombatCardMirrorContext<CardModel>
{
    protected override AbstractModel GetDispatchSource(CardModel _) => OriginalCard;
}
