using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events.Custom.CrystalSphere;

namespace RandomForeseer.OutOfCombat;

[HarmonyPatch(typeof(NCrystalSphereMask), nameof(NCrystalSphereMask._Ready))]
internal static class CrystalSphereClairvoyancePatch
{
    private const float HiddenFogAlpha = 0.25f;

    private static void Postfix(NCrystalSphereMask __instance)
    {
        if (!RandomForeseerSettings.IsPredictionFeatureEnabled(
                RandomForeseerSettings.EnableCrystalSphereClairvoyance))
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
