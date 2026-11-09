namespace RandomForeseer.Common.Mirrors;

internal interface IMethodMirrorContext<in TBase>
    where TBase : class
{
    IDisposable PushDispatchSource(TBase receiver, MirrorMethodSpec method);

    void RecordMethodNotMirroredRisk();

    void RecordMethodMirrorIncompleteRisk();
}
