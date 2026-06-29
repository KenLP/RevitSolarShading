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
    private const double ThicknessFeet = 30.0 / 304.8;  // 30 mm slab — easier to see
    private const double MinSegmentFeet = 1.0 / 304.8;  // drop sub-mm segments
    public const double DefaultGapMeters = 0.06;        // push off the glass so it isn't coincident

    // Tags stamped on every overlay DirectShape so a re-run can find and delete the previous
    // set (otherwise overlays from earlier runs pile up and overlap into a messy result).
    private const string AppTag = "SolarShading";
    public const string WindowShadowTag = "WindowShadow";
    public const string BuildingShadowTag = "BuildingShadow";

    public static IList<GeometryObject> BuildSolids(Plane3 plane, PathsD shadedRegion, double gapMeters)
    {
        var solids = new List<GeometryObject>();
        if (shadedRegion.Count == 0)
            return solids;
        XYZ normal = Units.ToXyz(plane.Normal).Normalize();

        // Drop tessellation noise (near-duplicate / near-collinear vertices) up front.
        PathsD region = Clipper.SimplifyPaths(shadedRegion, 0.003);

        // Always triangulate, then extrude each triangle. A triangle is a clean convex loop,
        // so CreateExtrusionGeometry succeeds reliably for it — the SAME region is drawn the
        // same way on every window. (Extruding a whole non-convex loop is what failed
        // intermittently, dropping the diagonal sliver on some windows.)
        foreach (PathD path in region)
        {
            double signed = Clipper.Area(path);
            if (Math.Abs(signed) < 1e-6)
                continue; // slivers / holes
            PathD ccw = signed < 0 ? Reversed(path) : path;
            foreach ((PointD a, PointD b, PointD c) in Triangulate(ccw))
            {
                CurveLoop? tri = TriangleLoop(plane, a, b, c, gapMeters);
                Solid? solid = tri == null ? null : TryExtrude(tri, normal);
                if (solid != null)
                    solids.Add(solid);
            }
        }
        return solids;
    }

    private static CurveLoop? TriangleLoop(Plane3 plane, PointD a, PointD b, PointD c, double gapMeters)
    {
        Vec3 outward = plane.Normal * gapMeters;
        XYZ pa = Units.ToXyz(plane.UvToWorld(a.x, a.y) + outward);
        XYZ pb = Units.ToXyz(plane.UvToWorld(b.x, b.y) + outward);
        XYZ pc = Units.ToXyz(plane.UvToWorld(c.x, c.y) + outward);
        if (pa.DistanceTo(pb) < MinSegmentFeet || pb.DistanceTo(pc) < MinSegmentFeet
            || pc.DistanceTo(pa) < MinSegmentFeet)
            return null;
        try
        {
            var loop = new CurveLoop();
            loop.Append(Line.CreateBound(pa, pb));
            loop.Append(Line.CreateBound(pb, pc));
            loop.Append(Line.CreateBound(pc, pa));
            return loop;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a DirectShape overlay for the shaded region, pushed <paramref name="gapMeters"/>
    /// off the glass. Must run in a transaction. Staggering the gap per hour lets a whole-day
    /// sweep fan out into visible layers.
    /// </summary>
    public static DirectShape? CreateOverlay(
        Document doc, Plane3 plane, PathsD shadedRegion, BuiltInCategory category,
        double gapMeters = DefaultGapMeters, string? tag = null)
    {
        IList<GeometryObject> solids = BuildSolids(plane, shadedRegion, gapMeters);
        if (solids.Count == 0)
            return null;
        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(category));
        // Stamp app/tag ids so ClearOverlays can remove this run's overlays on the next run.
        ds.ApplicationId = AppTag;
        if (tag != null)
            ds.ApplicationDataId = tag;
        ds.SetShape(solids);
        return ds;
    }

    /// <summary>
    /// Delete overlay DirectShapes left by a previous run with the given tag. Must run in a
    /// transaction. Keeps re-runs from stacking shadow layers on top of each other.
    /// </summary>
    public static void ClearOverlays(Document doc, string tag)
    {
        var ids = new FilteredElementCollector(doc)
            .OfClass(typeof(DirectShape))
            .Cast<DirectShape>()
            .Where(d => d.ApplicationId == AppTag && d.ApplicationDataId == tag)
            .Select(d => d.Id)
            .ToList();
        if (ids.Count > 0)
            doc.Delete(ids);
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

    /// <summary>Ear-clipping triangulation of a simple CCW polygon (plane-local U,V).</summary>
    private static List<(PointD, PointD, PointD)> Triangulate(PathD path)
    {
        var pts = new List<PointD>(path);
        for (int i = pts.Count - 1; i > 0; i--)
            if (Near(pts[i], pts[i - 1]))
                pts.RemoveAt(i);
        if (pts.Count >= 2 && Near(pts[0], pts[^1]))
            pts.RemoveAt(pts.Count - 1);

        var tris = new List<(PointD, PointD, PointD)>();
        if (pts.Count < 3)
            return tris;

        var idx = new List<int>(pts.Count);
        for (int i = 0; i < pts.Count; i++)
            idx.Add(i);

        int guard = pts.Count * pts.Count + 16;
        while (idx.Count > 3 && guard-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                PointD a = pts[idx[(i - 1 + idx.Count) % idx.Count]];
                PointD b = pts[idx[i]];
                PointD c = pts[idx[(i + 1) % idx.Count]];
                if (Cross(a, b, c) <= 1e-12)
                    continue; // reflex or collinear — not a convex ear

                bool empty = true;
                foreach (int j in idx)
                {
                    PointD p = pts[j];
                    if (Near(p, a) || Near(p, b) || Near(p, c))
                        continue;
                    if (InTriangle(p, a, b, c))
                    {
                        empty = false;
                        break;
                    }
                }
                if (!empty)
                    continue;

                tris.Add((a, b, c));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped)
                break; // degenerate remainder — stop with what we have
        }
        if (idx.Count == 3)
            tris.Add((pts[idx[0]], pts[idx[1]], pts[idx[2]]));
        return tris;
    }

    private static PathD Reversed(PathD path)
    {
        var r = new PathD(path.Count);
        for (int i = path.Count - 1; i >= 0; i--)
            r.Add(path[i]);
        return r;
    }

    private static bool Near(PointD a, PointD b)
        => Math.Abs(a.x - b.x) < 1e-7 && Math.Abs(a.y - b.y) < 1e-7;

    private static double Cross(PointD a, PointD b, PointD c)
        => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

    private static bool InTriangle(PointD p, PointD a, PointD b, PointD c)
    {
        const double eps = 1e-9;
        return Cross(a, b, p) > eps && Cross(b, c, p) > eps && Cross(c, a, p) > eps;
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
