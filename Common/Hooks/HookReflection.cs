using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.Common.Hooks;

internal static class HookReflection
{
    private static readonly Dictionary<Assembly, Mod?> _modByAssembly = [];

    // Publicizer excludes virtual members, so reflection remains the safest way to distinguish
    // inherited no-op hooks from model-specific overrides.
    public static bool TryGetOverride(HookSpec hook, Type modelType, [NotNullWhen(true)] out MethodInfo? overrideMethod)
    {
        overrideMethod = modelType.GetMethod(
            hook.Name,
            BindingFlags.Instance | BindingFlags.Public,
            hook.ParameterTypes);
        return overrideMethod != null && overrideMethod.DeclaringType != typeof(AbstractModel);
    }

    public static bool TryGetMod(MethodInfo overrideMethod, [NotNullWhen(true)] out Mod? mod)
    {
        var declaringAssembly = overrideMethod.DeclaringType?.Assembly;
        if (declaringAssembly == null || declaringAssembly == typeof(AbstractModel).Assembly)
        {
            mod = null;
            return false;
        }

        if (_modByAssembly.TryGetValue(declaringAssembly, out mod))
        {
            return mod != null;
        }

        mod = ModManager.GetLoadedMods().FirstOrDefault(mod => mod.assembly == declaringAssembly);
        _modByAssembly[declaringAssembly] = mod;
        return mod != null;
    }

    public static bool IsNonGameplayMod(Mod mod)
    {
        return mod.manifest?.affectsGameplay == false;
    }
}
