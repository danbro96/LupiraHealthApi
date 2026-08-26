using LupiraHealthApi.Core.Application.Telemetry;
using LupiraHealthApi.Core.Domain.Telemetry;
using Xunit;

namespace LupiraHealthApi.UnitTests;

public class RingMetricsTests
{
    [Theory]
    [InlineData("hr", RingMetric.HeartRate)]
    [InlineData("HR", RingMetric.HeartRate)]
    [InlineData("hrv", RingMetric.Hrv)]
    [InlineData("spo2", RingMetric.Spo2)]
    [InlineData("skin_temp", RingMetric.SkinTemp)]
    [InlineData("steps", RingMetric.Steps)]
    public void Parses_metric_aliases(string raw, RingMetric expected)
    {
        Assert.True(RingMetrics.TryParse(raw, out var m));
        Assert.Equal(expected, m);
    }

    [Fact]
    public void Rejects_unknown_metric() => Assert.False(RingMetrics.TryParse("bp", out _));
}
