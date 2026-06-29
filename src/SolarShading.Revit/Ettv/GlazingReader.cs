using Autodesk.Revit.DB;
using SolarShading.Core.Ettv;

namespace SolarShading.Revit.Ettv;

/// <summary>
/// Reads the thermal properties of a window's glazing from the model — the analytic
/// U-value and Solar Heat Gain Coefficient on the window instance/type — so ETTV uses
/// each element's real glass instead of one project-wide default. Missing values fall
/// back to the supplied default glazing.
///
/// Parameters are looked up by their display names rather than a BuiltInParameter enum,
/// because the analytic-property enum members differ between Revit versions; name lookup
/// (plus a data-type check for unit conversion) is version-independent.
/// </summary>
public static class GlazingReader
{
    // SC1 is referenced to 3 mm clear glass (SHGC ≈ 0.87): SC = SHGC / 0.87.
    private const double ClearGlassShgc = 0.87;

    private static readonly string[] UNames =
        { "Heat Transfer Coefficient (U)", "Analytic Heat Transfer Coefficient", "U-Value", "U", "Uf" };
    private static readonly string[] ShgcNames =
        { "Solar Heat Gain Coefficient", "Analytic Solar Heat Gain Coefficient", "SHGC" };

    public static Glazing Read(FamilyInstance window, Glazing fallback)
    {
        Element? type = window.Document.GetElement(window.GetTypeId());
        double u = ReadUValue(window, type) ?? fallback.UValue;
        double sc1 = ReadShadingCoefficient(window, type) ?? fallback.ShadingCoefficient;
        return new Glazing("Model glazing", u, sc1);
    }

    private static double? ReadUValue(FamilyInstance inst, Element? type)
    {
        Parameter? p = Find(inst, type, UNames);
        if (p is not { HasValue: true })
            return null;
        double raw = p.AsDouble();
        if (raw <= 0)
            return null;
        // Convert only when the parameter really is a heat-transfer coefficient; a plain
        // numeric override is assumed already in W/m²·K.
        return IsHeatTransferCoefficient(p)
            ? UnitUtils.ConvertFromInternalUnits(raw, UnitTypeId.WattsPerSquareMeterKelvin)
            : raw;
    }

    private static double? ReadShadingCoefficient(FamilyInstance inst, Element? type)
    {
        Parameter? p = Find(inst, type, ShgcNames);
        if (p is { HasValue: true })
        {
            double shgc = p.AsDouble();
            if (shgc > 0)
                return shgc / ClearGlassShgc;
        }
        return null;
    }

    private static Parameter? Find(FamilyInstance inst, Element? type, string[] names)
    {
        foreach (string name in names)
        {
            Parameter? p = inst.LookupParameter(name) ?? type?.LookupParameter(name);
            if (p is { HasValue: true } && p.StorageType == StorageType.Double)
                return p;
        }
        return null;
    }

    private static bool IsHeatTransferCoefficient(Parameter p)
    {
        try
        {
            return p.Definition.GetDataType().Equals(SpecTypeId.HeatTransferCoefficient);
        }
        catch
        {
            return false;
        }
    }
}
