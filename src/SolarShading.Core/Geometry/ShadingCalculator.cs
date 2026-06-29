using Clipper2Lib;
using SolarShading.Core.Solar;

namespace SolarShading.Core.Geometry;

/// <summary>Shaded-area result for one window at one instant.</summary>
public readonly struct ShadeResult
{
    public double WindowAreaM2 { get; }
    public double ShadedAreaM2 { get; }
    public ShadeResult(double windowAreaM2, double shadedAreaM2)
    {
        WindowAreaM2 = windowAreaM2;
        ShadedAreaM2 = shadedAreaM2;
    }
    /// <summary>Fraction of the window in shadow, 0..1.</summary>
    public double ShadedFraction => WindowAreaM2 > 1e-9 ? Math.Clamp(ShadedAreaM2 / WindowAreaM2, 0.0, 1.0) : 0.0;
    public double SunlitFraction => 1.0 - ShadedFraction;
}

/// <summary>
/// High-level shading engine: projects occluder geometry onto a window plane and
/// returns the exact shaded area via 2D polygon clipping. Revit-independent — the
/// Revit adapter feeds it extracted geometry and a sun vector.
/// </summary>
public sealed class ShadingCalculator
{
    /// <summary>
    /// Compute the shaded area of <paramref name="window"/> under <paramref name="sun"/>,
    /// cast by <paramref name="occluderFaces"/>. The window outline is given as a 3D loop
    /// lying on (or defining) the receiver plane.
    /// </summary>
    public ShadeResult Compute(
        Polygon3 window, Plane3 windowPlane,
        IReadOnlyList<Polygon3> occluderFaces, SunVector sun)
        => Compute(window, windowPlane, OccluderFace.FromPolygons(occluderFaces), sun);

    /// <summary>
    /// Compute the shaded area, with occluder faces that may carry holes (perforations,
    /// frames, cut-outs).
    /// </summary>
    public ShadeResult Compute(
        Polygon3 window, Plane3 windowPlane,
        IReadOnlyList<OccluderFace> occluderFaces, SunVector sun)
        => Compute(window, windowPlane, occluderFaces, sun.ToSun());

    /// <summary>
    /// Core overload taking an explicit unit vector toward the sun, expressed in the
    /// SAME coordinate frame as the geometry. This lets the Revit layer supply a sun
    /// vector already rotated into model (project-north) coordinates without round-
    /// tripping through azimuth. The sun is above the horizon when toSun.Z &gt; 0.
    /// </summary>
    public ShadeResult Compute(
        Polygon3 window, Plane3 windowPlane,
        IReadOnlyList<OccluderFace> occluderFaces, Vec3 toSun)
        => ComputeRegion(window, windowPlane, occluderFaces, toSun).Result;

    /// <summary>
    /// Like <see cref="Compute(Polygon3, Plane3, IReadOnlyList{OccluderFace}, Vec3)"/>
    /// but also returns the shaded region (plane-local 2D loops) so callers can draw it.
    /// </summary>
    public (ShadeResult Result, PathsD ShadedRegion) ComputeRegion(
        Polygon3 window, Plane3 windowPlane,
        IReadOnlyList<OccluderFace> occluderFaces, Vec3 toSun)
    {
        PathsD windowPath = ToClipRegion(window, windowPlane);
        double windowArea = PolygonClipper.Area(windowPath);

        bool daytime = toSun.Z > 0.0;
        if (!daytime || windowArea < 1e-9 || occluderFaces.Count == 0)
            return (new ShadeResult(windowArea, 0.0), new PathsD());

        // Back-face cull: only occluders between the window and the sun matter.
        // (The Revit layer narrows candidates further with a bounding-box filter.)
        Vec3 lightDir = -toSun;

        PathsD footprint = ShadowProjector.ProjectFootprint(occluderFaces, windowPlane, lightDir);
        if (footprint.Count == 0)
            return (new ShadeResult(windowArea, 0.0), new PathsD());

        PathsD shaded = PolygonClipper.Intersect(footprint, windowPath);
        double shadedArea = PolygonClipper.Area(shaded);
        return (new ShadeResult(windowArea, shadedArea), shaded);
    }

    /// <summary>
    /// Compute the shaded area against a set of occluder OBJECTS. Each object is rejected
    /// cheaply by its bounding box before any face is projected — a whole shading device
    /// that can't cast onto the window (wrong side of the glass, or its projected box
    /// misses the window) is skipped (T5). Back-face culling (T3) and polygon
    /// simplification (T4) further cut the clipping work. This is the fast path used for
    /// whole-model runs.
    /// </summary>
    public ShadeResult Compute(
        Polygon3 window, Plane3 windowPlane,
        IReadOnlyList<OccluderObject> occluders, Vec3 toSun)
    {
        PathsD windowPath = ToClipRegion(window, windowPlane);
        double windowArea = PolygonClipper.Area(windowPath);

        bool daytime = toSun.Z > 0.0;
        if (!daytime || windowArea < 1e-9 || occluders.Count == 0)
            return new ShadeResult(windowArea, 0.0);

        Vec3 lightDir = -toSun;
        UvBounds win = UvBounds.Of(windowPath);

        var faces = new List<OccluderFace>();
        foreach (OccluderObject occ in occluders)
            if (IsRelevant(occ, windowPlane, lightDir, win))
                faces.AddRange(occ.Faces);

        if (faces.Count == 0)
            return new ShadeResult(windowArea, 0.0);

        PathsD footprint = PolygonClipper.Simplify(
            ShadowProjector.ProjectFootprint(faces, windowPlane, lightDir));
        if (footprint.Count == 0)
            return new ShadeResult(windowArea, 0.0);

        PathsD shaded = PolygonClipper.Intersect(footprint, windowPath);
        return new ShadeResult(windowArea, PolygonClipper.Area(shaded));
    }

    private static bool IsRelevant(OccluderObject occ, Plane3 plane, Vec3 lightDir, UvBounds win)
    {
        double maxDist = double.MinValue;
        var bounds = new UvBounds();
        bool any = false;
        foreach (Vec3 c in occ.Bounds.Corners())
        {
            double d = plane.SignedDistance(c);
            if (d > maxDist) maxDist = d;
            var uv = ShadowProjector.ProjectPoint(c, plane, lightDir);
            if (uv is { } p)
            {
                bounds.Add(p.U, p.V);
                any = true;
            }
        }
        // Wholly behind the glass (indoor side) => can't shade it.
        if (maxDist < -1e-4)
            return false;
        // Projected bounding box misses the window entirely.
        if (any && !bounds.Overlaps(win, 0.001))
            return false;
        return true;
    }

    /// <summary>Whether the sun is in front of a surface with the given outward normal.</summary>
    public static bool FacesSun(Vec3 outwardNormal, SunVector sun)
        => FacesSun(outwardNormal, sun.ToSun());

    /// <summary>Whether the given toSun vector is in front of a surface's outward normal.</summary>
    public static bool FacesSun(Vec3 outwardNormal, Vec3 toSun)
        => outwardNormal.Dot(toSun) > 0.0;

    private static PathsD ToClipRegion(Polygon3 loop, Plane3 plane)
    {
        var path = new PathD(loop.Vertices.Count);
        foreach (Vec3 v in loop.Vertices)
        {
            (double u, double w) = plane.WorldToUv(v);
            path.Add(new PointD(u, w));
        }
        return PolygonClipper.ToPaths(path);
    }

    /// <summary>A mutable 2D axis-aligned bound in plane-local (U,V) coordinates.</summary>
    private struct UvBounds
    {
        private double _uMin = double.MaxValue, _uMax = double.MinValue;
        private double _vMin = double.MaxValue, _vMax = double.MinValue;
        public UvBounds() { }

        public void Add(double u, double v)
        {
            if (u < _uMin) _uMin = u;
            if (u > _uMax) _uMax = u;
            if (v < _vMin) _vMin = v;
            if (v > _vMax) _vMax = v;
        }

        public bool Overlaps(UvBounds o, double margin)
            => _uMin <= o._uMax + margin && _uMax >= o._uMin - margin
            && _vMin <= o._vMax + margin && _vMax >= o._vMin - margin;

        public static UvBounds Of(PathsD paths)
        {
            var b = new UvBounds();
            foreach (PathD path in paths)
                foreach (PointD p in path)
                    b.Add(p.x, p.y);
            return b;
        }
    }
}
