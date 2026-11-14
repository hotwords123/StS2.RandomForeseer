using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.Common;

namespace RandomForeseer.InCombat.Mirrors.Hooks;

internal sealed class FlagPredictionState(bool value)
{
    public bool Value { get; set; } = value;
}

internal sealed class CounterPredictionState(int value)
{
    public int Value { get; set; } = value;
}

// Shadow amount shared by hook mirrors that consume an existing live power without mutating it.
internal sealed class PowerAmountPredictionState(int amount)
{
    public int Amount { get; set; } = amount;

    public bool IsActive => Amount != 0;

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

internal sealed class CardPlayAmountPredictionState
{
    public Dictionary<CardPlay, int> Amounts { get; } = new(ReferenceEqualityComparer.Instance);
}

internal static class PredictionStateStoreExtensions
{
    extension(PredictionStateStore store)
    {
        public PowerAmountPredictionState GetPowerAmount(PowerModel power)
        {
            return store.Get(power, () => new PowerAmountPredictionState(power.Amount));
        }

        public void RecordCardPlayAmount(AbstractModel model, CardPlay cardPlay, int amount)
        {
            store.Get<CardPlayAmountPredictionState>(model).Amounts.Add(cardPlay, amount);
        }

        public bool TryTakeCardPlayAmount(AbstractModel model, CardPlay cardPlay, out int amount)
        {
            return store.Get<CardPlayAmountPredictionState>(model).Amounts.Remove(cardPlay, out amount);
        }
    }
}
