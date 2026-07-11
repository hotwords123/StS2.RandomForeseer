using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace RandomForeseer.RandomForeseerCode.Common.Mirrors;

// Identifies the original virtual method whose model-specific overrides are mirrored.
// Keeping the base MethodInfo lets the registry support methods declared below AbstractModel,
// including protected methods excluded by the publicizer.
internal class MirrorMethodSpec
{
    private readonly BindingFlags _lookupFlags;
    private readonly Type[] _parameterTypes;

    public string Name { get; }

    public MethodInfo BaseMethod { get; }

    public MirrorMethodSpec(
        Type declaringType,
        string name,
        BindingFlags bindingFlags,
        Type[] parameterTypes)
    {
        Name = name;
        _lookupFlags = bindingFlags & ~BindingFlags.DeclaredOnly;
        _parameterTypes = parameterTypes;
        BaseMethod = declaringType.GetMethod(
                name,
                _lookupFlags | BindingFlags.DeclaredOnly,
                binder: null,
                parameterTypes,
                modifiers: null)
            ?? throw new MissingMethodException(declaringType.FullName, name);
    }

    public bool TryGetOverride(Type receiverType, [NotNullWhen(true)] out MethodInfo? overrideMethod)
    {
        overrideMethod = receiverType.GetMethod(
            Name,
            _lookupFlags,
            binder: null,
            _parameterTypes,
            modifiers: null);

        if (overrideMethod is null || overrideMethod.DeclaringType == BaseMethod.DeclaringType)
        {
            overrideMethod = null;
            return false;
        }

        return true;
    }
}
