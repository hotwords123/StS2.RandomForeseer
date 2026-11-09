using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using RandomForeseer.Common.Mirrors;

namespace RandomForeseer.OutOfCombat.Mirrors.Hooks.CardCreation;

using Registry = MethodMirrorRegistry<AbstractModel, ModifyMerchantCardCreationResultsMirrorContext>;

internal static class ModifyMerchantCardCreationResultsMirrors
{
    private static readonly MirrorMethodSpec ModifyMerchantCardCreationResults = MirrorMethodSpec.Hook(
        nameof(AbstractModel.ModifyMerchantCardCreationResults),
        [typeof(Player), typeof(List<CardCreationResult>)]);

    private static readonly Registry Registry = CreateRegistry();

    public static MirrorDispatchResult Invoke(
        AbstractModel listener,
        ModifyMerchantCardCreationResultsMirrorContext context)
    {
        return Registry.Invoke(listener, context);
    }

    private static Registry CreateRegistry()
    {
        var registry = new Registry(ModifyMerchantCardCreationResults);

        registry.Register<FrozenEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Power));
        registry.Register<MoltenEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Attack));
        registry.Register<ToxicEgg>((relic, context) => HandleEggRelic(relic, context, CardType.Skill));
        registry.Register<FresnelLens>(HandleFresnelLens);

        return registry;
    }

    private static void HandleEggRelic(
        RelicModel relic,
        ModifyMerchantCardCreationResultsMirrorContext context,
        CardType cardType)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        CardCreationResultUtils.UpgradeCardsOfType(context.Results, relic, cardType);
    }

    private static void HandleFresnelLens(
        FresnelLens relic,
        ModifyMerchantCardCreationResultsMirrorContext context)
    {
        if (relic.Owner != context.Player)
        {
            return;
        }

        var amount = relic.DynamicVars[FresnelLens._nimbleAmountKey].BaseValue;
        CardCreationResultUtils.EnchantValidCards<Nimble>(context.Results, relic, amount);
    }
}

internal sealed class ModifyMerchantCardCreationResultsMirrorContext : CardCreationMirrorContext;
