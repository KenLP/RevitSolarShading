namespace SolarShading.Core.Geometry;

/// <summary>
/// An oriented plane with a local 2D (U,V) coordinate frame. Used as the shadow
/// receiver (window face or ground plane). World points are mapped to plane-local
/// 2D coordinates so that 2D polygon clipping can compute exact shaded areas.
/// </summary>
public sealed class Plane3
{
    public Vec3 Origin { get; }
    public Vec3 Normal { get; }
    public Vec3 UAxis { get; }
    public Vec3 VAxis { get; }

    private Plane3(Vec3 origin, Vec3 normal, Vec3 uAxis, Vec3 vAxis)
    {
        Origin = origin;
        Normal = normal;
        UAxis = uAxis;
        VAxis = vAxis;
    }

    /// <summary>
    /// Build a plane from an origin and normal. The U axis is chosen automatically
    /// (world-up preferred) so results are deterministic.
    /// </summary>
    public static Plane3 FromOriginNormal(Vec3 origin, Vec3 normal)
    {
        Vec3 n = normal.Normalized();
        // Pick a reference that is not parallel to the normal.
        Vec3 reference = Math.Abs(n.Dot(Vec3.UnitZ)) < 0.9 ? Vec3.UnitZ : Vec3.UnitX;
        Vec3 u = reference.Cross(n);
        if (u.Length < 1e-9)
            u = Vec3.UnitX;
        u = u.Normalized();
        Vec3 v = n.Cross(u).Normalized();
        return new Plane3(origin, n, u, v);
    }

    /// <summary>Build a plane with an explicit U axis (e.g. window's horizontal axis).</summary>
    public static Plane3 FromFrame(Vec3 origin, Vec3 normal, Vec3 uAxis)
    {
        Vec3 n = normal.Normalized();
        Vec3 u = (uAxis - n * uAxis.Dot(n)).Normalized(); // orthogonalize u against n
        Vec3 v = n.Cross(u).Normalized();
        return new Plane3(origin, n, u, v);
    }

    /// <summary>Signed distance of a world point from the plane (positive on normal side).</summary>
    public double SignedDistance(Vec3 p) => (p - Origin).Dot(Normal);

    /// <summary>Map a world point onto the plane's local 2D (U,V) coordinates.</summary>
    public (double U, double V) WorldToUv(Vec3 p)
    {
        Vec3 d = p - Origin;
        return (d.Dot(UAxis), d.Dot(VAxis));
    }

    /// <summary>Map plane-local 2D coordinates back to a world point.</summary>
    public Vec3 UvToWorld(double u, double v) => Origin + UAxis * u + VAxis * v;
}
