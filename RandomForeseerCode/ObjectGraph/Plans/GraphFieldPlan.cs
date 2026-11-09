using System.Reflection;

namespace RandomForeseer.ObjectGraph.Plans;

/// <summary>Describes a field write for IL generation: its metadata and Copy or Reset operation.</summary>
internal sealed class GraphFieldPlan(FieldInfo field, bool reset)
{
    public FieldInfo Field { get; } = field;

    /// <summary>Reset writes default without reading source; otherwise copy the declared field value.</summary>
    public bool Reset { get; } = reset;
}

