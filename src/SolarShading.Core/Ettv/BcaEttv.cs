namespace SolarShading.Core.Ettv;

/// <summary>
/// Per-façade inputs for the Singapore BCA Envelope Thermal Transfer Value (ETTV).
/// All areas in m², U-values in W/m²·K.
/// </summary>
public sealed class EttvInputs
{
    /// <summary>Window-to-wall ratio of the façade (0..1).</summary>
    public required double WindowToWallRatio { get; init; }
    /// <summary>Thermal transmittance of the opaque wall, W/m²·K.</summary>
    public required double WallUValue { get; init; }
    /// <summary>Thermal transmittance of the fenestration (glass), W/m²·K.</summary>
    public required double FenestrationUValue { get; init; }
    /// <summary>Solar correction factor for the façade orientation (BCA tables).</summary>
    public required double CorrectionFactor { get; init; }
    /// <summary>Shading coefficient of the glass alone (SC1).</summary>
    public required double GlassShadingCoefficient { get; init; }
    /// <summary>Effective shading coefficient from external shading devices (SC2), 0..1.</summary>
    public required double ExternalShadingCoefficient { get; init; }
    /// <summary>
    /// Solar absorptance of the opaque wall (0..1). Used by OTTV codes (e.g. MS1525) that
    /// weight the wall-conduction term by absorptance. BCA ETTV does not use it, so it
    /// defaults to 1.0 (no effect).
    /// </summary>
    public double WallAbsorptance { get; init; } = 1.0;
}

/// <summary>
/// Singapore BCA ETTV formula (W/m²):
///   ETTV = 12·(1−WWR)·Uw  +  3.4·WWR·Uf  +  211·WWR·CF·SC
/// where SC = SC1 (glass) × SC2 (external shading). The shadow engine supplies SC2.
///
/// Coefficients follow the BCA "Guidelines on Envelope Thermal Transfer Value"
/// and must be confirmed against the edition in force before submission — they are
/// centralised here so a code edition / jurisdiction can override them.
/// </summary>
public static class BcaEttv
{
    public const double ConductionThroughWall = 12.0;   // W/m²·K coefficient
    public const double ConductionThroughGlass = 3.4;    // W/m²·K coefficient
    public const double SolarRadiation = 211.0;          // W/m² coefficient

    /// <summary>The BCA ETTV coefficients as a reusable set.</summary>
    public static readonly EnvelopeCoefficients Coefficients = new()
    {
        WallConduction = ConductionThroughWall,
        GlassConduction = ConductionThroughGlass,
        SolarRadiation = SolarRadiation,
    };

    public static double Compute(EttvInputs i) => EnvelopeThermal.Compute(Coefficients, i);
}
