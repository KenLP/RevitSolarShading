using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using SolarShading.Revit.Headless;

namespace SolarShading.Revit;

/// <summary>Registers the Solar Shading ribbon tab and its commands.</summary>
public sealed class App : IExternalApplication
{
    private const string TabName = "Solar Shading";

    public Result OnStartup(UIControlledApplication application)
    {
        string asm = Assembly.GetExecutingAssembly().Location;

        application.CreateRibbonTab(TabName);
        RibbonPanel panel = application.CreateRibbonPanel(TabName, "Shading & ETTV");

        panel.AddItem(new PushButtonData(
            "SS_SetupParameters", "Setup\nParameters", asm,
            "SolarShading.Revit.Commands.SetupParametersCommand")
        {
            ToolTip = "Create and bind the add-in's shared parameters (run once per project).",
            LargeImage = Icon("setup32.png"),
            Image = Icon("setup16.png"),
        });

        panel.AddItem(new PushButtonData(
            "SS_GetShadingDevices", "Shading\nDevices", asm,
            "SolarShading.Revit.Commands.GetShadingDevicesCommand")
        {
            ToolTip = "Tag / untag / review the shading devices (saved in the model).",
            LargeImage = Icon("devices32.png"),
            Image = Icon("devices16.png"),
        });

        panel.AddItem(new PushButtonData(
            "SS_ShadingOnWindows", "Shading on\nWindows", asm,
            "SolarShading.Revit.Commands.ShadingOnWindowsCommand")
        {
            ToolTip = "Compute shaded area and external shading coefficient (SC2) for every window.",
            LargeImage = Icon("windows32.png"),
            Image = Icon("windows16.png"),
        });

        panel.AddItem(new PushButtonData(
            "SS_BuildingShadow", "Building Shadow\non Ground", asm,
            "SolarShading.Revit.Commands.BuildingShadowOnGroundCommand")
        {
            ToolTip = "Cast the selected Mass element(s)' shadow onto the ground at a chosen date/time.",
            LargeImage = Icon("building32.png"),
            Image = Icon("building16.png"),
        });

        // Headless trigger watcher (no-op unless a trigger file is queued) — enables
        // automated runs without a ribbon click.
        application.Idling += HeadlessRunner.OnIdling;
        return Result.Succeeded;
    }

    /// <summary>Load a ribbon icon embedded under <c>Resources/</c> as a frozen bitmap.</summary>
    private static BitmapImage? Icon(string file)
    {
        try
        {
            using Stream? s = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream($"SolarShading.Revit.Resources.{file}");
            if (s == null)
                return null;
            var img = new BitmapImage();
            img.BeginInit();
            img.StreamSource = s;
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null; // missing icon must never block ribbon creation
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        application.Idling -= HeadlessRunner.OnIdling;
        return Result.Succeeded;
    }
}
