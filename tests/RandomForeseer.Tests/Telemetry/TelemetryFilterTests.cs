using System.Text.Json.Nodes;
using RandomForeseer.RandomForeseerCode.Telemetry;
using STS2RitsuLib.Telemetry;

namespace RandomForeseer.Tests.Telemetry;

/// <summary>
/// Verifies <see cref="ModTelemetryAdapter.ShouldSend"/> for ordinary events, automatic diagnostics
/// requiring a mod stack frame, and manually captured diagnostics.
/// </summary>
/// <remarks>Pure event-filter tests; no telemetry client, network connection or Godot scene is created.</remarks>
public sealed class TelemetryFilterTests
{
    private static TelemetryEnvelope Envelope(string eventName, JsonNode? payload = null,
        Dictionary<string, object?>? properties = null) => new()
        {
            ApplicantId = "RandomForeseer",
            EventName = eventName,
            RequestId = "test",
            Category = default,
            Payload = payload,
            Properties = properties ?? []
        };

    [Theory]
    [InlineData("session_start")]
    [InlineData("mod_inventory")]
    public void NondiagnosticEventsPassThrough(string eventName) =>
        Assert.True(ModTelemetryAdapter.ShouldSend(Envelope(eventName)));

    [Theory]
    [InlineData("at RandomForeseer.Prediction.Run()", true)]
    [InlineData("at OtherMod.Handler()", false)]
    [InlineData("res://RandomForeseer/scenes/test.tscn", false)]
    public void AutomaticDiagnosticsRequireAModStackFrame(string stack, bool expected)
    {
        var payload = new JsonObject
        {
            ["applicant_payload"] = new JsonObject
            {
                ["exception"] = new JsonObject { ["stack_trace"] = stack }
            }
        };
        Assert.Equal(expected, ModTelemetryAdapter.ShouldSend(Envelope("exception", payload)));
    }

    [Fact]
    public void ManualDiagnosticsDoNotRequireAModStack() => Assert.True(ModTelemetryAdapter.ShouldSend(
        Envelope("exception", new JsonObject { ["stack_trace"] = "OtherMod.Handler()" },
            new() { ["capture_mode"] = "manual" })));

    [Fact]
    public void MissingOrMessageOnlyDiagnosticsDoNotPassTheAutomaticFilter()
    {
        Assert.False(ModTelemetryAdapter.ShouldSend(Envelope("exception")));
        Assert.False(ModTelemetryAdapter.ShouldSend(Envelope("exception",
            new JsonObject { ["message"] = "RandomForeseer.Prediction failed" })));
    }
}
