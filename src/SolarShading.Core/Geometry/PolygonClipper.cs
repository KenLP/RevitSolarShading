using Clipper2Lib;

namespace SolarShading.Core.Geometry;

/// <summary>
/// Thin wrapper over Clipper2 (Angus Johnson's production implementation of robust
/// polygon boolean operations — the Weiler–Atherton family recommended in the
/// research for exact shadow-area computation). Works in plane-local 2D (U,V)
/// coordinates, in metres. Areas are returned in m².
/// </summary>
public static class PolygonClipper
{
    // Clipper2 scales doubles to 64-bit integers internally. precision = decimal
    // digits kept; 5 gives 10-micron resolution, ample for building geometry while
    // staying far inside Int64 range for normal plane-local coordinates.
    private const int Precision = 5;

    public static PathsD Union(IEnumerable<PathsD> shadows)
    {
        var subject = new PathsD();
        foreach (PathsD s in shadows)
            subject.AddRange(s);
        if (subject.Count == 0)
            return new PathsD();
        // 4-arg overload (empty clip) so we keep precision control on the union.
        return Clipper.Union(subject, new PathsD(), FillRule.NonZero, Precision);
    }

    public static PathsD Intersect(PathsD subject, PathsD clip)
    {
        if (subject.Count == 0 || clip.Count == 0)
            return new PathsD();
        return Clipper.Intersect(subject, clip, FillRule.NonZero, Precision);
    }

    public static PathsD Difference(PathsD subject, PathsD clip)
    {
        if (subject.Count == 0)
            return new PathsD();
        if (clip.Count == 0)
            return subject;
        return Clipper.Difference(subject, clip, FillRule.NonZero, Precision);
    }

    /// <summary>
    /// Remove near-collinear vertices (e.g. from tessellated curves) within a tolerance,
    /// cutting the edge count fed into later boolean ops. epsilon in metres.
    /// </summary>
    public static PathsD Simplify(PathsD paths, double epsilon = 0.002)
        => paths.Count == 0 ? paths : Clipper.SimplifyPaths(paths, epsilon);

    /// <summary>Total absolute area of a set of (possibly nested) loops, in m².</summary>
    public static double Area(PathsD paths)
    {
        double area = 0.0;
        foreach (PathD p in paths)
            area += Clipper.Area(p);
        return Math.Abs(area);
    }

    public static PathsD ToPaths(params PathD[] loops)
    {
        var paths = new PathsD();
        paths.AddRange(loops);
        return paths;
    }
}
