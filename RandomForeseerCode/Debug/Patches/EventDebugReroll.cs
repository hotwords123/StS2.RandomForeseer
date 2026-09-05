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
internal static class EventDebugRerollPatch
{
    private const string ButtonName = Entry.ModId + "_EventDebugReroll";

    private static readonly System.Reflection.MethodInfo GenerateInitialOptionsMethod =
        AccessTools.Method(typeof(EventModel), "GenerateInitialOptionsWrapper");

    private static readonly System.Reflection.MethodInfo SetEventStateMethod =
        AccessTools.Method(typeof(EventModel), "SetEventState", [typeof(LocString), typeof(IEnumerable<EventOption>)]);

    private static void Postfix(NEventLayout __instance)
    {
        var settings = ModData.Settings;
        if (!settings.DebugSettingsEnabled || !settings.EventDebugRerollEnabled ||
            __instance._event is not { IsFinished: false } eventModel ||
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
        button.Connect(BaseButton.SignalName.Pressed, Callable.From(() => Reroll(eventModel)));
        __instance.GetNode<VBoxContainer>("%OptionsContainer").AddChildSafely(button);
    }

    private static void Reroll(EventModel eventModel)
    {
        var options = (IReadOnlyList<EventOption>)GenerateInitialOptionsMethod.Invoke(eventModel, null)!;
        SetEventStateMethod.Invoke(eventModel, [eventModel.InitialDescription, options]);
    }
}
