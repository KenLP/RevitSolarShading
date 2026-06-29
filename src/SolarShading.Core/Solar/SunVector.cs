using SolarShading.Core.Geometry;

namespace SolarShading.Core.Solar;

/// <summary>Result of a solar-position evaluation for a single instant and location.</summary>
public readonly struct SunVector
{
    /// <summary>Solar altitude above the horizon, in degrees (negative = below horizon).</summary>
    public double AltitudeDeg { get; }

    /// <summary>Solar azimuth in degrees, measured clockwise from true North (0 = N, 90 = E, 180 = S).</summary>
    public double AzimuthDeg { get; }

    public SunVector(double altitudeDeg, double azimuthDeg)
    {
        AltitudeDeg = altitudeDeg;
        AzimuthDeg = azimuthDeg;
    }

    public bool IsDaytime => AltitudeDeg > 0.0;

    /// <summary>
    /// Unit vector pointing from the scene toward the sun, in ENU coordinates
    /// (X = East, Y = North, Z = Up).
    /// </summary>
    public Vec3 ToSun()
    {
        double alt = AltitudeDeg * Math.PI / 180.0;
        double az = AzimuthDeg * Math.PI / 180.0;
        double cosAlt = Math.Cos(alt);
        return new Vec3(
            cosAlt * Math.Sin(az),  // East
            cosAlt * Math.Cos(az),  // North
            Math.Sin(alt));         // Up
    }

    /// <summary>
    /// Unit vector in the direction the sunlight travels (scene-ward). This is the
    /// projection direction used to cast shadows onto a receiver plane.
    /// </summary>
    public Vec3 LightDirection() => -ToSun();
}
