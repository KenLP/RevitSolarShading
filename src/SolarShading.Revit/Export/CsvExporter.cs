using System.Globalization;
using System.IO;
using System.Text;
using SolarShading.Revit.Engine;

namespace SolarShading.Revit.Export;

/// <summary>Writes window shading analyses to a CSV report.</summary>
public static class CsvExporter
{
    public static string Write(string path, IEnumerable<WindowShadeAnalysis> analyses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WindowId,WindowArea_m2,EffectiveSC2,Instant,AltitudeDeg,Lit,ShadedFraction,ShadedArea_m2");
        foreach (WindowShadeAnalysis a in analyses)
        {
            foreach (InstantShade s in a.Instants)
            {
                sb.Append(a.WindowId.Value).Append(',')
                  .Append(F(a.WindowAreaM2)).Append(',')
                  .Append(F(a.EffectiveSc2)).Append(',')
                  .Append(s.Instant.ToString("yyyy-MM-dd HH:mm")).Append(',')
                  .Append(F(s.AltitudeDeg)).Append(',')
                  .Append(s.Lit ? "1" : "0").Append(',')
                  .Append(F(s.ShadedFraction)).Append(',')
                  .Append(F(s.ShadedAreaM2)).Append('\n');
            }
        }
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    private static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
}
