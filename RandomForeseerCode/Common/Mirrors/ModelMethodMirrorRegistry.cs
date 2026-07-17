using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.RandomForeseerCode.Common.Mirrors;

internal enum MirrorDispatchKind
{
    NotOverridden,
    Handled,
    Ignored,
    Unsupported
}

internal readonly record struct MirrorDispatchResult(MirrorDispatchKind Kind);

internal readonly record struct MirrorDispatchResult<TResult>(MirrorDispatchKind Kind, TResult Value);

// Dispatches one mirrored virtual method against one receiver. Invocation details such as source
// scopes, listener enumeration, and hook ordering belong to adapters layered on top of this registry.
internal sealed class ModelMethodMirrorRegistry<TBase, TContext>(MirrorMethodSpec method)
    where TBase : class
    where TContext : IPredictionMirrorContext<TBase>
{
    // All registrations must be completed before the first invocation. Registries are built during
    // static initialization and do not support runtime registration.
    private readonly Dictionary<Type, LookupResult> _lookups = [];

    public void Register<TModel>(Action<TModel, TContext> handler)
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        // Exact type matching is intentional: derived models must be reviewed independently.
        _lookups.Add(type, new(
            MirrorDispatchKind.Handled,
            (receiver, context) => handler((TModel)receiver, context)));
    }

    public void RegisterIgnored<TModel>()
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        _lookups.Add(type, new(MirrorDispatchKind.Ignored, null));
    }

    public MirrorDispatchResult Invoke(TBase receiver, TContext context)
    {
        using var _ = context.PushDispatchSource(receiver, method);
        var lookup = Lookup(receiver.GetType());
        switch (lookup.Kind)
        {
            case MirrorDispatchKind.Handled:
                lookup.Handler!(receiver, context);
                break;
            case MirrorDispatchKind.Unsupported:
                context.RecordMethodNotMirroredRisk();
                break;
        }

        return new(lookup.Kind);
    }

    private LookupResult Lookup(Type type)
    {
        if (_lookups.TryGetValue(type, out var result))
        {
            return result;
        }

        if (!method.TryGetOverride(type, out var overrideMethod))
        {
            result = new(MirrorDispatchKind.NotOverridden, null);
        }
        else if (TryGetMod(overrideMethod, out var mod) && mod.manifest?.affectsGameplay is false)
        {
            Entry.Logger.Info(
                $"Mirror for {method.Name} ignored unsupported {type.FullName} from non-gameplay mod {mod.manifest?.id}.");
            result = new(MirrorDispatchKind.Ignored, null);
        }
        else
        {
            Entry.Logger.Warn(
                $"Mirror for {method.Name} does not safely handle {type.FullName}; preview may omit that modifier.");
            result = new(MirrorDispatchKind.Unsupported, null);
        }

        _lookups.Add(type, result);
        return result;
    }

    private void ValidateOverride(Type type)
    {
        if (!method.TryGetOverride(type, out _))
        {
            throw new InvalidOperationException(
                $"{type.FullName} does not override {method.BaseMethod.DeclaringType?.FullName}.{method.Name}.");
        }
    }

    private static bool TryGetMod(MethodInfo overrideMethod, [NotNullWhen(true)] out Mod? mod)
    {
        var declaringType = overrideMethod.DeclaringType;
        if (declaringType is null)
        {
            mod = null;
            return false;
        }

        // StS2 v0.109.0 centralizes base-game, mod, and test mock type lookup here.
        mod = AssemblyInfo.ModForType(declaringType, out var isBaseGame);
        return !isBaseGame && mod is not null;
    }

    private readonly record struct LookupResult(
        MirrorDispatchKind Kind,
        Action<TBase, TContext>? Handler);
}

// Result-producing counterpart used by mirrored methods such as OrbModel.Evoke.
internal sealed class ModelMethodMirrorRegistry<TBase, TContext, TResult>(MirrorMethodSpec method)
    where TBase : class
    where TContext : IPredictionMirrorContext<TBase>
{
    // All registrations must be completed before the first invocation. Registries are built during
    // static initialization and do not support runtime registration.
    private readonly Dictionary<Type, LookupResult> _lookups = [];

    public void Register<TModel>(Func<TModel, TContext, TResult> handler)
        where TModel : TBase
    {
        var type = typeof(TModel);
        ValidateOverride(type);
        _lookups.Add(type, new(
            MirrorDispatchKind.Handled,
            (receiver, context) => handler((TModel)receiver, context)));
    }

    public MirrorDispatchResult<TResult> Invoke(
        TBase receiver,
        TContext context,
        TResult defaultResult)
    {
        using var _ = context.PushDispatchSource(receiver, method);
        var lookup = Lookup(receiver.GetType());
        return lookup.Kind switch
        {
            MirrorDispatchKind.Handled =>
                new(lookup.Kind, lookup.Handler!(receiver, context)),
            MirrorDispatchKind.Unsupported => MarkUnsupported(context, defaultResult),
            _ => new(lookup.Kind, defaultResult)
        };
    }

    private LookupResult Lookup(Type type)
    {
        if (_lookups.TryGetValue(type, out var result))
        {
            return result;
        }

        if (!method.TryGetOverride(type, out var overrideMethod))
        {
            result = new(MirrorDispatchKind.NotOverridden, null);
        }
        else
        {
            Entry.Logger.Warn(
                $"Mirror for {method.Name} does not safely handle {type.FullName}; preview may omit that behavior.");
            result = new(MirrorDispatchKind.Unsupported, null);
        }

        _lookups.Add(type, result);
        return result;
    }

    private static MirrorDispatchResult<TResult> MarkUnsupported(
        TContext context,
        TResult defaultResult)
    {
        context.RecordMethodNotMirroredRisk();
        return new(MirrorDispatchKind.Unsupported, defaultResult);
    }

    private void ValidateOverride(Type type)
    {
        if (!method.TryGetOverride(type, out _))
        {
            throw new InvalidOperationException(
                $"{type.FullName} does not override {method.BaseMethod.DeclaringType?.FullName}.{method.Name}.");
        }
    }

    private readonly record struct LookupResult(
        MirrorDispatchKind Kind,
        Func<TBase, TContext, TResult>? Handler);
}
