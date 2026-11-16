using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace RandomForeseer.OutOfCombat;

internal static class MerchantPredictionUtils
{
    public static bool TryGetCurrentInventory(
        MerchantEntry entry,
        [NotNullWhen(true)] out MerchantInventory? inventory)
    {
        var player = entry.Player;
        var runManager = RunManager.Instance;
        if (!ReferenceEquals(player.RunState, runManager.State) || player.RunState.CurrentRoom is not MerchantRoom)
        {
            inventory = null;
            return false;
        }

        inventory = runManager.MerchantSynchronizer.GetInventoryFor(player);
        return true;
    }
}
