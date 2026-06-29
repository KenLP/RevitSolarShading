using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Core.Geometry;
using SolarShading.Revit.Engine;
using SolarShading.Revit.Geometry;
using SolarShading.Revit.Solar;
using SolarShading.Revit.UI;
using Units = SolarShading.Revit.Geometry.Units;

namespace SolarShading.Revit.Commands;

/// <summary>
/// Projects the selected Mass element(s) onto the ground plane for a chosen date and time,
/// draws the cast shadow as a coloured DirectShape and reports its area.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class BuildingShadowOnGroundCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;

        // Restrict to Mass elements — keeps the building-shadow tool simple and predictable.
        var masses = new List<Element>();
        foreach (ElementId id in uidoc.Selection.GetElementIds())
        {
            Element e = doc.GetElement(id);
            if (e?.Category?.BuiltInCategory == BuiltInCategory.OST_Mass)
                masses.Add(e);
        }
        if (masses.Count == 0)
        {
            TaskDialog.Show("Solar Shading", "Select one or more Mass elements, then run again.");
            return Result.Cancelled;
        }

        var dialog = new BuildingShadowWindow();
        if (dialog.ShowDialog() != true)
            return Result.Cancelled;

        // Collect occluder faces and the combined bounding box of the selection.
        var occluders = new List<OccluderFace>();
        BoundingBoxXYZ? combined = null;
        foreach (Element e in masses)
        {
            occluders.AddRange(RevitGeometryExtractor.ToOccluderFaces(e));
            combined = Union(combined, e.get_BoundingBox(null));
        }

        if (occluders.Count == 0 || combined == null)
        {
            TaskDialog.Show("Solar Shading", "Selected mass(es) have no usable solid geometry.");
            return Result.Cancelled;
        }

        // Ground plane at the base of the selection; receiver outline a generous square.
        double groundZ = combined.Min.Z * Units.FeetToMeters;
        var centre = (combined.Min + combined.Max) * 0.5;
        double extentFeet = Math.Max(
            Math.Max(combined.Max.X - combined.Min.X, combined.Max.Y - combined.Min.Y),
            combined.Max.Z - combined.Min.Z);
        double half = (extentFeet * 3.0 + 10.0) * Units.FeetToMeters;
        double cx = centre.X * Units.FeetToMeters, cy = centre.Y * Units.FeetToMeters;

        var ground = Plane3.FromFrame(new Vec3(cx, cy, groundZ), Vec3.UnitZ, Vec3.UnitX);
        var outline = new Polygon3(
            new Vec3(cx - half, cy - half, groundZ),
            new Vec3(cx + half, cy - half, groundZ),
            new Vec3(cx + half, cy + half, groundZ),
            new Vec3(cx - half, cy + half, groundZ));

        var instant = new DateTimeOffset(dialog.Date.Year, dialog.Date.Month, dialog.Date.Day,
            dialog.Hour, 0, 0, TimeSpan.FromHours(doc.SiteLocation.TimeZone));
        ModelSun sun = new RevitShadeEngine(doc).SunAt(instant);

        if (!sun.IsDaytime)
        {
            TaskDialog.Show("Solar Shading",
                $"The sun is below the horizon at {instant:dd MMM} {dialog.Hour:00}:00.");
            return Result.Cancelled;
        }

        var calc = new ShadingCalculator();
        (ShadeResult result, var region) = calc.ComputeRegion(outline, ground, occluders, sun.ToSun);

        using (var t = new Transaction(doc, "Building shadow on ground"))
        {
            t.Start();
            // Remove a previous building-shadow overlay so re-runs don't stack.
            ShadowVisualizer.ClearOverlays(doc, ShadowVisualizer.BuildingShadowTag);
            DirectShape? ds = ShadowVisualizer.CreateOverlay(
                doc, ground, region, BuiltInCategory.OST_GenericModel,
                ShadowVisualizer.DefaultGapMeters, ShadowVisualizer.BuildingShadowTag);
            if (ds != null)
                ElementColorizer.Apply(doc, doc.ActiveView, new[] { ds.Id }, ElementColorizer.ShadingDevice);
            t.Commit();
        }

        TaskDialog.Show("Solar Shading",
            $"{instant:dd MMM} {dialog.Hour:00}:00 — sun altitude {sun.AltitudeDeg:0.0}°\n" +
            $"Cast shadow area on ground: {result.ShadedAreaM2:0.0} m²");
        return Result.Succeeded;
    }

    private static BoundingBoxXYZ? Union(BoundingBoxXYZ? a, BoundingBoxXYZ? b)
    {
        if (b == null)
            return a;
        if (a == null)
            return b;
        return new BoundingBoxXYZ
        {
            Min = new XYZ(Math.Min(a.Min.X, b.Min.X), Math.Min(a.Min.Y, b.Min.Y), Math.Min(a.Min.Z, b.Min.Z)),
            Max = new XYZ(Math.Max(a.Max.X, b.Max.X), Math.Max(a.Max.Y, b.Max.Y), Math.Max(a.Max.Z, b.Max.Z)),
        };
    }
}
