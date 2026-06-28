using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.Common.Hooks;

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
    private readonly HashSet<Type> _warnedIgnoredUnsupportedTypes = [];
    private readonly HashSet<Type> _warnedUnsupportedTypes = [];

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
            using (context.PushSource(model))
            {
                if (!TryHandle(model, context))
                {
                    continue;
                }
            }

            if (!context.ShouldContinue)
            {
                break;
            }
        }
    }

    // Handles a single model, returning true if it was handled by a registered handler or false
    // if it was ignored or unsupported.
    private bool TryHandle(AbstractModel model, TContext context)
    {
        var type = model.GetType();

        if (!HookReflection.TryGetOverride(hook, type, out var overrideMethod))
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

        if (HookReflection.TryGetMod(overrideMethod, out var mod) &&
            HookReflection.IsNonGameplayMod(mod))
        {
            WarnIgnoredUnsupported(type, mod);
        }
        else
        {
            WarnUnsupported(type);
            context.MarkCurrentSourceRisky();
        }

        return false;
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

internal interface IPredictionHookContext
{
    IDisposable PushSource(AbstractModel model);

    void MarkCurrentSourceRisky();

    bool ShouldContinue => true;
}
