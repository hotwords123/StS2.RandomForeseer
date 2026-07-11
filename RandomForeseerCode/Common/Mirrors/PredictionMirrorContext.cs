namespace RandomForeseer.RandomForeseerCode.Common.Mirrors;

internal interface IPredictionMirrorContext<in TBase>
    where TBase : class
{
    IDisposable PushSource(TBase receiver);

    void MarkCurrentSourceRisky();
}
