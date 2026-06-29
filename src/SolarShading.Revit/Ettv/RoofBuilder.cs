using Autodesk.Revit.DB;
using SolarShading.Core.Ettv;
using SolarShading.Revit.Config;
using Units = SolarShading.Revit.Geometry.Units;

namespace SolarShading.Revit.Ettv;

/// <summary>
/// Aggregates the model's roofs and skylights into a single <see cref="RoofData"/> for the
/// RTTV check. Skylights are fenestration hosted in a roof. Roof U-value comes from the config
/// (per-element thermal extraction is a later refinement).
/// </summary>
public static class RoofBuilder
{
    public static RoofData? Build(Document doc, AnalysisConfig cfg)
    {
        var roofs = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Roofs)
            .WhereElementIsNotElementType()
            .ToList();
        if (roofs.Count == 0)
            return null;

        double roofAreaM2 = 0;
        foreach (Element roof in roofs)
        {
            double ft2 = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0.0;
            roofAreaM2 += ft2 * Units.SqFeetToSqMeters;
        }
        if (roofAreaM2 < 1e-6)
            return null;

        // Skylights = windows whose host is a roof.
        double skylightAreaM2 = 0;
        var skylights = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType()
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .Where(w => w.Host is RoofBase);
        foreach (FamilyInstance s in skylights)
        {
            double ft2 = s.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0.0;
            skylightAreaM2 += ft2 * Units.SqFeetToSqMeters;
        }

        return new RoofData
        {
            GrossRoofAreaM2 = roofAreaM2,
            SkylightAreaM2 = skylightAreaM2,
            RoofUValue = cfg.RoofUValue,
            SkylightGlazing = cfg.Glazing,
        };
    }
}
