using Autodesk.Revit.DB;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>
/// Extracts Revit element geometry into Revit-independent core types
/// (<see cref="OccluderFace"/> / <see cref="Polygon3"/>, in metres). Planar faces keep
/// their exact edge loops (outer + holes); curved faces are tessellated. The resulting
/// faces feed the validated <c>SolarShading.Core</c> shadow engine.
/// </summary>
public static class RevitGeometryExtractor
{
    private static readonly Options DefaultOptions = new()
    {
        ComputeReferences = false,
        DetailLevel = ViewDetailLevel.Fine,
        IncludeNonVisibleObjects = false,
    };

    // Level-of-detail for triangulating curved faces (organic/freeform shades): 0 = coarsest,
    // 1 = finest. Raised for smoother organic shadow outlines (more triangles, slightly slower).
    private const double CurvedFaceLod = 0.6;

    /// <summary>All solids of an element, recursing through geometry instances (world coords).</summary>
    public static IReadOnlyList<Solid> GetSolids(Element element, Options? options = null)
    {
        var solids = new List<Solid>();
        GeometryElement? geo = element.get_Geometry(options ?? DefaultOptions);
        if (geo != null)
            CollectSolids(geo, solids);
        return solids;
    }

    private static void CollectSolids(GeometryElement geo, List<Solid> solids)
    {
        foreach (GeometryObject obj in geo)
        {
            switch (obj)
            {
                case Solid s when s.Volume > 1e-6 && s.Faces.Size > 0:
                    solids.Add(s);
                    break;
                case GeometryInstance gi:
                    CollectSolids(gi.GetInstanceGeometry(), solids);
                    break;
            }
        }
    }

    /// <summary>Convert an element's solids into occluder faces for shadow projection.</summary>
    public static IReadOnlyList<OccluderFace> ToOccluderFaces(Element element, Options? options = null)
    {
        var faces = new List<OccluderFace>();
        foreach (Solid solid in GetSolids(element, options))
            AppendSolidFaces(solid, faces);
        return faces;
    }

    /// <summary>
    /// Convert an element into an <see cref="OccluderObject"/> (faces + bounding box) for the
    /// fast culling path. Returns null if the element has no usable solid geometry.
    /// </summary>
    public static OccluderObject? ToOccluder(Element element, Options? options = null)
    {
        IReadOnlyList<OccluderFace> faces = ToOccluderFaces(element, options);
        return faces.Count == 0 ? null : OccluderObject.FromFaces(faces);
    }

    /// <summary>
    /// Build a self-shading occluder from a window's OWN family geometry — shading fins/louvres
    /// modelled directly inside the window family.
    ///
    /// The shade geometry is isolated by sub-category: glass and frame are drawn on named
    /// sub-categories (so their solids carry a GraphicsStyle), while the shading geometry is
    /// drawn WITHOUT a sub-category (no GraphicsStyle). Keeping only the no-sub-category solids
    /// therefore picks up the shades and excludes the window body — which would otherwise
    /// self-occlude the whole pane. (Families that put their shades on a sub-category won't be
    /// auto-detected; tag such shades as separate shading devices instead.)
    /// </summary>
    public static OccluderObject? ToSelfShadingOccluder(Element window, Options? options = null)
    {
        Document doc = window.Document;
        var faces = new List<OccluderFace>();
        GeometryElement? geo = window.get_Geometry(options ?? DefaultOptions);
        if (geo != null)
            CollectShadeFaces(geo, doc, faces);
        return faces.Count == 0 ? null : OccluderObject.FromFaces(faces);
    }

    private static void CollectShadeFaces(GeometryElement geo, Document doc, List<OccluderFace> faces)
    {
        foreach (GeometryObject obj in geo)
        {
            switch (obj)
            {
                case Solid s when s.Volume > 1e-6 && s.Faces.Size > 0:
                    // No GraphicsStyle (no sub-category) == shading geometry, not glass/frame.
                    if (doc.GetElement(s.GraphicsStyleId) is not GraphicsStyle)
                        AppendSolidFaces(s, faces);
                    break;
                case GeometryInstance gi:
                    CollectShadeFaces(gi.GetInstanceGeometry(), doc, faces);
                    break;
            }
        }
    }

    public static void AppendSolidFaces(Solid solid, List<OccluderFace> faces)
    {
        foreach (Face face in solid.Faces)
        {
            if (face is PlanarFace planar)
            {
                OccluderFace? of = PlanarFaceToOccluder(planar);
                if (of != null)
                    faces.Add(of);
            }
            else
            {
                AppendTessellatedFace(face, faces);
            }
        }
    }

    private static OccluderFace? PlanarFaceToOccluder(PlanarFace face)
    {
        IList<CurveLoop> loops = face.GetEdgesAsCurveLoops();
        if (loops.Count == 0)
            return null;

        // The outer loop is the one with the largest projected extent; Revit usually
        // returns it first, but pick by absolute area to be safe.
        int outerIdx = 0;
        double maxLen = -1;
        for (int i = 0; i < loops.Count; i++)
        {
            double len = LoopLength(loops[i]);
            if (len > maxLen)
            {
                maxLen = len;
                outerIdx = i;
            }
        }

        Polygon3? outer = LoopToPolygon(loops[outerIdx]);
        if (outer == null)
            return null;

        var holes = new List<Polygon3>();
        for (int i = 0; i < loops.Count; i++)
        {
            if (i == outerIdx)
                continue;
            Polygon3? hole = LoopToPolygon(loops[i]);
            if (hole != null)
                holes.Add(hole);
        }
        return new OccluderFace(outer, holes, Units.DirToVec3(face.FaceNormal));
    }

    private static void AppendTessellatedFace(Face face, List<OccluderFace> faces)
    {
        Mesh mesh = face.Triangulate(CurvedFaceLod);
        for (int i = 0; i < mesh.NumTriangles; i++)
        {
            MeshTriangle t = mesh.get_Triangle(i);
            Vec3 a = Units.ToVec3(t.get_Vertex(0));
            Vec3 b = Units.ToVec3(t.get_Vertex(1));
            Vec3 c = Units.ToVec3(t.get_Vertex(2));
            Vec3 normal = (b - a).Cross(c - a);
            Vec3? n = normal.Length > 1e-12 ? normal.Normalized() : null;
            faces.Add(new OccluderFace(new Polygon3(a, b, c), null, n));
        }
    }

    private static Polygon3? LoopToPolygon(CurveLoop loop)
    {
        var pts = new List<Vec3>();
        foreach (Curve c in loop)
        {
            // Tessellate every curve; lines return their 2 endpoints, arcs/splines a polyline.
            IList<XYZ> tess = c.Tessellate();
            // Drop the last point of each curve to avoid duplicating shared vertices.
            for (int i = 0; i < tess.Count - 1; i++)
                pts.Add(Units.ToVec3(tess[i]));
        }
        return pts.Count >= 3 ? new Polygon3(pts) : null;
    }

    private static double LoopLength(CurveLoop loop)
    {
        double len = 0;
        foreach (Curve c in loop)
            len += c.ApproximateLength;
        return len;
    }
}
