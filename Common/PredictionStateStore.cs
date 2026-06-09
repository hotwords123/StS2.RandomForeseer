using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common;

internal sealed class PredictionStateStore
{
    private readonly Dictionary<(AbstractModel Model, Type StateType), object> _states = [];

    public TState Get<TState>(AbstractModel model)
        where TState : new()
    {
        return Get(model, static () => new TState());
    }

    public TState Get<TState>(AbstractModel model, Func<TState> create)
    {
        var key = (model, typeof(TState));
        if (!_states.TryGetValue(key, out var state))
        {
            state = create() ?? throw new InvalidOperationException("Prediction state factory returned null.");
            _states[key] = state;
        }

        return (TState)state;
    }
}
