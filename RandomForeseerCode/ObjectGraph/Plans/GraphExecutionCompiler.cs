using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace RandomForeseer.ObjectGraph.Plans;

/// <summary>Emits field access for one bound plan; recursive policy decisions remain in the active copier.</summary>
/// <remarks>Field accesses, names and recursive call targets are embedded in the generated IL.</remarks>
internal static class GraphExecutionCompiler
{
    // Match CopyChild<T>(in T, string), whose first parameter has the signature T&.
    private static readonly MethodInfo CopyChildMethod = typeof(PredictionGraphCopier).GetMethod(
        nameof(PredictionGraphCopier.CopyChild),
        genericParameterCount: 1,
        types: [Type.MakeGenericMethodParameter(0).MakeByRefType(), typeof(string)])!;

    /// <summary>Fills a registered shallow copy, including an existing boxed value.</summary>
    internal delegate void ObjectPopulator(object source, object clone, PredictionGraphCopier copier);

    /// <summary>Copies inline value storage through a typed local and returns the populated value.</summary>
    internal delegate T ValueCopier<T>(in T source, PredictionGraphCopier copier);

    /// <summary>Compiles field writes to an object or box whose runtime type matches the plan.</summary>
    internal static ObjectPopulator CompileObject(GraphTypePlan plan)
    {
        var method = new DynamicMethod(
            $"GraphPopulate_{plan.Type.Name}",
            typeof(void),
            [typeof(object), typeof(object), typeof(PredictionGraphCopier)],
            typeof(GraphExecutionCompiler).Module,
            skipVisibility: true);
        var il = method.GetILGenerator();

        // unbox yields a managed pointer into the registered box, so stfld updates that same identity.
        var storageType = plan.Type.IsValueType ? plan.Type.MakeByRefType() : plan.Type;
        var source = il.DeclareLocal(storageType);
        var clone = il.DeclareLocal(storageType);

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(plan.Type.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, plan.Type);
        il.Emit(OpCodes.Stloc, source);

        il.Emit(OpCodes.Ldarg_1);
        il.Emit(plan.Type.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, plan.Type);
        il.Emit(OpCodes.Stloc, clone);

        EmitFields(
            il,
            plan.Fields,
            () => il.Emit(OpCodes.Ldloc, source),
            () => il.Emit(OpCodes.Ldloc, clone),
            copierArgument: 2);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<ObjectPopulator>();
    }

    /// <summary>Compiles copying for a non-nullable value type, retaining untouched fields from the source.</summary>
    internal static ValueCopier<T> CompileValue<T>(GraphTypePlan<T> plan)
    {
        var method = new DynamicMethod(
            $"GraphCopyValue_{typeof(T).Name}",
            typeof(T),
            [typeof(T).MakeByRefType(), typeof(PredictionGraphCopier)],
            typeof(GraphExecutionCompiler).Module,
            skipVisibility: true);
        var il = method.GetILGenerator();

        // Start with the whole value; field writes target the local through ldloca.
        var clone = il.DeclareLocal(typeof(T));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldobj, typeof(T));
        il.Emit(OpCodes.Stloc, clone);

        EmitFields(
            il,
            plan.Fields,
            () => il.Emit(OpCodes.Ldarg_0),
            () => il.Emit(OpCodes.Ldloca, clone),
            copierArgument: 1);

        il.Emit(OpCodes.Ldloc, clone);
        il.Emit(OpCodes.Ret);

        return method.CreateDelegate<ValueCopier<T>>();
    }

    /// <summary>Emits Reset or typed recursive Copy for each field requiring a write.</summary>
    /// <param name="il">The dynamic method's instruction stream.</param>
    /// <param name="fields">Field descriptions consumed during code generation.</param>
    /// <param name="loadSource">Emits an object reference or managed pointer for reading the source field.</param>
    /// <param name="loadClone">Emits an object reference or managed pointer for writing the destination field.</param>
    /// <param name="copierArgument">The copier's zero-based IL argument index.</param>
    private static void EmitFields(
        ILGenerator il,
        IReadOnlyList<GraphFieldPlan> fields,
        Action loadSource,
        Action loadClone,
        short copierArgument)
    {
        foreach (var plan in fields)
        {
            var field = plan.Field;
            var value = il.DeclareLocal(field.FieldType);

            // C# represents volatile with a required modifier; IL uses a prefix on each field access.
            var isVolatile = field.GetRequiredCustomModifiers().Contains(typeof(IsVolatile));

            if (plan.Reset)
            {
                // initobj produces default(TField) for both reference and value fields.
                il.Emit(OpCodes.Ldloca, value);
                il.Emit(OpCodes.Initobj, field.FieldType);
            }
            else
            {
                loadSource();
                if (isVolatile)
                {
                    il.Emit(OpCodes.Volatile);
                }

                il.Emit(OpCodes.Ldfld, field);
                il.Emit(OpCodes.Stloc, value);
            }

            // Keep the destination below the value-producing instructions for the final stfld.
            loadClone();
            if (plan.Reset)
            {
                il.Emit(OpCodes.Ldloc, value);
            }
            else
            {
                // copier.CopyChild<TField>(in value, fieldName) leaves the copied value above the destination.
                il.Emit(OpCodes.Ldarg, copierArgument);
                il.Emit(OpCodes.Ldloca, value);
                il.Emit(OpCodes.Ldstr, field.Name);
                il.Emit(OpCodes.Call, CopyChildMethod.MakeGenericMethod(field.FieldType));
            }

            if (isVolatile)
            {
                il.Emit(OpCodes.Volatile);
            }

            // Direct IL with skipVisibility also fills private inherited and readonly instance fields.
            il.Emit(OpCodes.Stfld, field);
        }
    }
}
