using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace RandomForeseer.Debug;

internal static class RelicPickupDebugRewards
{
    private static readonly RelicRarity[] TreasureTestRarities =
    [
        RelicRarity.Common,
        RelicRarity.Uncommon,
        RelicRarity.Rare,
        RelicRarity.Shop
    ];

    public static void OfferPredictedNonAncientRelics()
    {
        TaskHelper.RunSafely(OfferPredictedNonAncientRelicsAsync());
    }

    public static void OpenPredictedTreasureRoom()
    {
        TaskHelper.RunSafely(OpenPredictedTreasureRoomAsync());
    }

    private static async Task OfferPredictedNonAncientRelicsAsync()
    {
        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null)
        {
            Entry.Logger.Warn("Cannot offer predicted non-Ancient relic debug rewards: no active run.");
            return;
        }

        var player = LocalContext.GetMe(state);
        if (player == null)
        {
            Entry.Logger.Warn("Cannot offer predicted non-Ancient relic debug rewards: no active local player.");
            return;
        }

        var rewards = CreateRelics()
            .Select(relic => new RelicReward(relic.ToMutable(), player))
            .Cast<Reward>()
            .ToList();

        await RewardsCmd.OfferCustom(player, rewards);
    }

    private static async Task OpenPredictedTreasureRoomAsync()
    {
        var state = RunManager.Instance.DebugOnlyGetState();
        if (state == null)
        {
            Entry.Logger.Warn("Cannot open predicted treasure room debug test: no active run.");
            return;
        }

        var player = LocalContext.GetMe(state);
        if (player == null)
        {
            Entry.Logger.Warn("Cannot open predicted treasure room debug test: no active local player.");
            return;
        }

        var target = Rng.Chaotic.NextItem(CreateTreasureTestRelics()
            .Where(relic => relic.IsAllowed(state))
            .ToList());
        if (target == null)
        {
            Entry.Logger.Warn("Cannot open predicted treasure room debug test: no War Paint or Whetstone target is currently allowed.");
            return;
        }

        var grabBag = new SerializableRelicGrabBag();
        foreach (var rarity in TreasureTestRarities)
        {
            grabBag.RelicIdLists[rarity] = [target.Id];
        }

        state.SharedRelicGrabBag.LoadFromSerializable(grabBag);
        Entry.Logger.Info($"Opening debug treasure room with predicted relic target {target.Id}.");
        await RunManager.Instance.EnterRoomDebug(RoomType.Treasure, MapPointType.Treasure, showTransition: false);
    }

    private static IEnumerable<RelicModel> CreateRelics()
    {
        yield return ModelDb.Relic<Cauldron>();
        yield return ModelDb.Relic<FragrantMushroom>();
        yield return ModelDb.Relic<Orrery>();
        yield return ModelDb.Relic<WarPaint>();
        yield return ModelDb.Relic<Whetstone>();
    }

    private static IEnumerable<RelicModel> CreateTreasureTestRelics()
    {
        yield return ModelDb.Relic<WarPaint>();
        yield return ModelDb.Relic<Whetstone>();
    }
}
