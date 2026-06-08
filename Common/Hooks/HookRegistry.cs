using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.Common.Hooks;

internal delegate HookResultKind HookHandler<in TContext>(AbstractModel model, TContext context);

internal sealed record HookSpec(string Name, Type[] ParameterTypes);

// Shared per-hook dispatcher. Each concrete hook owns one or more registries with a typed context;
// this class only handles exact model-type matching and unsupported detection.
internal sealed class HookRegistry<TContext>(HookSpec hook)
{
    private readonly Dictionary<Type, HookHandler<TContext>> _handlers = [];
    private readonly HashSet<Type> _warnedIgnoredUnsupportedTypes = [];
    private readonly HashSet<Type> _warnedUnsupportedTypes = [];

    public void Register<TModel>(Func<TModel, TContext, HookResultKind> handler)
        where TModel : AbstractModel
    {
        // Exact type matching is intentional: a derived model may have different side effects and
        // should be reviewed before it is treated as covered by a base model handler.
        _handlers[typeof(TModel)] = (model, context) => handler((TModel)model, context);
    }

    public IReadOnlyList<HookResult> Run(
        IEnumerable<AbstractModel> models,
        TContext context)
    {
        var results = new List<HookResult>();

        foreach (var model in models)
        {
            var type = model.GetType();
            HookResultKind resultKind;

            if (_handlers.TryGetValue(type, out var handler))
            {
                resultKind = handler(model, context);
            }
            else if (HookReflection.TryGetOverride(hook, type, out var overrideMethod))
            {
                if (HookReflection.TryGetMod(overrideMethod, out var mod) &&
                    HookReflection.IsNonGameplayMod(mod))
                {
                    WarnIgnoredUnsupported(type, mod);
                    resultKind = HookResultKind.Ignored;
                }
                else
                {
                    WarnUnsupported(type);
                    resultKind = HookResultKind.Unsupported;
                }
            }
            else
            {
                continue;
            }

            results.Add(new HookResult(resultKind, model));

            if (resultKind == HookResultKind.Blocked)
            {
                break;
            }
        }

        return results;
    }

    private void WarnIgnoredUnsupported(Type modelType, Mod mod)
    {
        // Log each ignored model type once per registry so recurring hover previews do not flood the log.
        if (!_warnedIgnoredUnsupportedTypes.Add(modelType))
        {
            return;
        }

        Entry.Logger.Warn(
            $"Mirror for {hook.Name} ignored unsupported {modelType.FullName} from non-gameplay mod {mod.manifest?.id}.");
    }

    private void WarnUnsupported(Type modelType)
    {
        // Log each unsupported model type once per registry so recurring hover previews do not flood the log.
        if (!_warnedUnsupportedTypes.Add(modelType))
        {
            return;
        }

        Entry.Logger.Warn(
            $"Mirror for {hook.Name} does not safely handle {modelType.FullName}; preview may omit that modifier.");
    }
}
