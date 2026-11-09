using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.ObjectGraph.Copiers;

internal static class GenericGraphCopier
{
    internal static GraphReferenceStrategy? Match(Type type, Type definitionType, Type copierType) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == definitionType
            ? GraphReferenceStrategy.CloneWith(Create(type, copierType))
            : null;

    internal static IGraphReferenceCopier Create(Type type, Type copierType) =>
        (IGraphReferenceCopier)Activator.CreateInstance(copierType.MakeGenericType(type.GenericTypeArguments))!;
}
