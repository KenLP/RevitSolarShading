namespace SolarShading.Core.Ettv;

/// <summary>Raw roof quantities for an RTTV evaluation.</summary>
public sealed class RoofData
{
    /// <summary>Gross roof area (opaque + skylight), m².</summary>
    public required double GrossRoofAreaM2 { get; init; }
    /// <summary>Total skylight area, m².</summary>
    public required double SkylightAreaM2 { get; init; }
    /// <summary>Thermal transmittance of the opaque roof, W/m²·K.</summary>
    public required double RoofUValue { get; init; }
    /// <summary>Skylight glazing (U-value + SC1). Use a clear default when there are no skylights.</summary>
    public required Glazing SkylightGlazing { get; init; }
    /// <summary>External shading coefficient on the skylights (SC2); 1.0 = unshaded.</summary>
    public double SkylightSc2 { get; init; } = 1.0;
    /// <summary>Roof solar absorptance (0..1); used by OTTV/RTTV codes that weight it.</summary>
    public double RoofAbsorptance { get; init; } = 1.0;
}

/// <summary>Whole-roof RTTV result with pass/fail against the profile's roof threshold.</summary>
public sealed class EnvelopeRttvResult
{
    public required string Code { get; init; }
    public required double Rttv { get; init; }
    public required double Threshold { get; init; }
    public required double SkylightRoofRatio { get; init; }
    public bool Applicable { get; init; }
    public bool Passes => Rttv <= Threshold + 1e-9;
}

public static class RoofAssessment
{
    /// <summary>
    /// RTTV reuses the envelope formula with roof terms mapped onto it: the opaque roof is the
    /// "wall", the skylight is the "fenestration", and SRR (skylight-to-roof ratio) is the WWR.
    /// Area-weighted across roof elements.
    /// </summary>
    public static EnvelopeRttvResult Evaluate(IReadOnlyList<RoofData> roofs, ComplianceProfile profile)
    {
        RoofProfile? roofProfile = profile.Roof;
        if (roofProfile == null || roofs.Count == 0)
            return new EnvelopeRttvResult
            {
                Code = profile.Code, Rttv = 0, Threshold = roofProfile?.RttvThresholdWm2 ?? 0,
                SkylightRoofRatio = 0, Applicable = false,
            };

        double weighted = 0, totalArea = 0, totalSkylight = 0;
        foreach (RoofData r in roofs)
        {
            double srr = r.GrossRoofAreaM2 > 1e-9 ? Math.Clamp(r.SkylightAreaM2 / r.GrossRoofAreaM2, 0.0, 1.0) : 0.0;
            var inputs = new EttvInputs
            {
                WindowToWallRatio = srr,
                WallUValue = r.RoofUValue,
                FenestrationUValue = r.SkylightGlazing.UValue,
                CorrectionFactor = roofProfile.CorrectionFactor,
                GlassShadingCoefficient = r.SkylightGlazing.ShadingCoefficient,
                ExternalShadingCoefficient = r.SkylightSc2,
                WallAbsorptance = r.RoofAbsorptance,
            };
            double rttv = EnvelopeThermal.Compute(roofProfile.Coefficients, inputs);
            weighted += rttv * r.GrossRoofAreaM2;
            totalArea += r.GrossRoofAreaM2;
            totalSkylight += r.SkylightAreaM2;
        }

        return new EnvelopeRttvResult
        {
            Code = profile.Code,
            Rttv = totalArea > 1e-9 ? weighted / totalArea : 0.0,
            Threshold = roofProfile.RttvThresholdWm2,
            SkylightRoofRatio = totalArea > 1e-9 ? totalSkylight / totalArea : 0.0,
            Applicable = true,
        };
    }
}
