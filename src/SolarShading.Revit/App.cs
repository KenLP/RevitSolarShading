using System.Reflection;
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
            "SS_GetShadingDevices", "Get Shading\nDevices", asm,
            "SolarShading.Revit.Commands.GetShadingDevicesCommand")
        {
            ToolTip = "Tag the selected elements as shading devices.",
        });

        panel.AddItem(new PushButtonData(
            "SS_ShadingOnWindows", "Shading on\nWindows", asm,
            "SolarShading.Revit.Commands.ShadingOnWindowsCommand")
        {
            ToolTip = "Compute shaded area and external shading coefficient (SC2) for every window.",
        });

        panel.AddItem(new PushButtonData(
            "SS_BuildingShadow", "Building Shadow\non Ground", asm,
            "SolarShading.Revit.Commands.BuildingShadowOnGroundCommand")
        {
            ToolTip = "Cast the selected building's shadow onto the ground plane.",
        });

        // Headless trigger watcher (no-op unless a trigger file is queued) — enables
        // automated runs without a ribbon click.
        application.Idling += HeadlessRunner.OnIdling;
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        application.Idling -= HeadlessRunner.OnIdling;
        return Result.Succeeded;
    }
}
