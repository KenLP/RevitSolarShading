using Autodesk.Revit.DB;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>
/// Turns a non-planar Revit face (organic / freeform shading geometry) into occluder triangles
/// (metres) for shadow projection.
///
/// This is a replaceable seam: the open-source <see cref="DefaultTessellator"/> uses the Revit
/// API's Face.Triangulate, but a proprietary implementation can take over by shipping a
/// <c>SolarShading.Private.dll</c> next to the add-in that exposes a public class implementing
/// this interface (see <see cref="Tessellation"/>). The private DLL is never part of the public
/// repository, yet the whole add-in keeps working.
/// </summary>
public interface ITessellator
{
    /// <param name="face">The non-planar face to tessellate.</param>
    /// <param name="lod">Level of detail 0..1 (coarse..fine); an implementation may ignore it.</param>
    /// <param name="output">Triangles are appended here as <see cref="OccluderFace"/>s, in metres.</param>
    void Tessellate(Face face, double lod, List<OccluderFace> output);
}

/// <summary>Default open-source tessellator: Revit's Face.Triangulate, triangles in metres.</summary>
public sealed class DefaultTessellator : ITessellator
{
    public void Tessellate(Face face, double lod, List<OccluderFace> output)
    {
        Mesh mesh = face.Triangulate(lod);
        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            MeshTriangle t = mesh.get_Triangle(i);
            Vec3 a = Units.ToVec3(t.get_Vertex(0));
            Vec3 b = Units.ToVec3(t.get_Vertex(1));
            Vec3 c = Units.ToVec3(t.get_Vertex(2));
            Vec3 normal = (b - a).Cross(c - a);
            Vec3? n = normal.Length > 1e-12 ? normal.Normalized() : null;
            output.Add(new OccluderFace(new Polygon3(a, b, c), null, n));
        }
    }
}
