using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Potions;
using RandomForeseer.InCombat.Simulation;

namespace RandomForeseer.InCombat.Mirrors.Potions.OnUse;

internal static class DrawPotionMirrors
{
    public static void BottledPotentialOnUse(BottledPotential potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        context.Simulator.MoveHandToDrawPile(player);
        context.Simulator.Shuffle(player);
        context.Simulator.Draw(player, potion.DynamicVars.Cards.BaseValue);
    }

    public static void ClarityOnUse(Clarity potion, PotionOnUseMirrorContext context)
    {
        // Clarity applies its power after drawing, so the unsupported power state cannot affect this prediction.
        context.Simulator.Draw(context.TargetPlayer, potion.DynamicVars.Cards.BaseValue);
    }

    public static void CureAllOnUse(CureAll potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        context.Simulator.GainEnergy(player, potion.DynamicVars.Energy.BaseValue);
        context.Simulator.Draw(player, potion.DynamicVars.Cards.BaseValue);
    }

    public static void GlowwaterPotionOnUse(GlowwaterPotion potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        context.Simulator.ExhaustHand(player);
        context.Simulator.Draw(player, potion.DynamicVars.Cards.BaseValue);
    }

    public static void SneckoOilOnUse(SneckoOil potion, PotionOnUseMirrorContext context)
    {
        var player = context.TargetPlayer;
        var hand = context.State.GetPlayerCombatState(player).Hand;

        context.Simulator.Draw(player, potion.DynamicVars.Cards.BaseValue);

        foreach (var card in hand.Cards)
        {
            if (card.Preview.EnergyCost.CostsX ||
                card.Preview.EnergyCost.GetWithModifiers(CostModifiers.None) < 0)
            {
                continue;
            }

            card.MutablePreview.EnergyCost.SetThisTurnOrUntilPlayed(context.Rng.CombatEnergyCosts.NextInt(4));
        }

        context.History.CardCostsRandomized(hand.Cards);
    }

    public static void SwiftPotionOnUse(SwiftPotion potion, PotionOnUseMirrorContext context)
    {
        context.Simulator.Draw(context.TargetPlayer, potion.DynamicVars.Cards.BaseValue);
    }
}
