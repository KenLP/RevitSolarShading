using SolarShading.Core.Geometry;

namespace SolarShading.Core.Solar;

/// <summary>
/// ASHRAE clear-sky model for direct (beam) normal irradiance. Used to weight the SC2
/// aggregation by how much solar energy actually reaches a surface at each instant, so
/// the effective external shading coefficient reflects energy, not just clock hours.
///
/// Beam normal:  Ibn = A · exp(−B / sin(altitude))
/// where A (apparent extraterrestrial irradiance, W/m²) and B (atmospheric extinction)
/// are the classic ASHRAE monthly coefficients. This is a clear-sky proxy; for
/// compliance-grade SC2 substitute measured/EPW irradiance.
/// </summary>
public static class ClearSkyIrradiance
{
    // ASHRAE clear-sky coefficients by month (1..12). A in W/m², B dimensionless.
    private static readonly double[] A =
        { 1230, 1215, 1186, 1136, 1104, 1088, 1085, 1107, 1151, 1192, 1221, 1233 };
    private static readonly double[] B =
        { 0.142, 0.144, 0.156, 0.180, 0.196, 0.205, 0.207, 0.201, 0.177, 0.160, 0.149, 0.142 };

    /// <summary>Direct normal irradiance (W/m²) for a month (1-12) and solar altitude.</summary>
    public static double BeamNormal(int month, double altitudeDeg)
    {
        if (altitudeDeg <= 0.0)
            return 0.0;
        int i = Math.Clamp(month - 1, 0, 11);
        double sinAlt = Math.Sin(altitudeDeg * Math.PI / 180.0);
        if (sinAlt < 1e-6)
            return 0.0;
        return A[i] * Math.Exp(-B[i] / sinAlt);
    }

    /// <summary>
    /// Beam irradiance incident on a surface (W/m²): the normal beam times the cosine of
    /// the incidence angle between the sun and the surface's outward normal. Zero when the
    /// sun is behind the surface.
    /// </summary>
    public static double OnSurface(int month, double altitudeDeg, Vec3 toSun, Vec3 outwardNormal)
    {
        double cosIncidence = Math.Max(0.0, toSun.Dot(outwardNormal));
        return BeamNormal(month, altitudeDeg) * cosIncidence;
    }
}
