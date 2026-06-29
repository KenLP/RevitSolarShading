using System.Text.Json;
using System.Text.Json.Serialization;
using SolarShading.Core.Ettv;
using SolarShading.Revit.Config;

namespace SolarShading.Revit.Headless;

/// <summary>
/// JSON shape of a headless trigger file. All fields optional; sensible defaults apply.
/// Lets an external driver (e.g. an automation/test loop) request a shading run without
/// the ribbon UI.
/// </summary>
public sealed class HeadlessConfig
{
    [JsonPropertyName("months")] public int[]? Months { get; set; }
    [JsonPropertyName("startHour")] public int? StartHour { get; set; }
    [JsonPropertyName("endHour")] public int? EndHour { get; set; }
    [JsonPropertyName("year")] public int? Year { get; set; }
    [JsonPropertyName("overlayMonth")] public int? OverlayMonth { get; set; }
    [JsonPropertyName("overlayHour")] public int? OverlayHour { get; set; }
    [JsonPropertyName("wholeDaySweep")] public bool? WholeDaySweep { get; set; }
    [JsonPropertyName("showOverlay")] public bool? ShowOverlay { get; set; }
    [JsonPropertyName("writeParameters")] public bool? WriteParameters { get; set; }
    [JsonPropertyName("exportCsv")] public bool? ExportCsv { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("glazingName")] public string? GlazingName { get; set; }
    [JsonPropertyName("wallU")] public double? WallU { get; set; }
    [JsonPropertyName("roofU")] public double? RoofU { get; set; }
    [JsonPropertyName("generateReport")] public bool? GenerateReport { get; set; }
    [JsonPropertyName("selectedIds")] public long[]? SelectedIds { get; set; }

    public static HeadlessConfig Parse(string json)
        => JsonSerializer.Deserialize<HeadlessConfig>(json) ?? new HeadlessConfig();

    public AnalysisConfig ToAnalysisConfig()
    {
        var c = new AnalysisConfig();
        if (Months is { Length: > 0 })
        {
            c.IncludeMarch = Months.Contains(3);
            c.IncludeJune = Months.Contains(6);
            c.IncludeDecember = Months.Contains(12);
        }
        if (StartHour is { } sh) c.StartHour = sh;
        if (EndHour is { } eh) c.EndHour = eh;
        if (Year is { } y) c.Year = y;
        if (OverlayMonth is { } om) c.OverlayMonth = om;
        if (OverlayHour is { } oh) c.OverlayHour = oh;
        if (WholeDaySweep is { } ws) c.WholeDaySweep = ws;
        if (ShowOverlay is { } so) c.ShowShadowOverlay = so;
        if (WriteParameters is { } wp) c.WriteParameters = wp;
        if (ExportCsv is { } ec) c.ExportCsv = ec;
        if (Code != null) c.Profile = ComplianceProfiles.ByCode(Code);
        if (WallU is { } wu) c.WallUValue = wu;
        if (RoofU is { } ru) c.RoofUValue = ru;
        if (GenerateReport is { } gr) c.GenerateReport = gr;
        if (GlazingName != null)
            c.Glazing = GlazingLibrary.All.FirstOrDefault(
                g => string.Equals(g.Name, GlazingName, StringComparison.OrdinalIgnoreCase)) ?? c.Glazing;
        return c;
    }
}
