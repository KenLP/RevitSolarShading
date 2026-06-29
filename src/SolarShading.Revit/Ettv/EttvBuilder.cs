using Autodesk.Revit.DB;
using SolarShading.Core.Ettv;
using SolarShading.Revit.Config;
using SolarShading.Revit.Engine;
using SolarShading.Revit.Geometry;
using Units = SolarShading.Revit.Geometry.Units;

namespace SolarShading.Revit.Ettv;

/// <summary>
/// Assembles per-orientation <see cref="FacadeData"/> from the model and the shading
/// analyses, so a building-level ETTV with pass/fail can be evaluated. Walls and windows
/// are grouped by their true-north orientation (project-north angle applied).
/// </summary>
public static class EttvBuilder
{
    public static IReadOnlyList<FacadeData> Build(
        Document doc, IReadOnlyList<WindowShadeAnalysis> analyses,
        IReadOnlyDictionary<ElementId, Glazing> glazingByWindow, AnalysisConfig cfg)
    {
        double northAngleDeg = doc.ActiveProjectLocation
            .GetProjectPosition(XYZ.Zero).Angle * 180.0 / Math.PI;

        var windowArea = new Dictionary<Orientation, double>();
        var windowSc2Weighted = new Dictionary<Orientation, double>();
        var windowUWeighted = new Dictionary<Orientation, double>();
        var windowSc1Weighted = new Dictionary<Orientation, double>();
        foreach (WindowShadeAnalysis a in analyses)
        {
            if (doc.GetElement(a.WindowId) is not FamilyInstance fi)
                continue;
            Orientation o = OrientationOf(fi.FacingOrientation, northAngleDeg);
            Glazing g = glazingByWindow.TryGetValue(a.WindowId, out Glazing? gz) ? gz : cfg.Glazing;
            Accumulate(windowArea, o, a.WindowAreaM2);
            Accumulate(windowSc2Weighted, o, a.EffectiveSc2 * a.WindowAreaM2);
            Accumulate(windowUWeighted, o, g.UValue * a.WindowAreaM2);
            Accumulate(windowSc1Weighted, o, g.ShadingCoefficient * a.WindowAreaM2);
        }

        var wallNetArea = new Dictionary<Orientation, double>();
        foreach (Wall wall in ExteriorWalls(doc))
        {
            double areaFt2 = wall.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0.0;
            if (areaFt2 <= 0)
                continue;
            Orientation o = OrientationOf(wall.Orientation, northAngleDeg);
            Accumulate(wallNetArea, o, areaFt2 * Units.SqFeetToSqMeters);
        }

        var facades = new List<FacadeData>();
        foreach (Orientation o in Enum.GetValues<Orientation>())
        {
            double win = windowArea.GetValueOrDefault(o);
            double wallNet = wallNetArea.GetValueOrDefault(o);
            double gross = wallNet + win;
            if (gross < 1e-6)
                continue;

            double sc2 = win > 1e-6 ? windowSc2Weighted.GetValueOrDefault(o) / win : 1.0;
            Glazing glazing = win > 1e-6
                ? new Glazing("Mixed (model)",
                    windowUWeighted.GetValueOrDefault(o) / win,
                    windowSc1Weighted.GetValueOrDefault(o) / win)
                : cfg.Glazing;
            facades.Add(new FacadeData
            {
                Orientation = o,
                GrossWallAreaM2 = gross,
                WindowAreaM2 = win,
                WallUValue = cfg.WallUValue,
                Glazing = glazing,
                ExternalShadingCoefficient = sc2,
            });
        }
        return facades;
    }

    private static IEnumerable<Wall> ExteriorWalls(Document doc)
    {
        var walls = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Walls)
            .WhereElementIsNotElementType()
            .Cast<Wall>()
            .Where(w => w.WallType.Kind != WallKind.Curtain)
            .ToList();

        var exterior = walls.Where(w => w.WallType.Function == WallFunction.Exterior).ToList();
        return exterior.Count > 0 ? exterior : walls; // fall back to all basic walls
    }

    private static Orientation OrientationOf(XYZ modelDir, double northAngleDeg)
    {
        double modelAz = Math.Atan2(modelDir.X, modelDir.Y) * 180.0 / Math.PI; // CW from +Y
        double trueAz = modelAz - northAngleDeg;
        return OrientationExtensions.FromAzimuth(trueAz);
    }

    private static void Accumulate(Dictionary<Orientation, double> map, Orientation o, double value)
        => map[o] = map.GetValueOrDefault(o) + value;
}
