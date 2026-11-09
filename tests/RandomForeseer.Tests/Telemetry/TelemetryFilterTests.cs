using System.Text.Json.Nodes;
using RandomForeseer.Telemetry;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Tests.Telemetry;

/// <summary>
/// Verifies <see cref="ModTelemetryFilter.ShouldCapture"/> through
/// <see cref="TelemetryRequest.WithCaptureFilter"/> for automatic exception chains, manual diagnostics,
/// and the two Godot engine stack fields also filtered by the gateway.
/// </summary>
/// <remarks>
/// Pure capture-filter tests; no telemetry client, queue, network connection or Godot scene is created.
/// Exception stacks are supplied explicitly to avoid the test assembly's own RandomForeseer namespace
/// accidentally satisfying the filter. Engine source data is checked for mutation.
/// </remarks>
public sealed class TelemetryFilterTests
{
    private static bool ShouldCapture(string eventName, object? sourceData = null, string source = "godot_logger")
    {
        var request = TelemetryRequest.Diagnostics("Test diagnostics")
            .WithCaptureFilter(ModTelemetryFilter.ShouldCapture);
        return request.CaptureFilter!(new(eventName, request.RequestId, request.Category, source, sourceData));
    }

    [Theory]
    [InlineData("session_start")]
    [InlineData("mod_inventory")]
    [InlineData("future_diagnostic")]
    public void OtherEventsPassThrough(string eventName) => Assert.True(ShouldCapture(eventName));

    [Theory]
    [InlineData("at RandomForeseer.Prediction.Run()", true)]
    [InlineData("at OtherMod.Handler()", false)]
    [InlineData("res://RandomForeseer/scenes/test.tscn", false)]
    [InlineData(null, false)]
    public void AutomaticExceptionsRequireAModStack(string? stack, bool expected) =>
        Assert.Equal(expected, ShouldCapture("exception", new StackTraceException(stack), "vanilla_sentry"));

    [Fact]
    public void InnerExceptionStacksAreCheckedWithoutMatchingMessages()
    {
        var unrelated = new StackTraceException("OtherMod.Handler()");
        Assert.False(ShouldCapture("exception", new Exception("RandomForeseer.Prediction failed", unrelated)));
        var relevant = new StackTraceException("RandomForeseer.Prediction.Run()");
        Assert.True(ShouldCapture("exception", new StackTraceException("OtherMod.Handler()",
            new Exception("Outer wrapper", relevant))));
    }

    [Theory]
    [InlineData("random_foreseer/combat", true)]
    [InlineData("random_foreseer_other/combat", false)]
    [InlineData("vanilla_sentry", false)]
    [InlineData("applicant", false)]
    public void OnlyOurManualExceptionSourceBypassesTheStackCheck(string source, bool expected) =>
        Assert.Equal(expected, ShouldCapture("exception", new StackTraceException(null), source));

    [Fact]
    public void MissingOrSerializedExceptionSourcesDoNotPassTheAutomaticFilter()
    {
        Assert.False(ShouldCapture("exception"));
        Assert.False(ShouldCapture("exception", new JsonObject { ["stack_trace"] = "RandomForeseer.Prediction.Run()" }));
    }

    [Theory]
    [InlineData("script_backtrace")]
    [InlineData("code")]
    public void EngineErrorsAcceptEitherObservedStackFieldWithoutMutatingTheSource(string field)
    {
        var error = new JsonObject
        {
            ["code"] = "Method/function failed. Returning: nullptr",
            ["script_backtrace"] = "C# backtrace:\n    [0] void Godot.Node.GetNode()"
        };
        error[field] = "   at RandomForeseer.Common.HoverTips.PredictionHoverTipControlFactory.CreateAndAddCard()";
        var original = error.DeepClone();
        Assert.True(ShouldCapture("godot_engine_error", error));
        Assert.True(JsonNode.DeepEquals(original, error));
    }

    [Fact]
    public void EngineErrorsDoNotMatchOtherMetadataOrManualSources()
    {
        var error = new JsonObject
        {
            ["code"] = "System.NullReferenceException\n   at MegaCrit.Sts2.Core.Nodes.Vfx.NNecrobinderVfx.UpdateFlameVisibility()",
            ["script_backtrace"] = "C# backtrace:\n    [0] void Godot.NativeInterop.ExceptionUtils.LogException()",
            ["message"] = "RandomForeseer.Prediction failed",
            ["file"] = "RandomForeseer.Prediction.cs",
            ["function"] = "RandomForeseer.Prediction.Run",
            ["framework_runtime"] = new JsonObject { ["stack_trace"] = "RandomForeseer.Prediction.Run()" }
        };
        Assert.False(ShouldCapture("godot_engine_error", error));
        Assert.False(ShouldCapture("godot_engine_error", error, "random_foreseer/debug"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("\"RandomForeseer.Prediction\"")]
    [InlineData("{}")]
    [InlineData("{\"script_backtrace\":null,\"code\":null}")]
    [InlineData("{\"script_backtrace\":\"\",\"code\":\"Invalid Task ID\"}")]
    [InlineData("{\"script_backtrace\":123,\"code\":true}")]
    [InlineData("{\"script_backtrace\":[\"RandomForeseer.Prediction\"],\"code\":{\"stack_trace\":\"RandomForeseer.Prediction\"}}")]
    public void EngineErrorsWithoutRecognizedStackStringsAreFiltered(string json) =>
        Assert.False(ShouldCapture("godot_engine_error", JsonNode.Parse(json)));

    private sealed class StackTraceException(string? stack, Exception? inner = null) : Exception("Synthetic error", inner)
    {
        public override string? StackTrace => stack;
    }
}
