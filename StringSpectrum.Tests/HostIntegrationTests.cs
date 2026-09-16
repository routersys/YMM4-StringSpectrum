using System.Windows;
using Telemetry;

namespace StringSpectrum.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        StringSpectrumTelemetry.EnsureStartedOnce();
        StringSpectrumTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheParameterCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var parameter = new StringSpectrumPlugin().CreateAudioSpectrumParameter(null);

        Assert.IsType<StringSpectrumParameter>(parameter);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
