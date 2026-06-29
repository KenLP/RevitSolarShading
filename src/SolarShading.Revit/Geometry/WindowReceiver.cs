using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>The receiver plane and outline of a window/curtain-wall onto which shading is cast.</summary>
public sealed class WindowReceiver
{
    public required Plane3 Plane { get; init; }
    public required Polygon3 Outline { get; init; }
    /// <summary>Outward facing normal in model coordinates.</summary>
    public required Vec3 OutwardNormal { get; init; }
    public required double AreaM2 { get; init; }

    private const double NormalParallelTolerance = 0.94; // ~20°

    /// <summary>
    /// Build a receiver for a window. Prefers the exact rough opening (wall cutout) for an
    /// accurate window area; falls back to the largest planar face parallel to the facing
    /// orientation if the opening cannot be obtained. Returns null if neither works.
    /// </summary>
    public static WindowReceiver? FromWindow(FamilyInstance window, Options? options = null)
        => FromOpening(window) ?? FromFacing(window, Units.DirToVec3(window.FacingOrientation), options);

    /// <summary>
    /// Build a receiver from the window's rough opening in its host wall, using
    /// <see cref="ExporterIFCUtils.GetInstanceCutoutFromWall"/>. Returns null if the host
    /// is not a wall or the cutout is unavailable.
    /// </summary>
    public static WindowReceiver? FromOpening(FamilyInstance window)
    {
        if (window.Host is not Wall wall)
            return null;

        CurveLoop cutout;
        try
        {
            cutout = ExporterIFCUtils.GetInstanceCutoutFromWall(
                window.Document, wall, window, out XYZ _);
        }
        catch
        {
            return null;
        }
        if (cutout == null)
            return null;

        Vec3 normal;
        try
        {
            normal = Units.DirToVec3(window.FacingOrientation).Normalized();
        }
        catch
        {
            return null;
        }

        // Shift the loop to the exterior wall face along the facing normal.
        XYZ facing = window.FacingOrientation.Normalize();
        Transform shift = Transform.CreateTranslation(facing * (wall.Width * 0.5));

        var pts = new List<Vec3>();
        foreach (Curve c in cutout)
        {
            IList<XYZ> tess = c.CreateTransformed(shift).Tessellate();
            for (int i = 0; i < tess.Count - 1; i++)
                pts.Add(Units.ToVec3(tess[i]));
        }
        if (pts.Count < 3)
            return null;

        var outline = new Polygon3(pts);
        Vec3 origin = Centroid(pts);
        Vec3 u = normal.Cross(Vec3.UnitZ);
        Plane3 plane = u.Length < 1e-6
            ? Plane3.FromOriginNormal(origin, normal)
            : Plane3.FromFrame(origin, normal, u);

        return new WindowReceiver
        {
            Plane = plane,
            Outline = outline,
            OutwardNormal = normal,
            AreaM2 = PolygonArea(outline, plane),
        };
    }

    /// <summary>Build a receiver from any element given an outward facing direction (model coords).</summary>
    public static WindowReceiver? FromFacing(Element element, Vec3 facing, Options? options = null)
    {
        Vec3 normal;
        try
        {
            normal = facing.Normalized();
        }
        catch
        {
            return null;
        }

        PlanarFace? best = null;
        double bestArea = -1;
        foreach (Solid solid in RevitGeometryExtractor.GetSolids(element, options))
        {
            foreach (Face face in solid.Faces)
            {
                if (face is not PlanarFace pf)
                    continue;
                Vec3 fn = Units.DirToVec3(pf.FaceNormal);
                if (Math.Abs(fn.Dot(normal)) < NormalParallelTolerance)
                    continue;
                if (pf.Area > bestArea)
                {
                    bestArea = pf.Area;
                    best = pf;
                }
            }
        }

        if (best == null)
            return null;

        IList<CurveLoop> loops = best.GetEdgesAsCurveLoops();
        if (loops.Count == 0)
            return null;

        Polygon3? outline = LoopToPolygon(loops[0]);
        if (outline == null)
            return null;

        XYZ originXyz = best.Origin;
        Vec3 origin = Units.ToVec3(originXyz);

        // Horizontal U axis on the façade; fall back if facing is near-vertical.
        Vec3 up = Vec3.UnitZ;
        Vec3 u = normal.Cross(up);
        Plane3 plane = u.Length < 1e-6
            ? Plane3.FromOriginNormal(origin, normal)
            : Plane3.FromFrame(origin, normal, u);

        return new WindowReceiver
        {
            Plane = plane,
            Outline = outline,
            OutwardNormal = normal,
            AreaM2 = bestArea * Units.SqFeetToSqMeters,
        };
    }

    private static Vec3 Centroid(IReadOnlyList<Vec3> pts)
    {
        Vec3 sum = Vec3.Zero;
        foreach (Vec3 p in pts)
            sum += p;
        return sum * (1.0 / pts.Count);
    }

    private static double PolygonArea(Polygon3 poly, Plane3 plane)
    {
        double a = 0.0;
        IReadOnlyList<Vec3> v = poly.Vertices;
        int n = v.Count;
        for (int i = 0; i < n; i++)
        {
            (double u1, double w1) = plane.WorldToUv(v[i]);
            (double u2, double w2) = plane.WorldToUv(v[(i + 1) % n]);
            a += u1 * w2 - u2 * w1;
        }
        return Math.Abs(a) * 0.5;
    }

    private static Polygon3? LoopToPolygon(CurveLoop loop)
    {
        var pts = new List<Vec3>();
        foreach (Curve c in loop)
        {
            IList<XYZ> tess = c.Tessellate();
            for (int i = 0; i < tess.Count - 1; i++)
                pts.Add(Units.ToVec3(tess[i]));
        }
        return pts.Count >= 3 ? new Polygon3(pts) : null;
    }
}
