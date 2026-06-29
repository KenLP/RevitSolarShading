namespace SolarShading.Core.Ettv;

/// <summary>
/// Built-in envelope-compliance profiles for the SE-Asia OTTV/ETTV family.
///
/// ⚠ VERIFY BEFORE SUBMISSION. Only the THRESHOLDS are confirmed from public sources
/// (BCA ETTV ≤ 50; MS1525 OTTV ≤ 50, RTTV ≤ 25; QCVN 09:2017 wall ≤ 60, roof ≤ 25). The
/// formula COEFFICIENTS and correction-factor tables for codes other than Singapore BCA are
/// INDICATIVE placeholders following the common OTTV structure — they MUST be replaced with
/// the official values from the standard document before any compliance use.
/// </summary>
public static class ComplianceProfiles
{
    private const int Day = 21;

    /// <summary>Singapore BCA Green Mark — ETTV. Coefficients per the BCA ETTV guidelines.</summary>
    public static ComplianceProfile SingaporeBca { get; } = new()
    {
        Code = "BCA Green Mark (ETTV)",
        Jurisdiction = "Singapore",
        WallCoefficients = BcaEttv.Coefficients,              // 12 / 3.4 / 211 (confirmed)
        WallThresholdWm2 = 50.0,                              // confirmed
        CorrectionFactors = BcaCorrectionFactors.IndicativeDefault,
        Roof = new RoofProfile
        {
            // Roof RTTV coefficients indicative; threshold confirmed.
            Coefficients = new EnvelopeCoefficients { WallConduction = 12.0, GlassConduction = 3.4, SolarRadiation = 211.0 },
            RttvThresholdWm2 = 25.0,
        },
        ReferenceMonths = new[] { 3, 6, 12 },
        StartHour = 7,
        EndHour = 18,
        VerificationNote = "BCA ETTV coefficients (12/3.4/211) and the 50 W/m² threshold per the " +
                           "BCA 'Guidelines on ETTV'. Correction factors are indicative — verify the edition.",
    };

    /// <summary>Malaysia MS1525 — OTTV + RTTV. Coefficients INDICATIVE (verify against MS1525).</summary>
    public static ComplianceProfile MalaysiaMs1525 { get; } = new()
    {
        Code = "MS1525 (OTTV)",
        Jurisdiction = "Malaysia",
        // INDICATIVE — common MS1525 OTTV form uses an absorptance-weighted wall term; replace with official values.
        WallCoefficients = new EnvelopeCoefficients { WallConduction = 15.0, GlassConduction = 6.0, SolarRadiation = 194.0 },
        WallThresholdWm2 = 50.0,                              // confirmed
        CorrectionFactors = BcaCorrectionFactors.IndicativeDefault,
        Roof = new RoofProfile
        {
            Coefficients = new EnvelopeCoefficients { WallConduction = 15.0, GlassConduction = 6.0, SolarRadiation = 194.0 },
            RttvThresholdWm2 = 25.0,                          // confirmed
        },
        ReferenceMonths = new[] { 3, 6, 12 },
        StartHour = 7,
        EndHour = 18,
        VerificationNote = "OTTV ≤ 50 / RTTV ≤ 25 confirmed; COEFFICIENTS (15/6/194) are INDICATIVE — " +
                           "replace with MS1525 official values (the wall term is absorptance-weighted).",
    };

    /// <summary>Vietnam QCVN 09:2017/BXD — OTTV. Coefficients INDICATIVE (verify against QCVN 09).</summary>
    public static ComplianceProfile VietnamQcvn09 { get; } = new()
    {
        Code = "QCVN 09:2017 (OTTV)",
        Jurisdiction = "Vietnam",
        // INDICATIVE placeholder following the OTTV structure — set from QCVN 09:2017/BXD Annexes.
        WallCoefficients = new EnvelopeCoefficients { WallConduction = 12.0, GlassConduction = 3.4, SolarRadiation = 211.0 },
        WallThresholdWm2 = 60.0,                              // confirmed (wall)
        CorrectionFactors = BcaCorrectionFactors.IndicativeDefault,
        Roof = new RoofProfile
        {
            Coefficients = new EnvelopeCoefficients { WallConduction = 12.0, GlassConduction = 3.4, SolarRadiation = 211.0 },
            RttvThresholdWm2 = 25.0,                          // confirmed (roof)
        },
        ReferenceMonths = new[] { 3, 6, 12 },
        StartHour = 7,
        EndHour = 18,
        VerificationNote = "Wall ≤ 60 / roof ≤ 25 confirmed; COEFFICIENTS are PLACEHOLDERS — QCVN 09:2017/BXD " +
                           "uses its own envelope thermal-resistance method; encode the official coefficients.",
    };

    public static IReadOnlyList<ComplianceProfile> All { get; } =
        new[] { SingaporeBca, MalaysiaMs1525, VietnamQcvn09 };

    public static ComplianceProfile ByCode(string code)
        => All.FirstOrDefault(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase))
           ?? SingaporeBca;
}
