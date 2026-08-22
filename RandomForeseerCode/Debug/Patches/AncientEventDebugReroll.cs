using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Events;
using RandomForeseer.RandomForeseerCode.Data;

namespace RandomForeseer.RandomForeseerCode.Debug.Patches;

[HarmonyPatch(typeof(NEventLayout), nameof(NEventLayout.AddOptions))]
internal static class AncientEventDebugRerollPatch
{
    private const string ButtonName = Entry.ModId + "_AncientEventDebugReroll";

    private static readonly System.Reflection.MethodInfo GenerateInitialOptionsMethod =
        AccessTools.Method(typeof(AncientEventModel), "GenerateInitialOptionsWrapper");

    private static readonly System.Reflection.MethodInfo SetEventStateMethod =
        AccessTools.Method(typeof(EventModel), "SetEventState", [typeof(LocString), typeof(IEnumerable<EventOption>)]);

    private static void Postfix(NEventLayout __instance)
    {
        var settings = ModData.Settings;
        if (!settings.DebugSettingsEnabled || !settings.AncientEventDebugRerollEnabled ||
            __instance._event is not AncientEventModel ancient ||
            ancient.IsFinished ||
            __instance.GetNodeOrNull<Button>(ButtonName) != null)
        {
            return;
        }

        var button = new Button
        {
            Name = ButtonName,
            Text = "Reroll",
            CustomMinimumSize = new Vector2(180f, 44f),
            FocusMode = Control.FocusModeEnum.None
        };
        button.Connect(BaseButton.SignalName.Pressed, Callable.From(() => Reroll(ancient)));
        __instance.GetNode<VBoxContainer>("%OptionsContainer").AddChildSafely(button);
    }

    private static void Reroll(AncientEventModel ancient)
    {
        var options = (IReadOnlyList<EventOption>)GenerateInitialOptionsMethod.Invoke(ancient, null)!;
        SetEventStateMethod.Invoke(ancient, [ancient.InitialDescription, options]);
    }
}
