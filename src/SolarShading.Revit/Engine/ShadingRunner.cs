using System.Diagnostics;
using System.Globalization;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Core.Ettv;
using SolarShading.Core.Reporting;
using SolarShading.Revit.Config;
using SolarShading.Revit.Ettv;
using SolarShading.Revit.Export;
using SolarShading.Revit.Geometry;
using SolarShading.Revit.Parameters;

namespace SolarShading.Revit.Engine;

/// <summary>Outcome of a shading run, consumed by both the ribbon UI and the headless runner.</summary>
public sealed class RunSummary
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public int WindowsAnalyzed { get; init; }
    public int OverlaysDrawn { get; init; }
    public DateTimeOffset OverlayTime { get; init; }
    public string? CsvPath { get; init; }
    public string? ReportPath { get; init; }
    public IReadOnlyList<WindowShadeAnalysis> Analyses { get; init; } = Array.Empty<WindowShadeAnalysis>();
    public EnvelopeEttvResult? Envelope { get; init; }
    public EnvelopeRttvResult? Rttv { get; init; }
    public IReadOnlyList<FacadeData> Facades { get; init; } = Array.Empty<FacadeData>();

    public static RunSummary Fail(string message) => new() { Ok = false, Message = message };
}

/// <summary>
/// The headless shading + ETTV computation: collect windows, analyze (cache + parallel),
/// evaluate ETTV, and write results / overlays in one transaction. Contains NO UI, so it
/// is shared by the ribbon command and the file-triggered headless runner.
/// </summary>
public static class ShadingRunner
{
    public const int Day = 21;
    private const double SearchOffsetFeet = 4000.0 / 304.8;

    public static RunSummary Run(UIApplication uiapp, AnalysisConfig config, ICollection<ElementId> preSelected)
    {
        Document doc = uiapp.ActiveUIDocument.Document;
        Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;

        var windows = new FilteredElementCollector(doc)
            .OfCategory(BuiltInCategory.OST_Windows)
            .WhereElementIsNotElementType()
            .OfClass(typeof(FamilyInstance))
            .Cast<FamilyInstance>()
            .ToList();
        if (windows.Count == 0)
            return RunSummary.Fail("No windows found in the model.");

        var shadingIds = new FilteredElementCollector(doc)
            .WhereElementIsNotElementType()
            .Where(ShadingFlag.IsShadingDevice)
            .Select(e => e.Id)
            .ToList();

        var tz = TimeSpan.FromHours(doc.SiteLocation.TimeZone);
        List<DateTimeOffset> instants = BuildInstants(config, tz);
        if (instants.Count == 0)
            return RunSummary.Fail("Select at least one reference date.");

        var engine = new RevitShadeEngine(doc);
        var inputs = new List<ShadeInput>();
        var nearbyByWindow = new Dictionary<ElementId, List<Element>>();
        foreach (FamilyInstance window in windows)
        {
            List<Element> near = NearbyShading(doc, window, shadingIds);
            nearbyByWindow[window.Id] = near;
            inputs.Add(new ShadeInput(window, near));
        }

        IReadOnlyList<WindowShadeAnalysis> analyses = engine.AnalyzeBatch(inputs, instants);
        if (analyses.Count == 0)
            return RunSummary.Fail("No window produced a valid analysis.");

        var windowById = windows.ToDictionary(w => w.Id);
        var glazingByWindow = new Dictionary<ElementId, Glazing>();
        foreach (WindowShadeAnalysis a in analyses)
            if (windowById.TryGetValue(a.WindowId, out FamilyInstance? fi))
                glazingByWindow[a.WindowId] = GlazingReader.Read(fi, config.Glazing);

        IReadOnlyList<FacadeData> facades = EttvBuilder.Build(doc, analyses, glazingByWindow, config);
        EnvelopeEttvResult envelope = new EttvAssessment(config.Profile).EvaluateEnvelope(facades);

        // Roof RTTV (if the model has roofs and the code defines a roof part).
        RoofData? roof = RoofBuilder.Build(doc, config);
        EnvelopeRttvResult? rttv = roof != null
            ? RoofAssessment.Evaluate(new[] { roof }, config.Profile)
            : null;

        int overlaysDrawn = 0;
        var overlayTime = new DateTimeOffset(config.Year, config.OverlayMonth, Day, config.OverlayHour, 0, 0, tz);
        if (config.WriteParameters || config.ShowShadowOverlay)
        {
            using var t = new Transaction(doc, "Solar Shading results");
            t.Start();
            if (config.WriteParameters)
            {
                ResultParameters.EnsureBound(doc, app, new[] { BuiltInCategory.OST_Windows });
                doc.Regenerate();
                foreach (WindowShadeAnalysis a in analyses)
                    WriteResults(doc, a);
            }
            if (config.ShowShadowOverlay)
                overlaysDrawn = config.WholeDaySweep
                    ? DrawDaySweep(doc, engine, windows, nearbyByWindow, preSelected, config, tz)
                    : DrawOverlay(doc, engine, windows, nearbyByWindow, overlayTime);
            t.Commit();
        }

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string? csvPath = null;
        if (config.ExportCsv)
        {
            csvPath = Path.Combine(desktop, $"SolarShading_{stamp}.csv");
            CsvExporter.Write(csvPath, analyses);
        }

        string? reportPath = null;
        if (config.GenerateReport)
        {
            string html = ComplianceReport.BuildHtml(new ReportData
            {
                ProjectName = string.IsNullOrWhiteSpace(doc.Title) ? "(untitled)" : doc.Title,
                GeneratedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                Profile = config.Profile,
                Ettv = envelope,
                Facades = facades,
                Rttv = rttv,
            });
            reportPath = Path.Combine(desktop, $"SolarShading_Report_{stamp}.html");
            File.WriteAllText(reportPath, html);
            try { Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true }); }
            catch { /* report still on disk */ }
        }

        return new RunSummary
        {
            Ok = true,
            WindowsAnalyzed = analyses.Count,
            OverlaysDrawn = overlaysDrawn,
            OverlayTime = overlayTime,
            CsvPath = csvPath,
            ReportPath = reportPath,
            Analyses = analyses,
            Envelope = envelope,
            Rttv = rttv,
            Facades = facades,
        };
    }

    private static List<DateTimeOffset> BuildInstants(AnalysisConfig cfg, TimeSpan tz)
    {
        var list = new List<DateTimeOffset>();
        foreach (int month in cfg.Months())
            for (int h = cfg.StartHour; h <= cfg.EndHour; h++)
                list.Add(new DateTimeOffset(cfg.Year, month, Day, h, 0, 0, tz));
        return list;
    }

    private static List<Element> NearbyShading(Document doc, Element window, ICollection<ElementId> shadingIds)
    {
        BoundingBoxXYZ? bb = window.get_BoundingBox(null);
        if (bb == null || shadingIds.Count == 0)
            return new List<Element>();
        var offset = new XYZ(SearchOffsetFeet, SearchOffsetFeet, SearchOffsetFeet);
        var outline = new Outline(bb.Min - offset, bb.Max + offset);
        return new FilteredElementCollector(doc, shadingIds.ToList())
            .WherePasses(new BoundingBoxIntersectsFilter(outline))
            .Where(e => e.Id != window.Id)
            .ToList();
    }

    private static int DrawOverlay(
        Document doc, RevitShadeEngine engine, List<FamilyInstance> windows,
        Dictionary<ElementId, List<Element>> nearbyByWindow, DateTimeOffset instant)
    {
        var created = new List<ElementId>();
        foreach (FamilyInstance window in windows)
        {
            if (!nearbyByWindow.TryGetValue(window.Id, out List<Element>? near))
                continue;
            var region = engine.RegionAt(window, near, instant);
            if (region is { } r && r.Region.Count > 0)
            {
                DirectShape? ds = ShadowVisualizer.CreateOverlay(
                    doc, r.Receiver.Plane, r.Region, BuiltInCategory.OST_GenericModel);
                if (ds != null)
                    created.Add(ds.Id);
            }
        }
        if (created.Count > 0)
            ShadowVisualizer.PaintRed(doc, doc.ActiveView, created);
        return created.Count;
    }

    private static int DrawDaySweep(
        Document doc, RevitShadeEngine engine, List<FamilyInstance> windows,
        Dictionary<ElementId, List<Element>> nearbyByWindow, ICollection<ElementId> selectedIds,
        AnalysisConfig cfg, TimeSpan tz)
    {
        List<FamilyInstance> shaded = windows.Where(w => nearbyByWindow.ContainsKey(w.Id)).ToList();
        List<FamilyInstance> targets = shaded.Where(w => selectedIds.Contains(w.Id)).ToList();
        if (targets.Count == 0)
            targets = shaded;
        targets = targets.Take(cfg.MaxSweepWindows).ToList();

        int total = 0;
        foreach (FamilyInstance window in targets)
        {
            List<Element> near = nearbyByWindow[window.Id];
            for (int h = cfg.StartHour; h <= cfg.EndHour; h++)
            {
                var instant = new DateTimeOffset(cfg.Year, cfg.OverlayMonth, Day, h, 0, 0, tz);
                var region = engine.RegionAt(window, near, instant);
                if (region is not { } r || r.Region.Count == 0)
                    continue;

                double gap = ShadowVisualizer.DefaultGapMeters + (h - cfg.StartHour) * 0.05;
                DirectShape? ds = ShadowVisualizer.CreateOverlay(
                    doc, r.Receiver.Plane, r.Region, BuiltInCategory.OST_GenericModel, gap);
                if (ds == null)
                    continue;

                ShadowVisualizer.Paint(doc, doc.ActiveView, new[] { ds.Id },
                    ShadowVisualizer.HourColor(h, cfg.StartHour, cfg.EndHour));
                total++;
            }
        }
        return total;
    }

    private static void WriteResults(Document doc, WindowShadeAnalysis a)
    {
        Element? w = doc.GetElement(a.WindowId);
        if (w == null)
            return;
        w.LookupParameter(ResultParameters.Sc2)?.Set(a.EffectiveSc2);
        SetSeries(w, ResultParameters.ShadedSeriesMarch, a, 3);
        SetSeries(w, ResultParameters.ShadedSeriesJune, a, 6);
        SetSeries(w, ResultParameters.ShadedSeriesDec, a, 12);
    }

    private static void SetSeries(Element w, string paramName, WindowShadeAnalysis a, int month)
    {
        IEnumerable<string> values = a.Instants
            .Where(s => s.Instant.Month == month)
            .OrderBy(s => s.Instant.Hour)
            .Select(s => s.Lit
                ? s.ShadedAreaM2.ToString("0.###", CultureInfo.InvariantCulture)
                : "NA");
        string joined = string.Join(";", values);
        if (!string.IsNullOrEmpty(joined))
            w.LookupParameter(paramName)?.Set(joined);
    }
}
