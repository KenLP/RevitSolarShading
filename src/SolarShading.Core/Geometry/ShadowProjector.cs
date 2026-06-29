using Clipper2Lib;

namespace SolarShading.Core.Geometry;

/// <summary>
/// Casts a parallel (sun) shadow of 3D occluder geometry onto a receiver plane and
/// returns the footprint in the plane's local 2D (U,V) coordinates.
///
/// This avoids building 3D shadow solids and running 3D boolean Intersect/Union
/// (slow and failure-prone). Each occluder face is projected
/// along the light direction onto the receiver plane; the union of those 2D polygons
/// is the exact shadow footprint of the occluder.
/// </summary>
public static class ShadowProjector
{
    private const double ParallelEpsilon = 1e-9;

    /// <summary>
    /// Project a single 3D loop along <paramref name="lightDir"/> (the direction the
    /// sunlight travels) onto <paramref name="receiver"/>. Returns null if the loop
    /// is parallel to the light (casts no footprint) or projects to a degenerate area.
    /// </summary>
    public static PathD? ProjectLoop(Polygon3 loop, Plane3 receiver, Vec3 lightDir)
    {
        double nDotD = receiver.Normal.Dot(lightDir);
        if (Math.Abs(nDotD) < ParallelEpsilon)
            return null; // light grazes the plane — no meaningful projection

        var path = new PathD(loop.Vertices.Count);
        foreach (Vec3 p in loop.Vertices)
        {
            // Intersect the ray p + t*lightDir with the receiver plane.
            double t = receiver.Normal.Dot(receiver.Origin - p) / nDotD;
            Vec3 hit = p + lightDir * t;
            (double u, double v) = receiver.WorldToUv(hit);
            path.Add(new PointD(u, v));
        }
        return path;
    }

    /// <summary>Project a single world point onto the receiver along the light direction.</summary>
    public static (double U, double V)? ProjectPoint(Vec3 p, Plane3 receiver, Vec3 lightDir)
    {
        double nDotD = receiver.Normal.Dot(lightDir);
        if (Math.Abs(nDotD) < ParallelEpsilon)
            return null;
        double t = receiver.Normal.Dot(receiver.Origin - p) / nDotD;
        return receiver.WorldToUv(p + lightDir * t);
    }

    /// <summary>True if a face whose outward normal is known faces away from the sun (back face).</summary>
    private static bool IsBackFace(OccluderFace face, Vec3 lightDir)
        // Front faces point toward the sun => normal·lightDir &lt; 0. For a closed solid the
        // front faces alone tile the silhouette, so back faces are redundant work.
        => face.Normal is { } n && n.Dot(lightDir) >= -ParallelEpsilon;

    /// <summary>
    /// Project a set of occluder faces and return their unioned shadow footprint
    /// on the receiver plane (plane-local 2D, metres).
    /// </summary>
    /// <summary>
    /// Project a loop and normalize it to positive (CCW) orientation. A solid's faces
    /// have mixed outward normals, so their projected 2D windings are inconsistent;
    /// without normalization, coincident opposite-wound faces cancel under the NonZero
    /// fill rule and the silhouette area collapses. Forcing positive orientation makes
    /// the union a true silhouette regardless of face normals.
    /// </summary>
    private static PathD? ProjectPositiveLoop(Polygon3 loop, Plane3 receiver, Vec3 lightDir)
    {
        // Only geometry BETWEEN the sun and the receiver casts a shadow onto it. Clip the loop
        // to the sun-side half-space first: without this, occluder geometry behind the receiver
        // plane (e.g. a deep fin that straddles the glass) projects forward onto the receiver
        // and paints a spurious shadow. The sun is upstream of the light direction, so a point p
        // is on the sun side when (p − origin)·lightDir ≤ 0.
        Polygon3? front = ClipToSunSide(loop, receiver, lightDir);
        if (front is null)
            return null;

        PathD? projected = ProjectLoop(front, receiver, lightDir);
        if (projected is not { Count: >= 3 })
            return null;
        if (Clipper.Area(projected) < 0)
            projected.Reverse();
        return projected;
    }

    /// <summary>
    /// Sutherland–Hodgman clip of a 3D loop against the receiver plane, keeping the half on the
    /// sun side (the side the light comes from). Returns null if nothing remains. Geometry behind
    /// the receiver cannot shadow it; clipping here also guarantees a forward (t ≥ 0) projection.
    /// </summary>
    private static Polygon3? ClipToSunSide(Polygon3 loop, Plane3 receiver, Vec3 lightDir)
    {
        IReadOnlyList<Vec3> v = loop.Vertices;
        int n = v.Count;
        if (n < 3)
            return null;

        const double eps = 1e-9;
        var output = new List<Vec3>(n + 4);
        for (int i = 0; i < n; i++)
        {
            Vec3 a = v[(i - 1 + n) % n];
            Vec3 b = v[i];
            double da = (a - receiver.Origin).Dot(lightDir); // ≤ 0 ⇒ sun side
            double db = (b - receiver.Origin).Dot(lightDir);
            bool aIn = da <= eps;
            bool bIn = db <= eps;

            if (bIn)
            {
                if (!aIn)
                    output.Add(Intersect(a, b, da, db));
                output.Add(b);
            }
            else if (aIn)
            {
                output.Add(Intersect(a, b, da, db));
            }
        }
        return output.Count >= 3 ? new Polygon3(output) : null;

        static Vec3 Intersect(Vec3 a, Vec3 b, double da, double db)
        {
            double denom = da - db;
            double t = Math.Abs(denom) < 1e-15 ? 0.0 : da / denom;
            return a + (b - a) * t;
        }
    }

    public static PathsD ProjectFootprint(
        IEnumerable<Polygon3> occluderFaces, Plane3 receiver, Vec3 lightDir)
    {
        var loops = new PathsD();
        foreach (Polygon3 face in occluderFaces)
        {
            PathD? projected = ProjectPositiveLoop(face, receiver, lightDir);
            if (projected is not null)
                loops.Add(projected);
        }
        if (loops.Count == 0)
            return new PathsD();
        // Union of every projected face = the occluder silhouette footprint.
        return Clipper.Union(loops, new PathsD(), FillRule.NonZero, 5);
    }

    /// <summary>
    /// Project occluder faces that may contain holes. Each face contributes
    /// (projected outer − projected holes); the results are unioned into the final
    /// shadow footprint on the receiver plane.
    /// </summary>
    public static PathsD ProjectFootprint(
        IReadOnlyList<OccluderFace> faces, Plane3 receiver, Vec3 lightDir)
    {
        var faceFootprints = new List<PathsD>(faces.Count);
        foreach (OccluderFace face in faces)
        {
            if (IsBackFace(face, lightDir))
                continue; // T3 back-face cull

            PathD? outer = ProjectPositiveLoop(face.Outer, receiver, lightDir);
            if (outer is null)
                continue;

            var outerPaths = PolygonClipper.ToPaths(outer);
            if (face.Holes.Count == 0)
            {
                faceFootprints.Add(outerPaths);
                continue;
            }

            var holePaths = new PathsD();
            foreach (Polygon3 hole in face.Holes)
            {
                PathD? h = ProjectPositiveLoop(hole, receiver, lightDir);
                if (h is not null)
                    holePaths.Add(h);
            }
            faceFootprints.Add(holePaths.Count == 0
                ? outerPaths
                : PolygonClipper.Difference(outerPaths, holePaths));
        }
        return PolygonClipper.Union(faceFootprints);
    }
}
