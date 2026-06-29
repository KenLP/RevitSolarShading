namespace SolarShading.Core.Ettv;

/// <summary>Roof thermal-transfer (RTTV) part of a compliance code, when applicable.</summary>
public sealed class RoofProfile
{
    public required EnvelopeCoefficients Coefficients { get; init; }
    /// <summary>Maximum allowed RTTV, W/m².</summary>
    public required double RttvThresholdWm2 { get; init; }
    /// <summary>Solar correction factor for the (horizontal) roof skylight term. Indicative.</summary>
    public double CorrectionFactor { get; init; } = 1.0;
}

/// <summary>
/// A declarative envelope-compliance code: the formula coefficients, threshold, orientation
/// correction factors and the reference dates/hours that drive the shading computation. Adding
/// a jurisdiction is data, not code — this is what turns the engine from a single-code ETTV
/// calculator into a multi-jurisdiction platform.
///
/// ⚠ All regulatory constants are INDICATIVE and must be confirmed against the edition of the
/// standard in force before any submission (see <see cref="VerificationNote"/>).
/// </summary>
public sealed class ComplianceProfile
{
    public required string Code { get; init; }
    public required string Jurisdiction { get; init; }

    /// <summary>Wall/façade thermal-transfer coefficients (ETTV/OTTV).</summary>
    public required EnvelopeCoefficients WallCoefficients { get; init; }
    /// <summary>Maximum allowed ETTV/OTTV for the façade, W/m².</summary>
    public required double WallThresholdWm2 { get; init; }
    /// <summary>Per-orientation solar correction factors.</summary>
    public required BcaCorrectionFactors CorrectionFactors { get; init; }

    /// <summary>Optional roof (RTTV) part of the code.</summary>
    public RoofProfile? Roof { get; init; }

    /// <summary>Reference months the code assesses shading on (e.g. solstice/equinox).</summary>
    public required IReadOnlyList<int> ReferenceMonths { get; init; }
    public required int StartHour { get; init; }
    public required int EndHour { get; init; }

    public required string VerificationNote { get; init; }

    public bool FacadePasses(double ettv) => ettv <= WallThresholdWm2 + 1e-9;
    public bool RoofPasses(double rttv) => Roof != null && rttv <= Roof.RttvThresholdWm2 + 1e-9;
}
