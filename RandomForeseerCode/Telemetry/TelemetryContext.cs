using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace RandomForeseer.RandomForeseerCode.Telemetry;

internal static class TelemetryContext
{
    /// <summary>Creates the stable diagnostic identity of an existing game model without serializing live state.</summary>
    public static object ForModel(AbstractModel model)
    {
        return new
        {
            Id = model.Id.ToString(),
            Type = ForType(model.GetType())
        };
    }

    /// <summary>Creates the stable diagnostic identity of a registered model type without constructing it.</summary>
    public static object ForModel(Type modelType)
    {
        return new
        {
            Id = TryGetModelId(modelType),
            Type = ForType(modelType)
        };
    }

    /// <summary>Creates the stable diagnostic identity of a live creature without serializing its state.</summary>
    public static object ForCreature(Creature creature)
    {
        return creature.IsPlayer
            ? ForModel(creature.Player!.Character)
            : ForModel(creature.Monster!);
    }

    /// <summary>Creates the stable diagnostic identity of a method.</summary>
    public static object ForMethod(MethodBase method)
    {
        return new
        {
            Method = method.FullDescription(),
            DeclaringType = method.DeclaringType is { } type ? ForType(type) : null
        };
    }

    /// <summary>Creates the stable diagnostic identity of a type.</summary>
    public static string ForType(Type type) => type.FullName ?? type.Name;

    private static string? TryGetModelId(Type modelType)
    {
        try
        {
            return ModelDb.GetId(modelType).ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
