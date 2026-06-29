using System.Globalization;
using System.Windows;
using SolarShading.Core.Ettv;
using SolarShading.Revit.Config;

namespace SolarShading.Revit.UI;

public partial class ShadingConfigWindow : Window
{
    public AnalysisConfig Config { get; }

    public ShadingConfigWindow(AnalysisConfig config)
    {
        InitializeComponent();
        Config = config;

        CmbCode.ItemsSource = ComplianceProfiles.All;
        CmbCode.SelectedItem = ComplianceProfiles.All.FirstOrDefault(p => p.Code == config.Profile.Code)
                               ?? ComplianceProfiles.SingaporeBca;

        CmbGlazing.ItemsSource = GlazingLibrary.All;
        CmbGlazing.SelectedItem = GlazingLibrary.All.FirstOrDefault(g => g.Name == config.Glazing.Name)
                                  ?? GlazingLibrary.DoubleLowE;

        ChkMarch.IsChecked = config.IncludeMarch;
        ChkJune.IsChecked = config.IncludeJune;
        ChkDecember.IsChecked = config.IncludeDecember;
        TxtStartHour.Text = config.StartHour.ToString();
        TxtEndHour.Text = config.EndHour.ToString();
        TxtYear.Text = config.Year.ToString();
        TxtWallU.Text = config.WallUValue.ToString(CultureInfo.InvariantCulture);
        TxtRoofU.Text = config.RoofUValue.ToString(CultureInfo.InvariantCulture);
        ChkOverlay.IsChecked = config.ShowShadowOverlay;
        ChkWriteParams.IsChecked = config.WriteParameters;
        ChkCsv.IsChecked = config.ExportCsv;
        ChkReport.IsChecked = config.GenerateReport;

        SelectOverlayMonth(config.OverlayMonth);
        TxtOverlayHour.Text = config.OverlayHour.ToString();
        ChkSweep.IsChecked = config.WholeDaySweep;
    }

    private void OnCodeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (CmbCode.SelectedItem is ComplianceProfile p)
            LblThreshold.Text = $"≤ {p.WallThresholdWm2:0} W/m²";
    }

    private void SelectOverlayMonth(int month)
    {
        foreach (object item in CmbOverlayDate.Items)
            if (item is System.Windows.Controls.ComboBoxItem ci && ci.Tag is string tag
                && int.TryParse(tag, out int m) && m == month)
            {
                CmbOverlayDate.SelectedItem = ci;
                return;
            }
    }

    private void OnRun(object sender, RoutedEventArgs e)
    {
        if (!TryReadInt(TxtStartHour.Text, 0, 23, out int startHour) ||
            !TryReadInt(TxtEndHour.Text, 0, 23, out int endHour) ||
            !TryReadInt(TxtYear.Text, 1900, 2200, out int year) ||
            !TryReadDouble(TxtWallU.Text, out double wallU) ||
            !TryReadDouble(TxtRoofU.Text, out double roofU))
        {
            MessageBox.Show("Please check the numeric fields (hours 0–23, valid year, numbers).",
                "Solar Shading", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (endHour < startHour)
        {
            MessageBox.Show("End hour must be on or after start hour.", "Solar Shading",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Config.IncludeMarch = ChkMarch.IsChecked == true;
        Config.IncludeJune = ChkJune.IsChecked == true;
        Config.IncludeDecember = ChkDecember.IsChecked == true;
        if (Config.Months().Count == 0)
        {
            MessageBox.Show("Select at least one reference date.", "Solar Shading",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryReadInt(TxtOverlayHour.Text, 0, 23, out int overlayHour))
        {
            MessageBox.Show("Overlay hour must be between 0 and 23.", "Solar Shading",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Config.OverlayHour = overlayHour;
        if (CmbOverlayDate.SelectedItem is System.Windows.Controls.ComboBoxItem oci
            && oci.Tag is string otag && int.TryParse(otag, out int om))
            Config.OverlayMonth = om;

        Config.StartHour = startHour;
        Config.EndHour = endHour;
        Config.Year = year;
        Config.WallUValue = wallU;
        Config.RoofUValue = roofU;
        if (CmbCode.SelectedItem is ComplianceProfile profile)
            Config.Profile = profile;
        Config.Glazing = (Glazing)CmbGlazing.SelectedItem;
        Config.ShowShadowOverlay = ChkOverlay.IsChecked == true;
        Config.WholeDaySweep = ChkSweep.IsChecked == true;
        Config.WriteParameters = ChkWriteParams.IsChecked == true;
        Config.ExportCsv = ChkCsv.IsChecked == true;
        Config.GenerateReport = ChkReport.IsChecked == true;

        DialogResult = true;
    }

    private static bool TryReadInt(string s, int min, int max, out int value)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
           && value >= min && value <= max;

    private static bool TryReadDouble(string s, out double value)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0;
}
