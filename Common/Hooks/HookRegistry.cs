using System.Reflection;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.Common.Hooks;

internal delegate HookResultKind HookHandler<in TContext>(AbstractModel model, TContext context);

internal sealed record HookSpec(string Name, Type[] ParameterTypes);

// Shared per-hook dispatcher. Each concrete hook owns one or more registries with a typed context;
// this class only handles exact model-type matching, optional original calls, and unsupported detection.
internal sealed class HookRegistry<TContext>(HookSpec hook, string predictionName)
{
    private readonly Dictionary<Type, HookHandler<TContext>> _handlers = [];
    private readonly HashSet<Type> _originalTypes = [];
    private readonly HashSet<Type> _warnedUnsupportedTypes = [];

    public void Register<TModel>(Func<TModel, TContext, HookResultKind> handler)
        where TModel : AbstractModel
    {
        // Exact type matching is intentional: a derived model may have different side effects and
        // should be reviewed before it is treated as covered by a base model handler.
        _handlers[typeof(TModel)] = (model, context) => handler((TModel)model, context);
    }

    public void RegisterOriginal<TModel>()
        where TModel : AbstractModel
    {
        // Use only for hooks whose original implementation has been audited as safe to call during
        // prediction.
        _originalTypes.Add(typeof(TModel));
    }

    public IReadOnlyList<HookResult> Run(
        IEnumerable<AbstractModel> models,
        TContext context,
        Func<AbstractModel, TContext, HookResultKind>? callOriginal = null)
    {
        var results = new List<HookResult>();

        foreach (var model in models)
        {
            var type = model.GetType();
            if (_handlers.TryGetValue(type, out var handler))
            {
                results.Add(new HookResult(handler(model, context), model));
                continue;
            }

            if (_originalTypes.Contains(type) && callOriginal != null)
            {
                results.Add(new HookResult(callOriginal(model, context), model));
                continue;
            }

            if (Overrides(type))
            {
                WarnUnsupported(type);
                results.Add(new HookResult(HookResultKind.Unsupported, model));
            }
        }

        return results;
    }

    // Publicizer excludes virtual members, so reflection remains the safest way to distinguish
    // inherited no-op hooks from model-specific overrides.
    private bool Overrides(Type modelType)
    {
        var method = modelType.GetMethod(
            hook.Name,
            BindingFlags.Instance | BindingFlags.Public,
            hook.ParameterTypes);
        return method?.DeclaringType != typeof(AbstractModel);
    }

    private void WarnUnsupported(Type modelType)
    {
        // Log each unsupported model type once per registry so recurring hover previews do not flood the log.
        if (!_warnedUnsupportedTypes.Add(modelType))
        {
            return;
        }

        Entry.Logger.Warn(
            $"{predictionName} does not safely mirror {modelType.FullName}.{hook.Name}; preview may omit that modifier.");
    }
}
