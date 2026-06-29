using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Revit.Parameters;

namespace SolarShading.Revit.Commands;

/// <summary>
/// Manage which elements are shading devices: tag the current selection, untag it, or select
/// all tagged elements to review them. The tag is a Yes/No shared parameter, so it is saved in
/// the model and can be scheduled / filtered like any other parameter.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class GetShadingDevicesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;
        Autodesk.Revit.ApplicationServices.Application app = commandData.Application.Application;

        var tagged = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(ShadingFlag.IsShadingDevice)
            .Select(e => e.Id)
            .ToList();
        int selectedCount = uidoc.Selection.GetElementIds().Count;

        var td = new TaskDialog("Shading Devices")
        {
            MainInstruction = $"{tagged.Count} element(s) tagged as shading devices.",
            MainContent = $"{selectedCount} element(s) currently selected.",
            AllowCancellation = true,
        };
        td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Tag selected as shading devices",
            "Add the current selection to the shading-device set (saved in the model).");
        td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Untag selected",
            "Remove the current selection from the shading-device set.");
        td.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Select all tagged (review)",
            "Select every tagged shading device so you can review or zoom to them.");

        switch (td.Show())
        {
            case TaskDialogResult.CommandLink1:
                return SetFlag(doc, app, uidoc, true);
            case TaskDialogResult.CommandLink2:
                return SetFlag(doc, app, uidoc, false);
            case TaskDialogResult.CommandLink3:
                uidoc.Selection.SetElementIds(tagged);
                TaskDialog.Show("Shading Devices", $"Selected {tagged.Count} tagged shading device(s).");
                return Result.Succeeded;
            default:
                return Result.Cancelled;
        }
    }

    private static Result SetFlag(Document doc, Autodesk.Revit.ApplicationServices.Application app,
        UIDocument uidoc, bool value)
    {
        ICollection<ElementId> selected = uidoc.Selection.GetElementIds();
        if (selected.Count == 0)
        {
            TaskDialog.Show("Shading Devices", "Select the elements first, then run this command.");
            return Result.Cancelled;
        }

        int changed = 0;
        using (var t = new Transaction(doc, value ? "Tag shading devices" : "Untag shading devices"))
        {
            t.Start();
            if (value)
            {
                ShadingFlag.EnsureBound(doc, app);
                doc.Regenerate();
            }
            foreach (ElementId id in selected)
            {
                Element e = doc.GetElement(id);
                if (e?.LookupParameter(ShadingFlag.Name) == null)
                    continue;
                ShadingFlag.Set(e, value);
                changed++;
            }
            t.Commit();
        }

        TaskDialog.Show("Shading Devices",
            $"{(value ? "Tagged" : "Untagged")} {changed} element(s).");
        return Result.Succeeded;
    }
}
