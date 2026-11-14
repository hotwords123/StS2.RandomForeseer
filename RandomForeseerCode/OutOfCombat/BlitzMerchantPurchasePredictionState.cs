using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using RandomForeseer.Utils;

namespace RandomForeseer.OutOfCombat;

/// <summary>Tracks local execution of each player's Blitz purchase, including remote choices awaited by the game.</summary>
/// <remarks>Accessed on the game thread; awaited purchases retain its synchronization context.</remarks>
internal static class BlitzMerchantPurchasePredictionState
{
    private static readonly ConditionalWeakTable<Player, PendingPurchases> Players = [];

    public static event Action<Player>? Changed;

    public static bool IsPending(Player player)
    {
        return Players.TryGetValue(player, out var state) && state.Count > 0;
    }

    public static IDisposable Enter(Player player)
    {
        var state = Players.GetOrCreateValue(player);
        if (++state.Count == 1)
        {
            Changed?.Invoke(player);
        }

        return new DisposableAction(() =>
        {
            if (--state.Count == 0)
            {
                Changed?.Invoke(player);
            }
        });
    }

    private sealed class PendingPurchases
    {
        public int Count;
    }
}
