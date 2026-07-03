using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.RandomForeseerCode.Common.Hooks;

internal static class HookReflection
{
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

    public static bool IsVanillaModel(Type modelType)
    {
        return modelType.Assembly == typeof(AbstractModel).Assembly;
    }

    public static bool TryGetMod(MethodInfo overrideMethod, [NotNullWhen(true)] out Mod? mod)
    {
        var declaringAssembly = overrideMethod.DeclaringType?.Assembly;
        if (declaringAssembly == null || declaringAssembly == typeof(AbstractModel).Assembly)
        {
            mod = null;
            return false;
        }

        // StS2 v0.108.0 initializes AssemblyInfo after mod loading and maintains a
        // direct assembly-to-mod map, including additional assemblies associated by mods.
        return AssemblyInfo.ModMap!.TryGetValue(declaringAssembly, out mod);
    }

    public static bool IsNonGameplayMod(Mod mod)
    {
        return mod.manifest?.affectsGameplay == false;
    }
}
