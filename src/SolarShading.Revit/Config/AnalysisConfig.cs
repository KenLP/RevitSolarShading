using SolarShading.Core.Ettv;

namespace SolarShading.Revit.Config;

/// <summary>User-configurable options for a shading / ETTV run.</summary>
public sealed class AnalysisConfig
{
    public bool IncludeMarch { get; set; } = true;
    public bool IncludeJune { get; set; } = true;
    public bool IncludeDecember { get; set; } = true;

    public int StartHour { get; set; } = 7;
    public int EndHour { get; set; } = 18;
    public int Year { get; set; } = DateTime.Today.Year;

    /// <summary>Selected compliance code (BCA / MS1525 / QCVN 09 …) — drives coefficients + threshold.</summary>
    public ComplianceProfile Profile { get; set; } = ComplianceProfiles.SingaporeBca;
    public Glazing Glazing { get; set; } = GlazingLibrary.DoubleLowE;
    public double WallUValue { get; set; } = 2.0;
    public double RoofUValue { get; set; } = 0.5;
    public bool GenerateReport { get; set; } = true;

    public bool ShowShadowOverlay { get; set; } = true;
    public bool WriteParameters { get; set; } = true;
    public bool ExportCsv { get; set; } = true;

    /// <summary>Which moment the shadow overlay is drawn for (month 3/6/12 and hour).</summary>
    public int OverlayMonth { get; set; } = 6;
    public int OverlayHour { get; set; } = 12;

    /// <summary>
    /// Draw the shadow at every hour of the overlay date (a layered, hour-coloured fan) for a
    /// few windows instead of a single moment. Limited to selected windows to avoid clutter.
    /// </summary>
    public bool WholeDaySweep { get; set; } = false;
    public int MaxSweepWindows { get; set; } = 5;

    public IReadOnlyList<int> Months()
    {
        var m = new List<int>();
        if (IncludeMarch) m.Add(3);
        if (IncludeJune) m.Add(6);
        if (IncludeDecember) m.Add(12);
        return m;
    }
}
