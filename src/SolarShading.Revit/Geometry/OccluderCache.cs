using Autodesk.Revit.DB;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>
/// Caches extracted occluder geometry by element id (T1). A shading device near several
/// windows is otherwise extracted once per window; extraction is the dominant Revit-API
/// cost, so caching it across the whole run is a large saving. Extraction must happen on
/// the Revit thread; the cached <see cref="OccluderObject"/>s are pure data and safe to
/// read from the parallel compute phase.
/// </summary>
public sealed class OccluderCache
{
    private readonly Document _doc;
    private readonly Dictionary<ElementId, OccluderObject?> _cache = new();

    public OccluderCache(Document doc) => _doc = doc;

    public OccluderObject? Get(ElementId id)
    {
        if (_cache.TryGetValue(id, out OccluderObject? cached))
            return cached;
        Element? e = _doc.GetElement(id);
        OccluderObject? built = e != null ? RevitGeometryExtractor.ToOccluder(e) : null;
        _cache[id] = built;
        return built;
    }

    /// <summary>Resolve a set of devices to their cached occluders (nulls dropped).</summary>
    public IReadOnlyList<OccluderObject> Get(IEnumerable<Element> devices)
    {
        var list = new List<OccluderObject>();
        foreach (Element d in devices)
        {
            OccluderObject? occ = Get(d.Id);
            if (occ != null)
                list.Add(occ);
        }
        return list;
    }
}
