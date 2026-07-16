namespace RandomForeseer.RandomForeseerCode.Common.Mirrors;

internal interface IPredictionMirrorContext<in TBase>
    where TBase : class
{
    IDisposable PushDispatchSource(TBase receiver, MirrorMethodSpec method);

    void RecordMethodNotMirroredRisk();
}
