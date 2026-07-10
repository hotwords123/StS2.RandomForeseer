using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using MegaCrit.Sts2.Core.Debug;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.WorkshopLoader;

// The variant bundle validation, host-version selection, AssemblyLoadContext
// loading, and ModInitializer forwarding design is adapted from the RitsuLib
// multi-variant loader:
// https://github.com/BAKAOLC/STS2-RitsuLib
// See THIRD_PARTY_NOTICES.md in this directory for its MIT license.

/// <summary>
/// Steam Workshop-only entry point. The normal GitHub package loads the real
/// mod assembly directly and does not use this dispatcher.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class Bootstrap
{
    private const string ModId = "RandomForeseer";
    private const string RealDllName = $"{ModId}.dll";
    private const string RealPckName = $"{ModId}.pck";
    private const string VariantManifestName = "random-foreseer-variants.manifest";

    public static void Initialize()
    {
        var loaderDirectory = Path.GetDirectoryName(typeof(Bootstrap).Assembly.Location);
        if (string.IsNullOrWhiteSpace(loaderDirectory))
        {
            throw new InvalidOperationException($"[${ModId}.Loader] Could not resolve the loader directory.");
        }

        var variants = LoadVariants(loaderDirectory);
        var hostVersion = ResolveHostVersion();
        var selected = SelectVariant(variants, hostVersion);

        Log.Info(
            $"[${ModId}.Loader] Host version {hostVersion?.ToString() ?? "<unknown>"}; " +
            $"selected Random Foreseer {selected.ModVersion} (minimum game version {selected.MinGameVersion}).");

        var loadContext = AssemblyLoadContext.GetLoadContext(typeof(Bootstrap).Assembly) ?? AssemblyLoadContext.Default;
        var realAssembly = loadContext.LoadFromAssemblyPath(selected.DllPath);
        AssociateAssemblyWhenSupported(realAssembly);

        LoadResourcePack(selected.PckPath);

        InvokeRealInitializer(realAssembly);
    }

    private static List<VariantCandidate> LoadVariants(string loaderDirectory)
    {
        var manifestPath = Path.Combine(loaderDirectory, VariantManifestName);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Workshop variant manifest was not found.", manifestPath);
        }

        BundleManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BundleManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            throw new InvalidDataException($"Failed to read Workshop variant manifest {manifestPath}.", ex);
        }

        if (manifest?.Variants is not { Count: > 0 })
        {
            throw new InvalidDataException($"Workshop variant manifest contains no variants: {manifestPath}");
        }

        var libRoot = Path.GetFullPath(Path.Combine(loaderDirectory, "lib"));
        return manifest.Variants
            .Select(entry => CreateCandidate(loaderDirectory, libRoot, entry))
            .ToList();
    }

    private static VariantCandidate CreateCandidate(
        string loaderDirectory,
        string libRoot,
        BundleVariant entry)
    {
        var modVersionText = entry.ModVersion?.Trim();
        if (string.IsNullOrWhiteSpace(modVersionText) ||
            !SemanticVersion.TryFromString(modVersionText, out var modVersion) ||
            modVersion is null)
        {
            throw new InvalidDataException($"Invalid variant Mod version: {entry.ModVersion}");
        }

        var minGameVersionText = entry.MinGameVersion?.Trim();
        if (string.IsNullOrWhiteSpace(minGameVersionText) ||
            !SemanticVersion.TryFromString(minGameVersionText, out var minGameVersion) ||
            minGameVersion is null)
        {
            throw new InvalidDataException($"Invalid variant minimum game version: {entry.MinGameVersion}");
        }

        if (string.IsNullOrWhiteSpace(entry.Directory))
        {
            throw new InvalidDataException($"Variant {entry.ModVersion} has no directory.");
        }

        var variantDirectory = Path.GetFullPath(Path.Combine(loaderDirectory, entry.Directory));
        if (!IsUnderDirectory(variantDirectory, libRoot))
        {
            throw new InvalidDataException($"Variant directory is outside the lib directory: {entry.Directory}");
        }

        var dllPath = Path.Combine(variantDirectory, RealDllName);
        var pckPath = Path.Combine(variantDirectory, RealPckName);
        return new(modVersionText, modVersion, minGameVersionText, minGameVersion, dllPath, pckPath);
    }

    private static VariantCandidate SelectVariant(
        IReadOnlyCollection<VariantCandidate> variants,
        SemanticVersion? hostVersion)
    {
        if (hostVersion is not null)
        {
            var compatible = variants
                .Where(candidate => candidate.MinGameSemanticVersion.CompareTo(hostVersion) <= 0)
                .OrderBy(candidate => candidate.ModSemanticVersion)
                .LastOrDefault();
            if (compatible is not null)
            {
                return compatible;
            }

            Log.Warn(
                $"[${ModId}.Loader] No bundled variant declares support for game {hostVersion}; " +
                "using the newest bundled Mod version as a best-effort fallback.");
        }
        else
        {
            Log.Warn(
                $"[${ModId}.Loader] Could not determine the game version; " +
                "using the newest bundled Mod version as a best-effort fallback.");
        }

        return variants.OrderBy(candidate => candidate.ModSemanticVersion).Last();
    }

    private static SemanticVersion? ResolveHostVersion()
    {
        try
        {
            return ReleaseInfoManager.Instance.SemVer;
        }
        catch (Exception ex)
        {
            Log.Warn($"[${ModId}.Loader] Failed to read the host game version: {ex.Message}");
            return null;
        }
    }

    private static void AssociateAssemblyWhenSupported(Assembly assembly)
    {
        var method = typeof(ModManager).GetMethod(
            "AssociateAssemblyWithMod",
            BindingFlags.Public | BindingFlags.Static,
            null,
            [typeof(string), typeof(Assembly)],
            null);
        method?.Invoke(null, [ModId, assembly]);
    }

    private static void LoadResourcePack(string pckPath)
    {
        var projectSettingsType =
            Type.GetType("Godot.ProjectSettings, GodotSharp", throwOnError: false) ??
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Godot.ProjectSettings", throwOnError: false))
                .FirstOrDefault(type => type is not null) ??
            throw new TypeLoadException("Godot.ProjectSettings could not be resolved.");

        var method = projectSettingsType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(candidate => candidate.Name == "LoadResourcePack")
            .FirstOrDefault(candidate =>
            {
                var parameters = candidate.GetParameters();
                return parameters.Length > 0 && parameters[0].ParameterType == typeof(string);
            }) ?? throw new MissingMethodException(projectSettingsType.FullName, "LoadResourcePack");

        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        arguments[0] = pckPath;
        for (var i = 1; i < parameters.Length; i++)
        {
            arguments[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : GetDefault(parameters[i].ParameterType);
        }

        if (method.Invoke(null, arguments) is not true)
        {
            throw new InvalidOperationException($"Godot failed to load resource pack {pckPath}.");
        }
    }

    private static object? GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    private static void InvokeRealInitializer(Assembly assembly)
    {
        var initializerTypes = GetLoadableTypes(assembly)
            .Where(type => type.GetCustomAttribute<ModInitializerAttribute>() is not null)
            .ToList();
        if (initializerTypes.Count == 0)
        {
            throw new InvalidOperationException($"No Mod initializer was found in {assembly.FullName}.");
        }

        foreach (var type in initializerTypes)
        {
            var attribute = type.GetCustomAttribute<ModInitializerAttribute>()!;
            var method = type.GetMethod(
                attribute.initializerMethod,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) ??
                throw new MissingMethodException(type.FullName, attribute.initializerMethod);
            method.Invoke(null, null);
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            Log.Warn($"[${ModId}.Loader] Only part of {assembly.FullName} could be inspected: {ex.Message}");
            return ex.Types.OfType<Type>();
        }
    }

    private static bool IsUnderDirectory(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record VariantCandidate(
        string ModVersion,
        SemanticVersion ModSemanticVersion,
        string MinGameVersion,
        SemanticVersion MinGameSemanticVersion,
        string DllPath,
        string PckPath);

    private sealed class BundleManifest
    {
        public int Schema { get; set; }
        public List<BundleVariant>? Variants { get; set; }
    }

    private sealed class BundleVariant
    {
        public string? ModVersion { get; set; }
        public string? MinGameVersion { get; set; }
        public string? Directory { get; set; }
    }
}
