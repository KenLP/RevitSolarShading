using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;
using Xunit;

namespace SolarShading.Core.Tests;

/// <summary>
/// First-principles checks on the solar engine (no memorised reference data): at true
/// solar noon the altitude equals 90° − |latitude − declination|, which we verify by
/// sampling the day for the maximum altitude. These pin the algorithm's correctness
/// independently of the single NREL SPA reference point.
/// </summary>
public class SolarAccuracyTests
{
    private static double MaxAltitudeOverDay(int year, int month, int day, double lat, double lon)
    {
        double max = -90;
        // Sample every 10 minutes in UTC; true solar noon (hour angle ≈ 0) gives the peak.
        for (int minute = 0; minute < 24 * 60; minute += 10)
        {
            var instant = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero).AddMinutes(minute);
            double alt = SolarPosition.Instance.Compute(instant, lat, lon).AltitudeDeg;
            if (alt > max)
                max = alt;
        }
        return max;
    }

    [Fact]
    public void Equator_equinox_noon_is_overhead()
    {
        double max = MaxAltitudeOverDay(2024, 3, 20, lat: 0.0, lon: 0.0);
        Assert.InRange(max, 89.0, 90.0001);
    }

    [Fact]
    public void Summer_solstice_noon_altitude_matches_geometry_at_lat40()
    {
        // 90 − (lat − solar declination); at the June solstice declination ≈ 23.44°.
        double expected = 90.0 - (40.0 - 23.44);
        double max = MaxAltitudeOverDay(2024, 6, 21, lat: 40.0, lon: 0.0);
        Assert.InRange(max, expected - 0.6, expected + 0.6);
    }

    [Fact]
    public void Tropic_of_cancer_is_overhead_at_summer_solstice()
    {
        double max = MaxAltitudeOverDay(2024, 6, 21, lat: 23.44, lon: 0.0);
        Assert.InRange(max, 89.0, 90.0001);
    }

    [Fact]
    public void Southern_hemisphere_winter_noon_is_low()
    {
        // Sydney (−33.87°) in June: max altitude ≈ 90 − (33.87 + 23.44) ≈ 32.7°.
        double expected = 90.0 - (33.87 + 23.44);
        double max = MaxAltitudeOverDay(2024, 6, 21, lat: -33.87, lon: 151.21);
        Assert.InRange(max, expected - 1.0, expected + 1.0);
    }
}

public class ClearSkyIrradianceTests
{
    [Fact]
    public void Beam_is_zero_below_horizon_and_positive_above()
    {
        Assert.Equal(0.0, ClearSkyIrradiance.BeamNormal(6, -5));
        Assert.True(ClearSkyIrradiance.BeamNormal(6, 60) > 0);
    }

    [Fact]
    public void Higher_sun_gives_more_beam()
    {
        Assert.True(ClearSkyIrradiance.BeamNormal(6, 60) > ClearSkyIrradiance.BeamNormal(6, 20));
    }

    [Fact]
    public void Incident_on_surface_is_zero_when_sun_is_behind()
    {
        var normal = new Vec3(0, 1, 0);
        var toSunBehind = new Vec3(0, -0.5, 0.866); // mostly −Y, sun behind the +Y face
        Assert.Equal(0.0, ClearSkyIrradiance.OnSurface(6, 60, toSunBehind, normal));
    }

    [Fact]
    public void Incident_peaks_at_normal_incidence()
    {
        var normal = new Vec3(0, 1, 0);
        var head_on = new Vec3(0, 1, 0);
        var oblique = new Vec3(0.7071, 0.7071, 0);
        Assert.True(ClearSkyIrradiance.OnSurface(6, 60, head_on, normal)
                  > ClearSkyIrradiance.OnSurface(6, 60, oblique, normal));
    }
}
