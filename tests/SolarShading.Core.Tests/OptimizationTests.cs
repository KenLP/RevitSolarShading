using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;
using Xunit;

namespace SolarShading.Core.Tests;

/// <summary>
/// The fast occluder-object path (bounding-box cull, back-face cull, simplification) must
/// give the SAME shaded area as the plain all-faces path.
/// </summary>
public class OptimizationTests
{
    [Fact]
    public void Occluder_object_path_matches_overhang_area()
    {
        const double w = 2.0, h = 1.5, d = 0.5;
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));
        var overhang = new Polygon3(
            new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h));

        var occ = OccluderObject.FromFaces(new[] { new OccluderFace(overhang, null, Vec3.UnitZ) });
        var sun = new SunVector(45.0, 0.0).ToSun();

        ShadeResult r = new ShadingCalculator().Compute(window, plane, new[] { occ }, sun);
        Assert.Equal(1.0, r.ShadedAreaM2, 3);
    }

    [Fact]
    public void Far_occluder_is_culled_by_bounding_box()
    {
        const double w = 2.0, h = 1.5, d = 0.5;
        var plane = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 1, 0), new Vec3(1, 0, 0));
        var window = new Polygon3(
            new Vec3(0, 0, 0), new Vec3(w, 0, 0), new Vec3(w, 0, h), new Vec3(0, 0, h));
        var overhang = OccluderObject.FromFaces(new[]
        {
            new OccluderFace(new Polygon3(
                new Vec3(0, 0, h), new Vec3(w, 0, h), new Vec3(w, d, h), new Vec3(0, d, h)), null, Vec3.UnitZ),
        });
        // A second device 100 m to the side — its projected box never reaches the window.
        var far = OccluderObject.FromFaces(new[]
        {
            new OccluderFace(new Polygon3(
                new Vec3(100, 0, h), new Vec3(101, 0, h), new Vec3(101, d, h), new Vec3(100, d, h)), null, Vec3.UnitZ),
        });

        var sun = new SunVector(45.0, 0.0).ToSun();
        ShadeResult r = new ShadingCalculator().Compute(window, plane, new[] { overhang, far }, sun);
        Assert.Equal(1.0, r.ShadedAreaM2, 3); // far device contributes nothing
    }

    [Fact]
    public void Box_on_ground_via_objects_with_backface_cull_is_correct()
    {
        var ground = Plane3.FromFrame(Vec3.Zero, new Vec3(0, 0, 1), new Vec3(1, 0, 0));
        var groundOutline = new Polygon3(
            new Vec3(-5, -5, 0), new Vec3(5, -5, 0), new Vec3(5, 5, 0), new Vec3(-5, 5, 0));

        Vec3 b000 = new(0, 0, 0), b100 = new(1, 0, 0), b110 = new(1, 1, 0), b010 = new(0, 1, 0);
        Vec3 t001 = new(0, 0, 1), t101 = new(1, 0, 1), t111 = new(1, 1, 1), t011 = new(0, 1, 1);
        var faces = new[]
        {
            new OccluderFace(new Polygon3(b000, b100, b110, b010), null, new Vec3(0, 0, -1)),
            new OccluderFace(new Polygon3(t001, t101, t111, t011), null, new Vec3(0, 0, 1)),
            new OccluderFace(new Polygon3(b000, b100, t101, t001), null, new Vec3(0, -1, 0)),
            new OccluderFace(new Polygon3(b100, b110, t111, t101), null, new Vec3(1, 0, 0)),
            new OccluderFace(new Polygon3(b110, b010, t011, t111), null, new Vec3(0, 1, 0)),
            new OccluderFace(new Polygon3(b010, b000, t001, t011), null, new Vec3(-1, 0, 0)),
        };
        var occ = OccluderObject.FromFaces(faces);
        var sun = new SunVector(45.0, 90.0).ToSun();

        ShadeResult r = new ShadingCalculator().Compute(groundOutline, ground, new[] { occ }, sun);
        Assert.Equal(2.0, r.ShadedAreaM2, 2);
    }
}
