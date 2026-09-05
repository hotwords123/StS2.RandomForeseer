using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using RandomForeseer.RandomForeseerCode.Data;
using RandomForeseer.RandomForeseerCode.Settings;
using STS2RitsuLib.Settings;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NCrystalSphereMask), nameof(NCrystalSphereMask._Ready))]
internal static class CrystalSphereClairvoyancePatch
{
    private const float MaskAlpha = 0.4f;

    private static void Postfix(NCrystalSphereMask __instance)
    {
        var original = __instance.SelfModulate;
        Refresh(__instance, original);

        ModSettingsBindingWriteEvents.SubscribeValueWrittenWhileNodeAlive(__instance, binding =>
        {
            if (IsRefreshBinding(binding))
            {
                Refresh(__instance, original);
            }
        });
    }

    private static bool IsRefreshBinding(IModSettingsBinding binding)
    {
        return ReferenceEquals(binding, SettingsUiBindings.SingleplayerPredictionEnabled) ||
               ReferenceEquals(binding, SettingsUiBindings.MultiplayerPredictionEnabled) ||
               ReferenceEquals(binding, SettingsUiBindings.CrystalSphereClairvoyanceEnabled);
    }

    private static void Refresh(NCrystalSphereMask mask, Color original)
    {
        var settings = ModData.Settings;

        mask.SelfModulate = settings.IsPredictionEnabled && settings.CrystalSphereClairvoyanceEnabled
            ? original with { A = MaskAlpha }
            : original;
    }
}
