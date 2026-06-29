using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Revit.Parameters;

namespace SolarShading.Revit.Commands;

/// <summary>
/// One-click setup: creates the add-in's shared parameters (with fixed GUIDs) and binds them
/// to the relevant categories, so the model is prepared up front instead of relying on implicit
/// creation during analysis. Safe to run repeatedly.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class SetupParametersCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;

        using (var t = new Transaction(doc, "Solar Shading — setup parameters"))
        {
            t.Start();
            ShadingFlag.EnsureBound(doc, app);          // SS_SHADING_DEVICE → many categories
            doc.Regenerate();
            ResultParameters.EnsureBound(doc, app, new[] { BuiltInCategory.OST_Windows }); // SS_* results → Windows
            t.Commit();
        }

        TaskDialog.Show("Solar Shading",
            "Shared parameters are ready:\n\n" +
            "• SS_SHADING_DEVICE (Yes/No) — bound to model categories (walls, generic models, framing…)\n" +
            "• SS_EXTERNAL_SC2 (number) — bound to Windows\n" +
            "• SS_SHADED_MARCH / JUNE / DEC (text) — bound to Windows\n\n" +
            $"Shared parameter file:\n{app.SharedParametersFilename}");
        return Result.Succeeded;
    }
}
