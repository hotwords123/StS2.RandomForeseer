using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace RandomForeseer.Integrations;

internal sealed class IntegrationCategoryPatcher(Harmony harmony, Assembly assembly)
{
    private readonly List<Registration> _registrations = [];
    private bool _subscribed;

    public void Register(string modId, string category)
    {
        var registration = new Registration(harmony, assembly, modId, category);
        if (registration.TryPatchIfLoaded())
        {
            return;
        }

        _registrations.Add(registration);
        if (!_subscribed)
        {
            ModManager.OnModDetected += OnModDetected;
            _subscribed = true;
        }
    }

    private void OnModDetected(Mod mod)
    {
        if (mod.state != ModLoadState.Loaded)
        {
            return;
        }

        for (var i = _registrations.Count - 1; i >= 0; i--)
        {
            var registration = _registrations[i];
            if (registration.Matches(mod) && registration.TryPatchIfLoaded())
            {
                _registrations.RemoveAt(i);
            }
        }

        if (_registrations.Count == 0 && _subscribed)
        {
            ModManager.OnModDetected -= OnModDetected;
            _subscribed = false;
        }
    }

    private sealed class Registration(Harmony harmony, Assembly assembly, string modId, string category)
    {
        private bool _patched;

        public bool Matches(Mod mod)
        {
            return mod.manifest?.id == modId;
        }

        public bool TryPatchIfLoaded()
        {
            if (_patched)
            {
                return true;
            }

            if (!ModManager.GetLoadedMods().Any(mod => mod.manifest?.id == modId))
            {
                return false;
            }

            try
            {
                harmony.PatchCategory(assembly, category);
                Entry.Logger.Info($"Patched integration category {category} for {modId}.");
            }
            catch (Exception ex)
            {
                Entry.Logger.Warn($"Could not patch integration category {category} for {modId}: {ex}");
            }

            _patched = true;
            return true;
        }
    }
}
