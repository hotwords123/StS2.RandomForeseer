using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.Common.HoverTips;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.OutOfCombat.Mirrors;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat;

internal static class MerchantRestockPrediction
{
    public static IReadOnlyList<IHoverTip> GetHoverTips(MerchantEntry entry)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.MerchantRestockPredictionEnabled ||
            entry is not { IsStocked: true, EnoughGold: true } ||
            entry._player is not { RunState.CurrentRoom: MerchantRoom room } player ||
            room.Inventories.Find(inventory => inventory.Player == player) is not { } inventory ||
            !HookMirrors.ShouldRefillMerchantEntry(player.RunState, entry, player))
        {
            return [];
        }

        var context = new RunPredictionContext(player);
        return entry switch
        {
            MerchantCardEntry cardEntry => PredictCard(context, cardEntry, inventory),
            MerchantPotionEntry potionEntry => PredictPotion(context, potionEntry, inventory),
            MerchantRelicEntry relicEntry => PredictRelic(context, relicEntry, inventory),
            _ => []
        };
    }

    private static IReadOnlyList<IHoverTip> PredictCard(
        RunPredictionContext context,
        MerchantCardEntry entry,
        MerchantInventory inventory)
    {
        var player = context.Player;
        var blacklist = inventory.CardEntries
            .Select(other => other.CreationResult?.Card.CanonicalInstance)
            .OfType<CardModel>()
            .ToHashSet();

        var options = entry._cardPool.Except(blacklist);
        options = HookMirrors.ModifyMerchantCardPool(player.RunState, player, options);
        options = options.Where(card => card.Rarity != CardRarity.Basic);
        options = CardFactory.FilterForPlayerCount(player.RunState, options);

        var filteredOptions = options.ToArray();

        IEnumerable<CardModel> candidates;
        if (entry._cardType is { } cardType)
        {
            var cardRarity = context.CardRarityOdds.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop);
            cardRarity = HookMirrors.ModifyMerchantCardRarity(player.RunState, player, cardRarity);
            cardRarity = CardFactory.GetNextAllowedRarity(
                cardRarity,
                rarity => filteredOptions.Any(card => card.Rarity == rarity && card.Type == cardType));
            if (cardRarity == CardRarity.None)
            {
                throw new InvalidOperationException(
                    $"Could not predict a merchant card rarity for type {cardType}.");
            }

            candidates = filteredOptions.Where(card => card.Rarity == cardRarity && card.Type == cardType);
        }
        else if (entry._cardRarity is { } cardRarity)
        {
            cardRarity = HookMirrors.ModifyMerchantCardRarity(player.RunState, player, cardRarity);
            candidates = filteredOptions.Where(card => card.Rarity == cardRarity);
        }
        else
        {
            throw new InvalidOperationException("Merchant card entry has neither a card type nor a card rarity.");
        }

        var canonicalCard = context.Rng.Shops.NextItem(candidates)
            ?? throw new InvalidOperationException("Could not predict a merchant restock card.");
        var result = new CardCreationResult(PredictionUtils.CreateCard(canonicalCard, player));

        // Mirrors CardFactory.CreateForMerchant. Vanilla always consumes this Rewards roll even though its
        // -999999999 base chance normally prevents a merchant card from being upgraded by this roll.
        CardRewardPrediction.RollForUpgrade(player, result.Card, -999999999m, context.Rng.Rewards);
        HookMirrors.ModifyMerchantCardCreationResults(context, [result]);

        var cost = MerchantCardEntry.GetCost(result.Card);
        cost = Mathf.RoundToInt(cost * context.Rng.Shops.NextFloat(0.95f, 1.05f));
        cost = (int)HookMirrors.ModifyMerchantPrice(context.RunState, player, entry, cost);

        return
        [
            CreateTextTip(result.Card.Title, cost),
            PredictionHoverTipFactory.Card(result.Card)
        ];
    }

    private static IReadOnlyList<IHoverTip> PredictPotion(
        RunPredictionContext context,
        MerchantPotionEntry entry,
        MerchantInventory inventory)
    {
        _ = inventory;

        // Mirrors MerchantPotionEntry.RestockAfterPurchase. Vanilla computes a current-inventory blacklist but
        // accidentally passes an empty blacklist to FillSlot, so duplicate potions remain possible.
        var potion = PotionFactory.CreateRandomPotionOutOfCombat(context.Player, context.Rng.Shops, []);

        var cost = MerchantPotionEntry.GetCost(potion.Rarity);
        cost = (int)Mathf.Round(cost * context.Rng.Shops.NextFloat(0.95f, 1.05f));
        cost = (int)HookMirrors.ModifyMerchantPrice(context.RunState, context.Player, entry, cost);

        return
        [
            CreateTextTip(potion.Title.GetFormattedText(), cost),
            PredictionHoverTipFactory.Potion(potion)
        ];
    }

    private static IReadOnlyList<IHoverTip> PredictRelic(
        RunPredictionContext context,
        MerchantRelicEntry entry,
        MerchantInventory inventory)
    {
        var player = context.Player;
        var obtainedRelic = PredictionUtils.CreateRelic(entry.Model!, player);
        RelicPickupPrediction.FastForwardRelicPickup(context, obtainedRelic);

        var rarity = RelicFactory.RollRarity(context.Rng.Rewards);
        var blacklist = inventory.RelicEntries
            .Select(other => other.Model?.CanonicalInstance)
            .OfType<RelicModel>()
            .ToHashSet();

        var relic = context.RelicGrabBag.PullFromBack(
            rarity,
            relic => !blacklist.Contains(relic) && relic.IsAllowedInShops,
            context.RunState) ?? RelicFactory.FallbackRelic;

        var cost = (int)Math.Round(relic.MerchantCost * context.Rng.Shops.NextFloat(0.85f, 1.15f));
        cost = (int)HookMirrors.ModifyMerchantPrice(context.RunState, player, entry, cost);
        cost = (int)obtainedRelic.ModifyMerchantPrice(player, entry, cost);

        return
        [
            CreateTextTip(relic.Title.GetFormattedText(), cost),
            PredictionHoverTipFactory.Relic(relic)
        ];
    }

    private static HoverTip CreateTextTip(string modelName, int cost)
    {
        return PredictionHoverTipFactory.Text("merchant_restock", description =>
        {
            description.Add("Model", modelName);
            description.Add("Cost", cost);
        });
    }
}
