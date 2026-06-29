namespace SolarShading.Revit.Parameters;

/// <summary>
/// Fixed GUIDs for the add-in's shared parameters. Stable GUIDs mean the parameters are the
/// SAME definition in every project and on every machine — so schedules, exports and
/// round-trips line up. Never change these once published.
/// </summary>
internal static class ParameterGuids
{
    public static readonly Guid ShadingDevice = new("a7c1e3d2-0b4f-4e6a-9c11-5e2d8f3a6b01");
    public static readonly Guid Sc2 = new("a7c1e3d2-0b4f-4e6a-9c11-5e2d8f3a6b02");
    public static readonly Guid ShadedMarch = new("a7c1e3d2-0b4f-4e6a-9c11-5e2d8f3a6b03");
    public static readonly Guid ShadedJune = new("a7c1e3d2-0b4f-4e6a-9c11-5e2d8f3a6b04");
    public static readonly Guid ShadedDec = new("a7c1e3d2-0b4f-4e6a-9c11-5e2d8f3a6b05");
}
