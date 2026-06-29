namespace SolarShading.Core.Geometry;

/// <summary>
/// A 3D polygon described by an ordered loop of vertices (the face of an occluder
/// or the outline of a receiver). The loop need not be closed explicitly; the last
/// vertex is implicitly connected back to the first.
/// </summary>
public sealed class Polygon3
{
    public IReadOnlyList<Vec3> Vertices { get; }

    public Polygon3(IReadOnlyList<Vec3> vertices)
    {
        if (vertices.Count < 3)
            throw new ArgumentException("A polygon needs at least 3 vertices.", nameof(vertices));
        Vertices = vertices;
    }

    public Polygon3(params Vec3[] vertices) : this((IReadOnlyList<Vec3>)vertices) { }
}
