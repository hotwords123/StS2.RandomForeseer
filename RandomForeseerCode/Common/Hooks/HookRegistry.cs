using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common.Mirrors;

namespace RandomForeseer.RandomForeseerCode.Common.Hooks;

internal sealed class HookSpec(string name, Type[] parameterTypes)
    : MirrorMethodSpec(
        typeof(AbstractModel),
        name,
        BindingFlags.Instance | BindingFlags.Public,
        parameterTypes);

// Shared per-hook dispatcher. Each concrete hook owns one or more registries with a typed context;
// this class owns listener iteration and delegates source-scoped model dispatch to the shared registry.
internal sealed class HookRegistry<TContext>(HookSpec hook)
    where TContext : IPredictionHookContext
{
    private readonly ModelMethodMirrorRegistry<AbstractModel, TContext> _mirrors = new(hook);

    public void Register<TModel>(Action<TModel, TContext> handler)
        where TModel : AbstractModel
    {
        _mirrors.Register(handler);
    }

    public void RegisterIgnored<TModel>()
        where TModel : AbstractModel
    {
        _mirrors.RegisterIgnored<TModel>();
    }

    public void Run(IEnumerable<AbstractModel> models, TContext context)
    {
        foreach (var model in models)
        {
            if (!context.ShouldContinue)
            {
                break;
            }

            _mirrors.Invoke(model, context);
        }
    }
}

internal interface IPredictionHookContext : IPredictionMirrorContext<AbstractModel>
{
    bool ShouldContinue => true;
}
