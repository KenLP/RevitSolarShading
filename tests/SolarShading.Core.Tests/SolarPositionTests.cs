using SolarShading.Core.Solar;
using Xunit;

namespace SolarShading.Core.Tests;

public class SolarPositionTests
{
    // Canonical test case from the NREL SPA paper (Reda & Andreas 2004):
    // 2003-10-17 12:30:30, timezone -07:00, lat 39.742476, lon -105.1786.
    // SPA reference: topocentric zenith 50.11162° (=> altitude 39.88838°), azimuth 194.34024°.
    // We use the NOAA series (no refraction), so we assert agreement within 0.3°.
    [Fact]
    public void Matches_SPA_reference_case_within_tolerance()
    {
        var instant = new DateTimeOffset(2003, 10, 17, 12, 30, 30, TimeSpan.FromHours(-7));
        SunVector sun = SolarPosition.Instance.Compute(instant, 39.742476, -105.1786);

        Assert.InRange(sun.AltitudeDeg, 39.88838 - 0.3, 39.88838 + 0.3);
        Assert.InRange(sun.AzimuthDeg, 194.34024 - 0.3, 194.34024 + 0.3);
    }

    // Singapore (≈1.35°N, 103.82°E) at solar-ish noon: sun should be high and roughly overhead.
    [Fact]
    public void Singapore_local_noon_sun_is_high()
    {
        var instant = new DateTimeOffset(2024, 3, 21, 13, 0, 0, TimeSpan.FromHours(8));
        SunVector sun = SolarPosition.Instance.Compute(instant, 1.3521, 103.8198);

        Assert.True(sun.IsDaytime);
        Assert.True(sun.AltitudeDeg > 70.0, $"Expected high sun, got altitude {sun.AltitudeDeg:F2}°");
    }

    [Fact]
    public void Night_time_sun_is_below_horizon()
    {
        var instant = new DateTimeOffset(2024, 6, 21, 0, 0, 0, TimeSpan.FromHours(8));
        SunVector sun = SolarPosition.Instance.Compute(instant, 1.3521, 103.8198);
        Assert.False(sun.IsDaytime);
    }

    [Fact]
    public void ToSun_and_LightDirection_are_opposite_unit_vectors()
    {
        var sun = new SunVector(45.0, 135.0);
        var toSun = sun.ToSun();
        var light = sun.LightDirection();
        Assert.Equal(1.0, toSun.Length, 6);
        Assert.Equal(-toSun.X, light.X, 9);
        Assert.Equal(-toSun.Y, light.Y, 9);
        Assert.Equal(-toSun.Z, light.Z, 9);
        // Azimuth 135° (SE), altitude 45° => points east, south, up.
        Assert.True(toSun.X > 0); // East
        Assert.True(toSun.Y < 0); // South
        Assert.True(toSun.Z > 0); // Up
    }
}
