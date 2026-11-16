namespace RandomForeseer.ObjectGraph;

/// <summary>Serialization-safe identities along a failed copy path, without references to source objects.</summary>
internal sealed record GraphCopyFailure(string Path, IReadOnlyList<GraphCopyFailure.Node> Nodes)
{
    private const string ExceptionDataKey = "RandomForeseer.ObjectGraph.Failure";

    internal void AttachTo(Exception exception) => exception.Data[ExceptionDataKey] = this;

    /// <summary>Finds copy diagnostics on an exception or its wrapped cause.</summary>
    public static GraphCopyFailure? Find(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.Data[ExceptionDataKey] is GraphCopyFailure failure)
            {
                return failure;
            }
        }

        return null;
    }

    internal sealed record Node(string Path, string? Type, string? Assembly);
}
