using System.Windows;
using System.Windows.Media;
using SolarShading.Core.Ettv;

namespace SolarShading.Revit.UI;

public partial class EttvResultWindow : Window
{
    public sealed class Row
    {
        public required string Orientation { get; init; }
        public required double WindowAreaM2 { get; init; }
        public required double WwrPercent { get; init; }
        public required double Sc2 { get; init; }
        public required double Ettv { get; init; }
    }

    public EttvResultWindow(EnvelopeEttvResult envelope, IReadOnlyList<FacadeData> facades, int windowsAnalyzed = 0)
    {
        InitializeComponent();
        if (windowsAnalyzed > 0)
            Title = $"ETTV Results — {windowsAnalyzed} windows analyzed";

        var facadeByOrientation = facades.ToDictionary(f => f.Orientation);
        var rows = envelope.Facades.Select(f =>
        {
            FacadeData data = facadeByOrientation[f.Orientation];
            return new Row
            {
                Orientation = f.Orientation.ToString(),
                WindowAreaM2 = data.WindowAreaM2,
                WwrPercent = f.WindowToWallRatio * 100.0,
                Sc2 = data.ExternalShadingCoefficient,
                Ettv = f.Ettv,
            };
        }).ToList();
        Grid.ItemsSource = rows;

        bool pass = envelope.Passes;
        SummaryText.Text =
            $"{envelope.Code}:  {envelope.Ettv:N1} W/m²   ·   limit {envelope.Threshold:N0} W/m²   ·   " +
            (pass ? "PASS" : "FAIL");
        SummaryBorder.Background = new SolidColorBrush(
            pass ? Color.FromRgb(0x2E, 0x7D, 0x32) : Color.FromRgb(0xC6, 0x28, 0x28));
    }
}
