using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat.Mirrors.Hooks;

// Shadow amount shared by hook mirrors that consume an existing live power without mutating it.
internal sealed class PowerAmountPredictionState(int amount)
{
    public int Amount { get; set; } = amount;

    public bool IsActive => Amount > 0;

    public void Decrement()
    {
        Decrease(1);
    }

    public void Decrease(int amount)
    {
        Amount = Math.Max(0, Amount - amount);
    }

    public void Consume()
    {
        Amount = 0;
    }
}

internal static class PredictionStateStorePowerAmountExtensions
{
    extension(PredictionStateStore store)
    {
        public PowerAmountPredictionState GetPowerAmount(PowerModel power)
        {
            return store.Get(power, () => new PowerAmountPredictionState(power.Amount));
        }
    }
}
