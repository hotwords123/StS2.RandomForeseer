using System.Reflection;

namespace RandomForeseer.Common;

internal static class ReflectionFlags
{
    public const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
    public const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    public const BindingFlags PublicStatic = BindingFlags.Static | BindingFlags.Public;
    public const BindingFlags NonPublicStatic = BindingFlags.Static | BindingFlags.NonPublic;
    public const BindingFlags AllInstance = PublicInstance | BindingFlags.NonPublic;
    public const BindingFlags AllStatic = PublicStatic | BindingFlags.NonPublic;
    public const BindingFlags AllMembers = AllInstance | AllStatic;
}
