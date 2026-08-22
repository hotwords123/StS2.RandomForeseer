using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.OutOfCombat.Patches;

[HarmonyPatch(typeof(NCrystalSphereMask), nameof(NCrystalSphereMask._Ready))]
internal static class CrystalSphereClairvoyancePatch
{
    private const float HiddenFogAlpha = 0.4f;

    private static void Postfix(NCrystalSphereMask __instance)
    {
        var settings = ModData.Settings;
        if (!settings.IsPredictionEnabled || !settings.CrystalSphereClairvoyanceEnabled)
        {
            return;
        }

        // A non-zero timestamp makes the shader use these alpha values instead of its opaque default.
        // Revealing a cell later still fades it normally from HiddenFogAlpha to zero.
        for (var i = 0; i < __instance._values.Count; i++)
        {
            __instance._values[i] = new Vector3(HiddenFogAlpha, HiddenFogAlpha, -1f);
        }

        __instance._material.SetShaderParameter("gridFadeParams", __instance._values);
    }
}
