using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Revit.Parameters;

namespace SolarShading.Revit.Commands;

/// <summary>Tags the currently selected elements as shading devices for the shadow computation.</summary>
[Transaction(TransactionMode.Manual)]
public sealed class GetShadingDevicesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;

        ICollection<ElementId> selected = uidoc.Selection.GetElementIds();
        if (selected.Count == 0)
        {
            TaskDialog.Show("Solar Shading",
                "Select the shading devices (overhangs, fins, ledges) first, then run this command.");
            return Result.Cancelled;
        }

        int tagged = 0;
        using (var t = new Transaction(doc, "Tag shading devices"))
        {
            t.Start();
            ShadingFlag.EnsureBound(doc, app);
            doc.Regenerate();
            foreach (ElementId id in selected)
            {
                Element e = doc.GetElement(id);
                if (e?.LookupParameter(ShadingFlag.Name) == null)
                    continue;
                ShadingFlag.Set(e, true);
                tagged++;
            }
            t.Commit();
        }

        TaskDialog.Show("Solar Shading", $"Tagged {tagged} element(s) as shading devices.");
        return Result.Succeeded;
    }
}
