using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SolarShading.Revit.Config;
using SolarShading.Revit.Engine;
using SolarShading.Revit.UI;

namespace SolarShading.Revit.Commands;

/// <summary>
/// Computes shaded area / external shading coefficient (SC2) for every window from nearby
/// shading devices and self-shading fins, evaluates the Singapore BCA ETTV, and reports
/// pass/fail. Analysis is read-only (sun computed analytically); results are written in a
/// single transaction. The heavy lifting lives in <see cref="ShadingRunner"/>.
/// </summary>
[Transaction(TransactionMode.Manual)]
public sealed class ShadingOnWindowsCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        UIApplication uiapp = commandData.Application;
        UIDocument uidoc = uiapp.ActiveUIDocument;
        ICollection<ElementId> preSelected = uidoc.Selection.GetElementIds();

        var config = new AnalysisConfig { Year = DateTime.Today.Year };
        var dialog = new ShadingConfigWindow(config);
        if (dialog.ShowDialog() != true)
            return Result.Cancelled;

        RunSummary summary = ShadingRunner.Run(uiapp, config, preSelected);
        if (!summary.Ok)
        {
            TaskDialog.Show("Solar Shading", summary.Message);
            return Result.Cancelled;
        }

        string csvNote = summary.CsvPath != null ? $"\nCSV: {summary.CsvPath}" : "";
        if (summary.ReportPath != null)
            csvNote += $"\nReport: {summary.ReportPath}";
        if (config.ShowShadowOverlay)
        {
            string date = summary.OverlayTime.ToString("dd MMM");
            string msg;
            if (config.WholeDaySweep)
                msg = summary.OverlaysDrawn > 0
                    ? $"Whole-day sweep on {date}: drew {summary.OverlaysDrawn} hourly shadow layer(s) " +
                      $"on up to {config.MaxSweepWindows} window(s), blue (morning) → red (afternoon).{csvNote}"
                    : $"Whole-day sweep on {date}: no shadows for the selected window(s). " +
                      $"Select the windows first, or try another date.{csvNote}";
            else
                msg = summary.OverlaysDrawn > 0
                    ? $"Analyzed {summary.WindowsAnalyzed} window(s).\nDrew {summary.OverlaysDrawn} red overlay(s) " +
                      $"on the glass at {summary.OverlayTime:dd MMM HH:mm}.{csvNote}"
                    : $"Analyzed {summary.WindowsAnalyzed} window(s), but none was shaded at " +
                      $"{summary.OverlayTime:dd MMM HH:mm}. Check the SS_ parameters / CSV for the full series.{csvNote}";
            TaskDialog.Show("Solar Shading", msg);
        }

        if (summary.Envelope != null)
            new EttvResultWindow(summary.Envelope, summary.Facades, summary.WindowsAnalyzed).ShowDialog();
        return Result.Succeeded;
    }
}
