using Autodesk.Revit.DB;

namespace SolarShading.Revit.Geometry;

/// <summary>Applies or clears a colour override on elements in a view (for visual management).</summary>
public static class ElementColorizer
{
    /// <summary>Colour the elements (surface + edges) in the view, or pass null to clear the override.</summary>
    public static void Apply(Document doc, View view, IEnumerable<ElementId> ids, Color? color)
    {
        OverrideGraphicSettings ogs = new();
        if (color != null)
        {
            ogs.SetProjectionLineColor(color);
            FillPatternElement? solid = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill);
            if (solid != null)
            {
                ogs.SetSurfaceForegroundPatternId(solid.Id);
                ogs.SetSurfaceForegroundPatternColor(color);
            }
        }
        foreach (ElementId id in ids)
        {
            try { view.SetElementOverrides(id, ogs); }
            catch { /* view may not support overrides for this element */ }
        }
    }

    /// <summary>Distinct colour used to highlight tagged shading devices.</summary>
    public static Color ShadingDevice => new(255, 127, 0); // orange
}
