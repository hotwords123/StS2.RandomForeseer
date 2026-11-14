using System.Reflection;
using System.Reflection.Emit;
using RandomForeseer.ObjectGraph;
using RandomForeseer.ObjectGraph.Policies;

namespace RandomForeseer.Tests.ObjectGraph;

/// <summary>Protects runtime metadata sharing and captured Type references in <see cref="PredictionGraphCopier"/>.</summary>
/// <remarks>Pure managed fixtures; mutable Emit definitions and user reflection wrappers must not gain sharing.</remarks>
public sealed class ObjectGraphReflectionTests
{
    [Fact]
    public void RuntimeMetadataKeepsItsIdentity()
    {
        var type = typeof(string);
        var method = type.GetMethod(nameof(string.Contains), [typeof(string)])!;
        object[] metadata = [method, method.GetParameters()[0], type.Assembly, type.Module];
        using var scope = new PredictionObjectScope();
        var copier = new PredictionGraphCopier(scope, new GraphCopyPolicy());

        foreach (var item in metadata)
        {
            Assert.Same(item, copier.Copy(item));
        }
    }

    [Fact]
    public void CopiedClosurePreservesCapturedTypeAlongsideIsolatedMutableState()
    {
        Type type = typeof(string);
        List<Type> seen = [];
        Action callback = () => seen.Add(type);
        object[] source = [callback, seen];
        using var scope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);

        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        ((Action)clone[0])();

        Assert.Same(type, Assert.Single((List<Type>)clone[1]));
        Assert.Empty(seen);
    }

    [Fact]
    public void MutableEmitDefinitionsRemainRejected()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("GraphReflectionFixture"),
            AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType("MutableDefinition");
        object[] definitions = [type, new DynamicMethod("Callback", typeof(void), Type.EmptyTypes)];
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);
        foreach (var definition in definitions)
        {
            using var rejectedScope = new PredictionObjectScope();
            Assert.Throws<NotSupportedException>(() => new PredictionGraphCopier(rejectedScope, policy).Copy(definition));
        }
    }

    [Fact]
    public void CustomTypeWrapperStateRemainsIsolated()
    {
        var source = new MutableType(typeof(string));
        using var scope = new PredictionObjectScope();
        var policy = new GraphCopyPolicy(referenceFallback: GraphReferenceStrategy.Clone);
        var clone = new PredictionGraphCopier(scope, policy).Copy(source);
        clone.Items.Add(1);

        Assert.Empty(source.Items);
        Assert.Equal(typeof(string).FullName, clone.FullName);
    }

    private sealed class MutableType(Type type) : TypeDelegator(type)
    {
        public List<int> Items = [];
    }
}
