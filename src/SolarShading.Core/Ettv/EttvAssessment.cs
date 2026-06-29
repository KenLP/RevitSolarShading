namespace SolarShading.Core.Ettv;

/// <summary>Raw façade quantities, before the ETTV inputs are assembled.</summary>
public sealed class FacadeData
{
    public required Orientation Orientation { get; init; }
    /// <summary>Gross façade area (opaque wall + fenestration), m².</summary>
    public required double GrossWallAreaM2 { get; init; }
    /// <summary>Total fenestration (window) area, m².</summary>
    public required double WindowAreaM2 { get; init; }
    public required double WallUValue { get; init; }
    public required Glazing Glazing { get; init; }
    /// <summary>Effective external shading coefficient (SC2) from the shadow engine, 0..1.</summary>
    public required double ExternalShadingCoefficient { get; init; }
    /// <summary>Opaque-wall solar absorptance (0..1); used by OTTV codes, ignored by BCA (1.0).</summary>
    public double WallAbsorptance { get; init; } = 1.0;
}

/// <summary>ETTV result for one façade.</summary>
public sealed class FacadeEttvResult
{
    public required Orientation Orientation { get; init; }
    public required double WindowToWallRatio { get; init; }
    public required double Ettv { get; init; }
}

/// <summary>Whole-envelope ETTV/OTTV result (area-weighted across façades) with pass/fail.</summary>
public sealed class EnvelopeEttvResult
{
    public required string Code { get; init; }
    public required IReadOnlyList<FacadeEttvResult> Facades { get; init; }
    public required double Ettv { get; init; }
    public required double Threshold { get; init; }
    public bool Passes => Ettv <= Threshold + 1e-9;
}

/// <summary>
/// Assembles ETTV/OTTV inputs from façade quantities and aggregates façades into a
/// building-envelope value with pass/fail, driven by a <see cref="ComplianceProfile"/> so the
/// formula coefficients, threshold and correction factors come from the selected jurisdiction.
/// </summary>
public sealed class EttvAssessment
{
    public const double DefaultThresholdWm2 = 50.0;

    private readonly ComplianceProfile _profile;

    public EttvAssessment(ComplianceProfile? profile = null)
        => _profile = profile ?? ComplianceProfiles.SingaporeBca;

    public ComplianceProfile Profile => _profile;

    public FacadeEttvResult EvaluateFacade(FacadeData f)
    {
        double wwr = f.GrossWallAreaM2 > 1e-9 ? Math.Clamp(f.WindowAreaM2 / f.GrossWallAreaM2, 0.0, 1.0) : 0.0;
        var inputs = new EttvInputs
        {
            WindowToWallRatio = wwr,
            WallUValue = f.WallUValue,
            FenestrationUValue = f.Glazing.UValue,
            CorrectionFactor = _profile.CorrectionFactors.For(f.Orientation),
            GlassShadingCoefficient = f.Glazing.ShadingCoefficient,
            ExternalShadingCoefficient = f.ExternalShadingCoefficient,
            WallAbsorptance = f.WallAbsorptance,
        };
        return new FacadeEttvResult
        {
            Orientation = f.Orientation,
            WindowToWallRatio = wwr,
            Ettv = EnvelopeThermal.Compute(_profile.WallCoefficients, inputs),
        };
    }

    /// <summary>
    /// Envelope ETTV = Σ(ETTVᵢ · Awallᵢ) / Σ(Awallᵢ), the area-weighted average of the façade
    /// values, per the OTTV/ETTV definition.
    /// </summary>
    public EnvelopeEttvResult EvaluateEnvelope(IReadOnlyList<FacadeData> facades)
    {
        var results = new List<FacadeEttvResult>(facades.Count);
        double weighted = 0.0, totalArea = 0.0;
        foreach (FacadeData f in facades)
        {
            FacadeEttvResult r = EvaluateFacade(f);
            results.Add(r);
            weighted += r.Ettv * f.GrossWallAreaM2;
            totalArea += f.GrossWallAreaM2;
        }
        return new EnvelopeEttvResult
        {
            Code = _profile.Code,
            Facades = results,
            Ettv = totalArea > 1e-9 ? weighted / totalArea : 0.0,
            Threshold = _profile.WallThresholdWm2,
        };
    }
}
