using Autodesk.Revit.DB;
using Clipper2Lib;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>
/// Turns a shaded region (plane-local 2D loops from the core engine) into thin Revit
/// solids that can be shown as a <see cref="DirectShape"/> overlay. Visualization only —
/// reported areas come from the exact 2D clip, not from these solids.
/// </summary>
public static class ShadowVisualizer
{
    private const double ThicknessFeet = 30.0 / 304.8; // 30 mm slab — easier to see
    private const double MinSegmentFeet = 1.0 / 304.8;  // drop sub-mm segments
    public const double DefaultGapMeters = 0.06;        // push off the glass so it isn't coincident

    public static IList<GeometryObject> BuildSolids(Plane3 plane, PathsD shadedRegion, double gapMeters)
    {
        var solids = new List<GeometryObject>();
        XYZ normal = Units.ToXyz(plane.Normal).Normalize();

        foreach (PathD path in shadedRegion)
        {
            if (Math.Abs(Clipper.Area(path)) < 1e-6)
                continue; // skip holes / slivers

            CurveLoop? loop = ToWorldLoop(plane, path, gapMeters);
            if (loop == null)
                continue;

            Solid? solid = TryExtrude(loop, normal);
            if (solid != null)
                solids.Add(solid);
        }
        return solids;
    }

    /// <summary>
    /// Create a DirectShape overlay for the shaded region, pushed <paramref name="gapMeters"/>
    /// off the glass. Must run in a transaction. Staggering the gap per hour lets a whole-day
    /// sweep fan out into visible layers.
    /// </summary>
    public static DirectShape? CreateOverlay(
        Document doc, Plane3 plane, PathsD shadedRegion, BuiltInCategory category,
        double gapMeters = DefaultGapMeters)
    {
        IList<GeometryObject> solids = BuildSolids(plane, shadedRegion, gapMeters);
        if (solids.Count == 0)
            return null;
        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(category));
        ds.SetShape(solids);
        return ds;
    }

    /// <summary>A distinct colour per hour across the day (blue morning → red afternoon).</summary>
    public static Color HourColor(int hour, int startHour, int endHour)
    {
        double t = endHour > startHour
            ? Math.Clamp((hour - startHour) / (double)(endHour - startHour), 0, 1)
            : 0.0;
        return HsvToColor(240.0 * (1.0 - t), 0.85, 0.95); // hue 240(blue)→0(red)
    }

    /// <summary>Paint overlay elements red (surface + edges) in a view. Needs a transaction.</summary>
    public static void PaintRed(Document doc, View view, IEnumerable<ElementId> ids)
        => Paint(doc, view, ids, new Color(220, 40, 40));

    /// <summary>Paint the given overlay elements a colour (surface + edges) in a view. Needs a transaction.</summary>
    public static void Paint(Document doc, View view, IEnumerable<ElementId> ids, Color color)
    {
        var ogs = new OverrideGraphicSettings();
        ogs.SetProjectionLineColor(color);
        FillPatternElement? solid = new FilteredElementCollector(doc)
            .OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
            .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
        if (solid != null)
        {
            ogs.SetSurfaceForegroundPatternId(solid.Id);
            ogs.SetSurfaceForegroundPatternColor(color);
        }
        foreach (ElementId id in ids)
        {
            try { view.SetElementOverrides(id, ogs); }
            catch { /* view may not support overrides */ }
        }
    }

    private static Color HsvToColor(double hueDeg, double s, double v)
    {
        double h = ((hueDeg % 360) + 360) % 360 / 60.0;
        int i = (int)Math.Floor(h) % 6;
        double f = h - Math.Floor(h);
        double p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
        (double r, double g, double b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return new Color((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static CurveLoop? ToWorldLoop(Plane3 plane, PathD path, double gapMeters)
    {
        Vec3 outward = plane.Normal * gapMeters;
        var pts = new List<XYZ>(path.Count);
        foreach (PointD p in path)
        {
            Vec3 w = plane.UvToWorld(p.x, p.y) + outward;
            XYZ xyz = Units.ToXyz(w);
            if (pts.Count == 0 || pts[^1].DistanceTo(xyz) > MinSegmentFeet)
                pts.Add(xyz);
        }
        if (pts.Count >= 2 && pts[0].DistanceTo(pts[^1]) <= MinSegmentFeet)
            pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3)
            return null;

        var loop = new CurveLoop();
        try
        {
            for (int i = 0; i < pts.Count; i++)
                loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
        }
        catch
        {
            return null;
        }
        return loop;
    }

    private static Solid? TryExtrude(CurveLoop loop, XYZ normal)
    {
        var loops = new List<CurveLoop> { loop };
        foreach (XYZ dir in new[] { normal, normal.Negate() })
        {
            try
            {
                return GeometryCreationUtilities.CreateExtrusionGeometry(loops, dir, ThicknessFeet);
            }
            catch
            {
                // wrong loop orientation for this direction — try the other way
            }
        }
        return null;
    }
}
