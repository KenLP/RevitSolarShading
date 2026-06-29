using Clipper2Lib;
using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;
using Xunit;

namespace SolarShading.Core.Tests;

public class ShadowGeometryTests
{
    // A horizontal overhang of depth D above a vertical window, lit by a sun at
    // altitude α directly in front, casts a shadow band of height D·tan(α). With
    // D = 0.5 m and α = 45°, the band is 0.5 m tall over a 2 m window => 1.0 m².
    [Fact]
    public void Horizontal_overhang_casts_analytically_correct_shaded_area()
    {
        const double w = 2.0, h = 1.5, d = 0.5;

        // Window plane: vertical, outward normal +Y, located at y = 0.
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));

        // Overhang bottom face at z = h, extending out to y = d.
        var overhang = new Polygon3(
            new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h));

        // Sun straight in front (azimuth 0 = North, the +Y direction), altitude 45°.
        var sun = new SunVector(45.0, 0.0);

        var calc = new ShadingCalculator();
        ShadeResult r = calc.Compute(window, plane, new[] { overhang }, sun);

        Assert.Equal(3.0, r.WindowAreaM2, 3);
        Assert.Equal(1.0, r.ShadedAreaM2, 3);          // D·tan(45°)·W = 0.5·2 = 1.0
        Assert.Equal(1.0 / 3.0, r.ShadedFraction, 3);
    }

    [Fact]
    public void Deeper_sun_angle_shades_more()
    {
        const double w = 2.0, h = 2.0, d = 0.5;
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));
        var overhang = new Polygon3(
            new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h));
        var calc = new ShadingCalculator();

        // 60° elevation -> band = D·tan(60°) = 0.866 m.
        var high = calc.Compute(window, plane, new[] { overhang }, new SunVector(60.0, 0.0));
        // 30° elevation -> band = D·tan(30°) = 0.289 m.
        var low = calc.Compute(window, plane, new[] { overhang }, new SunVector(30.0, 0.0));

        Assert.Equal(0.5 * Math.Tan(60 * Math.PI / 180) * w, high.ShadedAreaM2, 2);
        Assert.Equal(0.5 * Math.Tan(30 * Math.PI / 180) * w, low.ShadedAreaM2, 2);
        Assert.True(high.ShadedAreaM2 > low.ShadedAreaM2);
    }

    [Fact]
    public void Sun_behind_window_does_not_face_it()
    {
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        // Sun in the south (azimuth 180) while window faces north (+Y): not lit.
        var sun = new SunVector(45.0, 180.0);
        Assert.False(ShadingCalculator.FacesSun(plane.Normal, sun));
    }

    [Fact]
    public void Overlapping_occluders_do_not_double_count_area()
    {
        const double w = 2.0, h = 2.0, d = 0.5;
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));
        var overhang = new Polygon3(
            new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h));
        var calc = new ShadingCalculator();

        var single = calc.Compute(window, plane, new[] { overhang }, new SunVector(45.0, 0.0));
        // Two identical overlapping overhangs must give the same shaded area (union, not sum).
        var doubled = calc.Compute(window, plane, new[] { overhang, overhang }, new SunVector(45.0, 0.0));

        Assert.Equal(single.ShadedAreaM2, doubled.ShadedAreaM2, 3);
    }

    [Fact]
    public void Clipper_intersection_of_two_unit_squares_is_correct()
    {
        var a = PolygonClipper.ToPaths(new PathD
        {
            new(0, 0), new(2, 0), new(2, 2), new(0, 2)
        });
        var b = PolygonClipper.ToPaths(new PathD
        {
            new(1, 1), new(3, 1), new(3, 3), new(1, 3)
        });
        double area = PolygonClipper.Area(PolygonClipper.Intersect(a, b));
        Assert.Equal(1.0, area, 4); // overlap is the unit square [1,2]×[1,2]
    }
}
