using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Common.Hooks;

internal delegate void HookHandler<in TContext>(AbstractModel model, TContext context);

internal delegate bool GenericHookHandler<in TContext>(AbstractModel model, TContext context);

internal sealed record HookSpec(string Name, Type[] ParameterTypes);

// Shared per-hook dispatcher. Each concrete hook owns one or more registries with a typed context;
// this class handles exact model-type handlers, generic handlers, and unsupported detection.
internal sealed class HookRegistry<TContext>(HookSpec hook)
    where TContext : IPredictionHookContext
{
    private readonly Dictionary<Type, HookHandler<TContext>> _handlers = [];
    private readonly List<GenericHookHandler<TContext>> _genericHandlers = [];

    private readonly Dictionary<Type, MethodInfo?> _overrideCache = [];
    private readonly Dictionary<Type, UnsupportedHandling> _unsupportedCache = [];

    public void Register<TModel>(Action<TModel, TContext> handler)
        where TModel : AbstractModel
    {
        // Exact type matching is intentional: a derived model may have different side effects and
        // should be reviewed before it is treated as covered by a base model handler.
        _handlers[typeof(TModel)] = (model, context) => handler((TModel)model, context);
    }

    public void RegisterIgnored<TModel>()
        where TModel : AbstractModel
    {
        Register<TModel>(static (_, _) => { });
    }

    // Generic handlers are fallback mirrors for reviewed hook overrides that are not
    // registered by exact model type. The first one that accepts the model owns it.
    public void RegisterGeneric(GenericHookHandler<TContext> handler)
    {
        _genericHandlers.Add(handler);
    }

    public void Run(IEnumerable<AbstractModel> models, TContext context)
    {
        foreach (var model in models)
        {
            if (!context.ShouldContinue)
            {
                break;
            }

            using (context.PushSource(model))
            {
                if (!TryHandle(model, context))
                {
                    continue;
                }
            }
        }
    }

    // Handles a single model, returning true if it was handled by a registered handler or false
    // if it was ignored or unsupported.
    private bool TryHandle(AbstractModel model, TContext context)
    {
        var type = model.GetType();

        if (!TryGetOverride(type, out var overrideMethod))
        {
            return false;
        }

        if (_handlers.TryGetValue(type, out var handler))
        {
            handler(model, context);
            return true;
        }

        foreach (var genericHandler in _genericHandlers)
        {
            if (genericHandler(model, context))
            {
                return true;
            }
        }

        if (!_unsupportedCache.TryGetValue(type, out var unsupportedHandling))
        {
            if (HookReflection.TryGetMod(overrideMethod, out var mod) &&
                HookReflection.IsNonGameplayMod(mod))
            {
                unsupportedHandling = UnsupportedHandling.Ignore;
                Entry.Logger.Warn(
                    $"Mirror for {hook.Name} ignored unsupported {type.FullName} from non-gameplay mod {mod.manifest?.id}.");
            }
            else
            {
                unsupportedHandling = UnsupportedHandling.MarkRisky;
                Entry.Logger.Warn(
                    $"Mirror for {hook.Name} does not safely handle {type.FullName}; preview may omit that modifier.");
            }

            _unsupportedCache[type] = unsupportedHandling;
        }

        if (unsupportedHandling == UnsupportedHandling.MarkRisky)
        {
            context.MarkCurrentSourceRisky();
        }

        return false;
    }

    private bool TryGetOverride(Type type, [NotNullWhen(true)] out MethodInfo? overrideMethod)
    {
        if (!_overrideCache.TryGetValue(type, out overrideMethod))
        {
            overrideMethod = HookReflection.GetOverride(hook, type);
            _overrideCache[type] = overrideMethod;
        }

        return overrideMethod != null;
    }

    private enum UnsupportedHandling
    {
        Ignore,
        MarkRisky
    }
}

internal interface IPredictionHookContext
{
    IDisposable PushSource(AbstractModel model);

    void MarkCurrentSourceRisky();

    bool ShouldContinue => true;
}
