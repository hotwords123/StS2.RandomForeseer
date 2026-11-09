namespace RandomForeseer.ObjectGraph.Copiers;

/// <summary>Uses shallow allocation and bound field/value rules for reviewed classes and real boxed values.</summary>
internal sealed class FieldsGraphReferenceCopier : IGraphReferenceCopier
{
    public static FieldsGraphReferenceCopier Instance { get; } = new();

    /// <summary>Shallow-copies a non-null source without constructors; arrays, delegates and strings require other strategies.</summary>
    public object Allocate(object source) => ShallowCopier.Copy(source);

    public void Populate(object source, object clone, PredictionGraphCopier copier) =>
        copier.PopulateFields(source, clone);
}
