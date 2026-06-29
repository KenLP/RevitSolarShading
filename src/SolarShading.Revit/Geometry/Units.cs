using Autodesk.Revit.DB;
using SolarShading.Core.Geometry;

namespace SolarShading.Revit.Geometry;

/// <summary>Conversions between Revit internal units (feet) and the metric core (metres).</summary>
public static class Units
{
    public const double FeetToMeters = 0.3048;
    public const double MetersToFeet = 1.0 / 0.3048;
    public const double SqFeetToSqMeters = FeetToMeters * FeetToMeters;

    public static Vec3 ToVec3(XYZ p) => new(p.X * FeetToMeters, p.Y * FeetToMeters, p.Z * FeetToMeters);

    public static XYZ ToXyz(Vec3 v) => new(v.X * MetersToFeet, v.Y * MetersToFeet, v.Z * MetersToFeet);

    /// <summary>Direction vector (unitless) — no length scaling, just axis mapping.</summary>
    public static Vec3 DirToVec3(XYZ d) => new(d.X, d.Y, d.Z);
}
