namespace SolarShading.Core.Geometry;

/// <summary>
/// A single occluder face: an outer boundary loop plus any inner hole loops (e.g. a
/// perforated sun-shade, a louvre with cut-outs, a frame). Modelling holes explicitly
/// lets the shadow footprint subtract them — a hard case for 3D-boolean approaches that
/// here falls out naturally from 2D clipping.
/// </summary>
public sealed class OccluderFace
{
    public Polygon3 Outer { get; }
    public IReadOnlyList<Polygon3> Holes { get; }

    /// <summary>
    /// Outward face normal, when known. Used for back-face culling: only faces that face
    /// the sun contribute to a closed solid's silhouette. Null = unknown (never culled).
    /// </summary>
    public Vec3? Normal { get; }

    public OccluderFace(Polygon3 outer, IReadOnlyList<Polygon3>? holes = null, Vec3? normal = null)
    {
        Outer = outer;
        Holes = holes ?? Array.Empty<Polygon3>();
        Normal = normal;
    }

    /// <summary>Convenience: wrap plain outline polygons as hole-free occluder faces.</summary>
    public static IReadOnlyList<OccluderFace> FromPolygons(IEnumerable<Polygon3> polygons)
        => polygons.Select(p => new OccluderFace(p)).ToList();
}
