using System.Globalization;
using System.Windows;

namespace SolarShading.Revit.UI;

public partial class BuildingShadowWindow : Window
{
    public DateTime Date { get; private set; }
    public int Hour { get; private set; }

    public BuildingShadowWindow()
    {
        InitializeComponent();
        DatePick.SelectedDate = DateTime.Today;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (DatePick.SelectedDate is not { } date)
        {
            MessageBox.Show("Pick a date.", "Building Shadow", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(TxtHour.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hour)
            || hour < 0 || hour > 23)
        {
            MessageBox.Show("Hour must be 0–23.", "Building Shadow", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Date = date;
        Hour = hour;
        DialogResult = true;
    }
}
