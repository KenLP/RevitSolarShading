using System.Globalization;
using System.Text;
using SolarShading.Core.Ettv;

namespace SolarShading.Core.Reporting;

/// <summary>Everything needed to render a compliance submission report.</summary>
public sealed class ReportData
{
    public required string ProjectName { get; init; }
    public required string GeneratedOn { get; init; }
    public required ComplianceProfile Profile { get; init; }
    public required EnvelopeEttvResult Ettv { get; init; }
    public required IReadOnlyList<FacadeData> Facades { get; init; }
    public EnvelopeRttvResult? Rttv { get; init; }
}

/// <summary>
/// Renders a self-contained, printable HTML compliance report (ETTV/OTTV + RTTV) for a chosen
/// code. HTML keeps the add-in dependency-free; the user prints it to PDF from the browser. Kept
/// in Core so the layout is unit-testable.
/// </summary>
public static class ComplianceReport
{
    public static string BuildHtml(ReportData d)
    {
        var byOrientation = d.Facades.ToDictionary(f => f.Orientation);
        bool pass = d.Ettv.Passes && (d.Rttv is not { Applicable: true } rt || rt.Passes);

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'><style>").Append(Css).Append("</style></head><body>");

        sb.Append("<h1>Envelope Thermal Transfer — Compliance Report</h1>");
        sb.Append("<table class='meta'>")
          .Append(Row("Project", Esc(d.ProjectName)))
          .Append(Row("Code", Esc(d.Profile.Code) + " — " + Esc(d.Profile.Jurisdiction)))
          .Append(Row("Generated", Esc(d.GeneratedOn)))
          .Append("</table>");

        sb.Append($"<div class='banner {(pass ? "pass" : "fail")}'>{(pass ? "PASS" : "FAIL")} — ")
          .Append($"{d.Profile.Code}: ETTV/OTTV {F1(d.Ettv.Ettv)} W/m² (limit {F0(d.Ettv.Threshold)})");
        if (d.Rttv is { Applicable: true } r)
            sb.Append($" · RTTV {F1(r.Rttv)} W/m² (limit {F0(r.Threshold)})");
        sb.Append("</div>");

        // Façade breakdown
        sb.Append("<h2>Façade breakdown (ETTV/OTTV)</h2>");
        sb.Append("<table><tr><th>Orientation</th><th>Window m²</th><th>WWR %</th><th>SC2</th><th>ETTV W/m²</th></tr>");
        foreach (FacadeEttvResult f in d.Ettv.Facades)
        {
            FacadeData data = byOrientation[f.Orientation];
            sb.Append("<tr>")
              .Append(Td(f.Orientation.ToString()))
              .Append(Td(F1(data.WindowAreaM2)))
              .Append(Td(F0(f.WindowToWallRatio * 100)))
              .Append(Td(F2(data.ExternalShadingCoefficient)))
              .Append(Td(F1(f.Ettv)))
              .Append("</tr>");
        }
        sb.Append($"<tr class='total'><td colspan='4'>Envelope ETTV/OTTV (area-weighted)</td><td>{F1(d.Ettv.Ettv)}</td></tr>");
        sb.Append("</table>");

        // Roof
        if (d.Rttv is { Applicable: true } roof)
        {
            sb.Append("<h2>Roof (RTTV)</h2>");
            sb.Append("<table>")
              .Append(Row("RTTV", $"{F1(roof.Rttv)} W/m² (limit {F0(roof.Threshold)}) — {(roof.Passes ? "PASS" : "FAIL")}"))
              .Append(Row("Skylight-to-roof ratio", F0(roof.SkylightRoofRatio * 100) + " %"))
              .Append("</table>");
        }

        sb.Append("<h2>Verification</h2>");
        sb.Append("<p class='note'>").Append(Esc(d.Profile.VerificationNote)).Append("</p>");
        sb.Append("<p class='note'>This is a geometric shading / envelope calculation. Confirm all regulatory " +
                  "coefficients and thresholds against the edition of the standard in force before submission.</p>");

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Row(string k, string v) => $"<tr><th>{k}</th><td>{v}</td></tr>";
    private static string Td(string v) => $"<td>{v}</td>";
    private static string F0(double v) => v.ToString("0", CultureInfo.InvariantCulture);
    private static string F1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    private static string F2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Esc(string s) => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private const string Css = @"
@page { size: A4; margin: 16mm; }
body { font-family: 'Segoe UI', Arial, sans-serif; font-size: 11pt; color: #1a1a1a; }
h1 { font-size: 19pt; color: #0b5394; border-bottom: 3px solid #0b5394; padding-bottom: 5px; }
h2 { font-size: 13pt; color: #0b5394; margin-top: 20px; }
table { border-collapse: collapse; width: 100%; margin: 8px 0; font-size: 10pt; }
th, td { border: 1px solid #bbb; padding: 5px 8px; text-align: left; }
th { background: #eaf1f8; }
table.meta { width: auto; } table.meta th { width: 120px; }
tr.total td { font-weight: bold; background: #f4f8fc; }
.banner { margin: 12px 0; padding: 10px 14px; border-radius: 5px; color: #fff; font-weight: bold; font-size: 13pt; }
.banner.pass { background: #2e7d32; } .banner.fail { background: #c62828; }
.note { color: #555; font-size: 9.5pt; }
";
}
