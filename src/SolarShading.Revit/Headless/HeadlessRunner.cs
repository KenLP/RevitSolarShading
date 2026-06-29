using System.IO;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using SolarShading.Revit.Config;
using SolarShading.Revit.Engine;

namespace SolarShading.Revit.Headless;

/// <summary>
/// Lets an external driver run the shading analysis without clicking the ribbon: on each
/// Revit idle tick it checks for a trigger JSON file; if present it runs the analysis on the
/// active document and writes a result JSON. The check is a cheap File.Exists, so when no
/// trigger is queued there is no behaviour change. Intended for automated testing/tuning.
/// </summary>
public static class HeadlessRunner
{
    // Use LocalApplicationData (deterministic) rather than Path.GetTempPath(): Revit
    // redirects TEMP to a per-session GUID folder, so a temp path never matches between
    // the add-in process and an external driver. LocalApplicationData is stable.
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SolarShading");
    public static readonly string TriggerPath = Path.Combine(Dir, "trigger.json");
    public static readonly string ResultPath = Path.Combine(Dir, "result.json");

    public static void OnIdling(object? sender, IdlingEventArgs e)
    {
        if (!File.Exists(TriggerPath))
            return;
        // Wait (without consuming the trigger) until a document is actually open, so a
        // trigger queued before the model loads runs once the model is ready.
        if (sender is not UIApplication uiapp || uiapp.ActiveUIDocument == null)
            return;

        string json;
        try
        {
            json = File.ReadAllText(TriggerPath);
            File.Delete(TriggerPath); // consume once, even if the run throws
        }
        catch
        {
            return;
        }

        try
        {
            HeadlessConfig hc = HeadlessConfig.Parse(json);
            AnalysisConfig config = hc.ToAnalysisConfig();
            ICollection<ElementId> selected = hc.SelectedIds is { Length: > 0 }
                ? hc.SelectedIds.Select(id => new ElementId(id)).ToList()
                : uiapp.ActiveUIDocument.Selection.GetElementIds();

            RunSummary summary = ShadingRunner.Run(uiapp, config, selected);
            WriteResult(ToResult(summary));
        }
        catch (Exception ex)
        {
            WriteResult(new { ok = false, message = ex.GetType().Name + ": " + ex.Message });
        }
    }

    private static object ToResult(RunSummary s) => new
    {
        ok = s.Ok,
        message = s.Message,
        windowsAnalyzed = s.WindowsAnalyzed,
        overlaysDrawn = s.OverlaysDrawn,
        overlayTime = s.OverlayTime.ToString("yyyy-MM-dd HH:mm"),
        code = s.Envelope?.Code,
        envelopeEttv = s.Envelope?.Ettv,
        threshold = s.Envelope?.Threshold,
        passes = s.Envelope?.Passes,
        rttv = s.Rttv is { Applicable: true } r ? new { value = Math.Round(r.Rttv, 2), threshold = r.Threshold, passes = r.Passes } : null,
        csvPath = s.CsvPath,
        reportPath = s.ReportPath,
        perWindow = s.Analyses.Select(a => new
        {
            id = a.WindowId.Value,
            sc2 = Math.Round(a.EffectiveSc2, 4),
            windowAreaM2 = Math.Round(a.WindowAreaM2, 3),
            litShadedM2 = a.Instants.Where(i => i.Lit).Select(i => Math.Round(i.ShadedAreaM2, 3)).ToArray(),
        }).ToArray(),
        facades = s.Facades.Select(f => new
        {
            orientation = f.Orientation.ToString(),
            windowAreaM2 = Math.Round(f.WindowAreaM2, 2),
            sc2 = Math.Round(f.ExternalShadingCoefficient, 4),
        }).ToArray(),
    };

    private static void WriteResult(object result)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(ResultPath,
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // best effort
        }
    }
}
