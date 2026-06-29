using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;
using Xunit;

namespace SolarShading.Core.Tests;

public class CoreEnhancementTests
{
    // A hole in the shading device lets sun through, reducing the shaded area.
    [Fact]
    public void Hole_in_occluder_reduces_shaded_area()
    {
        const double w = 2.0, h = 1.5, d = 0.5;
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));

        var outer = new Polygon3(
            new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h));
        // A rectangular cut-out in the overhang: x in [0.5,1.5], y in [0.1,0.4].
        var hole = new Polygon3(
            new Vec3(0.5, 0.1, h), new Vec3(1.5, 0.1, h), new Vec3(1.5, 0.4, h), new Vec3(0.5, 0.4, h));

        var calc = new ShadingCalculator();
        var sun = new SunVector(45.0, 0.0); // tan(45°) = 1, so y maps 1:1 onto the band

        var solid = calc.Compute(window, plane, new[] { new OccluderFace(outer) }, sun);
        var perforated = calc.Compute(window, plane, new[] { new OccluderFace(outer, new[] { hole }) }, sun);

        Assert.Equal(1.0, solid.ShadedAreaM2, 3);          // full band
        Assert.Equal(0.7, perforated.ShadedAreaM2, 3);     // minus 1.0×0.3 hole
    }

    // A cuboid "building" casts a shadow on the ground whose footprint is analytic.
    [Fact]
    public void Building_box_shadow_on_ground_has_correct_footprint()
    {
        // 1×1×1 box; sun at altitude 45° from the east => horizontal shift H/tan(45°) = 1.
        // Ground shadow footprint = (a + H/tan α) × b = 2 × 1 = 2 m².
        var ground = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 0, 1), new Vec3(1, 0, 0));
        var groundOutline = new Polygon3(
            new Vec3(-5, -5, 0), new Vec3(5, -5, 0), new Vec3(5, 5, 0), new Vec3(-5, 5, 0));

        Vec3 b000 = new(0, 0, 0), b100 = new(1, 0, 0), b110 = new(1, 1, 0), b010 = new(0, 1, 0);
        Vec3 t001 = new(0, 0, 1), t101 = new(1, 0, 1), t111 = new(1, 1, 1), t011 = new(0, 1, 1);
        var faces = new[]
        {
            new Polygon3(b000, b100, b110, b010), // bottom
            new Polygon3(t001, t101, t111, t011), // top
            new Polygon3(b000, b100, t101, t001), // sides
            new Polygon3(b100, b110, t111, t101),
            new Polygon3(b110, b010, t011, t111),
            new Polygon3(b010, b000, t001, t011),
        };

        var sun = new SunVector(45.0, 90.0); // azimuth 90° = East
        var calc = new ShadingCalculator();
        ShadeResult r = calc.Compute(groundOutline, ground, faces, sun);

        Assert.Equal(2.0, r.ShadedAreaM2, 2);
    }

    [Fact]
    public void Singapore_sun_is_east_in_morning_and_west_in_afternoon()
    {
        const double lat = 1.3521, lon = 103.8198;
        var morning = SolarPosition.Instance.Compute(
            new DateTimeOffset(2024, 6, 21, 8, 0, 0, TimeSpan.FromHours(8)), lat, lon);
        var afternoon = SolarPosition.Instance.Compute(
            new DateTimeOffset(2024, 6, 21, 17, 0, 0, TimeSpan.FromHours(8)), lat, lon);

        Assert.True(morning.IsDaytime && afternoon.IsDaytime);
        Assert.InRange(morning.AzimuthDeg, 45.0, 135.0);    // eastern sky
        Assert.InRange(afternoon.AzimuthDeg, 225.0, 315.0); // western sky
    }
}
