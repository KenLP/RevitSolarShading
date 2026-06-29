using Autodesk.Revit.DB;
using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;

namespace SolarShading.Revit.Solar;

/// <summary>Sun direction in Revit MODEL (project-north) coordinates for a given instant.</summary>
public readonly struct ModelSun
{
    /// <summary>Unit vector toward the sun, in model coordinates (X,Y,Z = project east/north/up).</summary>
    public Vec3 ToSun { get; }
    public double AltitudeDeg { get; }
    public ModelSun(Vec3 toSun, double altitudeDeg)
    {
        ToSun = toSun;
        AltitudeDeg = altitudeDeg;
    }
    public bool IsDaytime => AltitudeDeg > 0.0;
}

/// <summary>
/// Computes the sun direction analytically from the document's site location and
/// project-north angle — NO Revit transaction and NO dependency on SunAndShadowSettings
/// or an active 3D view. Driving the sun through a per-hour transaction would be a major
/// performance bottleneck; the analytic computation avoids it entirely.
/// </summary>
public sealed class SiteSun
{
    private readonly double _latDeg;
    private readonly double _lonDeg;
    private readonly double _timeZoneHours;
    private readonly double _projectNorthAngle; // radians, CCW about Z (true→project)
    private readonly ISolarPositionAlgorithm _algorithm;

    public SiteSun(Document doc, ISolarPositionAlgorithm? algorithm = null)
    {
        SiteLocation site = doc.SiteLocation;
        _latDeg = site.Latitude * 180.0 / Math.PI;   // Revit stores radians
        _lonDeg = site.Longitude * 180.0 / Math.PI;
        _timeZoneHours = site.TimeZone;

        ProjectPosition pos = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero);
        _projectNorthAngle = pos.Angle;
        _algorithm = algorithm ?? SolarPosition.Instance;
    }

    /// <summary>Local civil time at the site (year/month/day/hour…) → model-space sun.</summary>
    public ModelSun ForLocalTime(int year, int month, int day, int hour, int minute = 0)
    {
        var offset = TimeSpan.FromHours(_timeZoneHours);
        var instant = new DateTimeOffset(year, month, day, hour, minute, 0, offset);
        return ForInstant(instant);
    }

    public ModelSun ForInstant(DateTimeOffset instant)
    {
        SunVector sun = _algorithm.Compute(instant, _latDeg, _lonDeg);
        Vec3 toSunTrue = sun.ToSun(); // true-north ENU
        Vec3 toSunModel = RotateZ(toSunTrue, -_projectNorthAngle);
        return new ModelSun(toSunModel, sun.AltitudeDeg);
    }

    /// <summary>Rotate a vector about the Z axis by <paramref name="angle"/> radians (CCW positive).</summary>
    private static Vec3 RotateZ(Vec3 v, double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        return new Vec3(c * v.X - s * v.Y, s * v.X + c * v.Y, v.Z);
    }
}
