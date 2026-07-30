using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Domain;
using LupiraHealthApi.Domain.Telemetry;
using Xunit;

namespace LupiraHealthApi.UnitTests;

public class DeviceKeyHashingTests
{
    [Fact]
    public void Minted_key_verifies_against_its_hash()
    {
        var (keyId, secret, hash) = DeviceKeyHashing.Mint();
        Assert.NotEqual(Guid.Empty, keyId);
        Assert.True(DeviceKeyHashing.Verify(secret, hash));
        Assert.False(DeviceKeyHashing.Verify(secret + "x", hash));
    }

    [Fact]
    public void Format_then_parse_roundtrips()
    {
        var (keyId, secret, _) = DeviceKeyHashing.Mint();
        var cred = DeviceKeyHashing.Format(keyId, secret);
        Assert.True(DeviceKeyHashing.TryParse(cred, out var parsedId, out var parsedSecret));
        Assert.Equal(keyId, parsedId);
        Assert.Equal(secret, parsedSecret);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nodot")]
    [InlineData("not-a-guid.secret")]
    [InlineData(".secret")]
    public void TryParse_rejects_malformed(string cred) => Assert.False(DeviceKeyHashing.TryParse(cred, out _, out _));
}

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
