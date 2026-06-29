namespace SolarShading.Core.Geometry;

/// <summary>
/// One occluding element (a shading device) as a set of faces plus its 3D bounding box.
/// Grouping faces per object lets the engine reject an entire occluder cheaply when its
/// shadow can't reach the window (bounding-box / wrong-side culling) before projecting
/// any face.
/// </summary>
public sealed class OccluderObject
{
    public IReadOnlyList<OccluderFace> Faces { get; }
    public Aabb Bounds { get; }

    public OccluderObject(IReadOnlyList<OccluderFace> faces, Aabb bounds)
    {
        Faces = faces;
        Bounds = bounds;
    }

    /// <summary>Build from faces, computing the bounding box from all face vertices.</summary>
    public static OccluderObject FromFaces(IReadOnlyList<OccluderFace> faces)
    {
        var pts = new List<Vec3>();
        foreach (OccluderFace f in faces)
        {
            pts.AddRange(f.Outer.Vertices);
            foreach (Polygon3 hole in f.Holes)
                pts.AddRange(hole.Vertices);
        }
        return new OccluderObject(faces, Aabb.FromPoints(pts));
    }
}
