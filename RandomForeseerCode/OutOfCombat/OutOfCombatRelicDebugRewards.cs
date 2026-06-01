using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer;

internal static class OutOfCombatRelicDebugRewards
{
    public static void OfferPredictedNonAncientRelics()
    {
        TaskHelper.RunSafely(OfferPredictedNonAncientRelicsAsync());
    }

    private static async Task OfferPredictedNonAncientRelicsAsync()
    {
        var state = RunManager.Instance.DebugOnlyGetState();
        var player = LocalContext.GetMe(state);
        if (player == null)
        {
            Entry.Logger.Warn("Cannot offer predicted non-Ancient relic debug rewards: no active local player.");
            return;
        }

        var rewards = CreateRelics()
            .Select(relic => new RelicReward(relic, player))
            .Cast<Reward>()
            .ToList();

        await RewardsCmd.OfferCustom(player, rewards);
    }

    private static IEnumerable<RelicModel> CreateRelics()
    {
        yield return ModelDb.Relic<Cauldron>().ToMutable();
        yield return ModelDb.Relic<FragrantMushroom>().ToMutable();
        yield return ModelDb.Relic<Orrery>().ToMutable();
        yield return ModelDb.Relic<WarPaint>().ToMutable();
        yield return ModelDb.Relic<Whetstone>().ToMutable();
    }
}
