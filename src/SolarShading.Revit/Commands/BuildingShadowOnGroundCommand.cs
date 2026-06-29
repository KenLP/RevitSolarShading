using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Core.Geometry;
using SolarShading.Revit.Engine;
using SolarShading.Revit.Geometry;
using SolarShading.Revit.Solar;
using Units = SolarShading.Revit.Geometry.Units;

namespace SolarShading.Revit.Commands;

/// <summary>
/// Projects the selected building element(s) onto the ground plane for the current
/// site sun (today, 12:00 local by default), draws the cast shadow as a DirectShape and
/// reports its area. Demonstrates the same core engine on the "building-on-ground" case.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class BuildingShadowOnGroundCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;

        ICollection<ElementId> selected = uidoc.Selection.GetElementIds();
        if (selected.Count == 0)
        {
            TaskDialog.Show("Solar Shading", "Select the building element(s) to cast a ground shadow, then run again.");
            return Result.Cancelled;
        }

        // Collect occluder faces and the combined bounding box of the selection.
        var occluders = new List<OccluderFace>();
        BoundingBoxXYZ? combined = null;
        foreach (ElementId id in selected)
        {
            Element e = doc.GetElement(id);
            if (e == null)
                continue;
            occluders.AddRange(RevitGeometryExtractor.ToOccluderFaces(e));
            combined = Union(combined, e.get_BoundingBox(null));
        }

        if (occluders.Count == 0 || combined == null)
        {
            TaskDialog.Show("Solar Shading", "Selected element(s) have no usable solid geometry.");
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

        ModelSun sun = new RevitShadeEngine(doc).SunAt(
            new DateTimeOffset(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 12, 0, 0,
                TimeSpan.FromHours(doc.SiteLocation.TimeZone)));

        if (!sun.IsDaytime)
        {
            TaskDialog.Show("Solar Shading", "The sun is below the horizon at the selected time.");
            return Result.Cancelled;
        }

        var calc = new ShadingCalculator();
        (ShadeResult result, var region) = calc.ComputeRegion(outline, ground, occluders, sun.ToSun);

        using (var t = new Transaction(doc, "Building shadow on ground"))
        {
            t.Start();
            ShadowVisualizer.CreateOverlay(doc, ground, region, BuiltInCategory.OST_GenericModel);
            t.Commit();
        }

        TaskDialog.Show("Solar Shading",
            $"Sun altitude: {sun.AltitudeDeg:0.0}°\nCast shadow area on ground: {result.ShadedAreaM2:0.0} m²");
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
