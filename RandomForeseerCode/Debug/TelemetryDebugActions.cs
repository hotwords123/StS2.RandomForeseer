using MegaCrit.Sts2.Core.Debug;
using RandomForeseer.RandomForeseerCode.Telemetry;

namespace RandomForeseer.RandomForeseerCode.Debug;

internal static class TelemetryDebugActions
{
    public static void CaptureManualException()
    {
        try
        {
            ThrowManualTestException();
        }
        catch (Exception exception)
        {
            ModTelemetry.CaptureException(
                exception,
                "telemetry_debug",
                "capture_manual_test_exception");
        }
    }

    public static void CaptureAutomaticException()
    {
        try
        {
            ThrowAutomaticTestException();
        }
        catch (Exception exception)
        {
            // RitsuLib patches the vanilla Sentry capture boundary and mirrors this exception to
            // every applicant with diagnostics consent. The original call still honors game consent.
            SentryService.CaptureException(exception);
        }
    }

    private static void ThrowManualTestException()
    {
        throw new InvalidOperationException("Random Foreseer manual telemetry test exception.");
    }

    private static void ThrowAutomaticTestException()
    {
        throw new InvalidOperationException("Random Foreseer automatic telemetry test exception.");
    }
}
