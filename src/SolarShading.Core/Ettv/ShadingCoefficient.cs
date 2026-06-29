namespace SolarShading.Core.Ettv;

/// <summary>One time-step of shading data for a window.</summary>
public readonly struct ShadeSample
{
    /// <summary>Fraction of the window NOT in shadow at this instant, 0..1.</summary>
    public double SunlitFraction { get; }
    /// <summary>
    /// Weight for this instant — ideally the incident solar radiation on the window
    /// (W/m²) so the effective SC2 is solar-weighted. Use 1.0 for an unweighted mean.
    /// </summary>
    public double Weight { get; }

    public ShadeSample(double sunlitFraction, double weight)
    {
        SunlitFraction = sunlitFraction;
        Weight = weight;
    }
}

/// <summary>
/// Aggregates per-instant shaded fractions into the effective external shading
/// coefficient SC2 used by ETTV. SC2 is the share of solar gain that still reaches
/// the glass after the external shading device — i.e. the (solar-weighted) average
/// sunlit fraction over the analysis period.
/// </summary>
public static class ShadingCoefficient
{
    /// <summary>
    /// Effective SC2 in [0,1]. 1.0 = no shading benefit (fully sunlit); 0.0 = always
    /// fully shaded. Multiply by the glass SC1 to get the combined SC for ETTV.
    /// </summary>
    public static double EffectiveSc2(IReadOnlyList<ShadeSample> samples)
    {
        double weighted = 0.0;
        double total = 0.0;
        foreach (ShadeSample s in samples)
        {
            double w = Math.Max(0.0, s.Weight);
            weighted += Math.Clamp(s.SunlitFraction, 0.0, 1.0) * w;
            total += w;
        }
        return total > 1e-12 ? weighted / total : 1.0;
    }
}
