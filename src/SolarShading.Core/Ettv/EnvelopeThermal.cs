namespace SolarShading.Core.Ettv;

/// <summary>
/// The three coefficients of the SE-Asia envelope thermal-transfer formula. The OTTV/ETTV
/// family (Singapore BCA, Malaysia MS1525, Vietnam QCVN 09, Thailand BEC, …) all share the
/// same three-term structure and differ only in these constants and the threshold:
///
///   V = Cwall · α · (1−WWR) · Uw   +   Cglass · WWR · Uf   +   Csolar · WWR · CF · SC
///
/// where SC = SC1 (glass) × SC2 (external shading). α is wall solar absorptance (1.0 when a
/// code doesn't use it, e.g. BCA).
/// </summary>
public sealed class EnvelopeCoefficients
{
    /// <summary>Wall-conduction coefficient (BCA ETTV: 12).</summary>
    public required double WallConduction { get; init; }
    /// <summary>Glass-conduction coefficient (BCA ETTV: 3.4).</summary>
    public required double GlassConduction { get; init; }
    /// <summary>Solar-radiation coefficient (BCA ETTV: 211).</summary>
    public required double SolarRadiation { get; init; }
}

/// <summary>Evaluates the generalized envelope thermal-transfer value for any SE-Asia code.</summary>
public static class EnvelopeThermal
{
    public static double Compute(EnvelopeCoefficients c, EttvInputs i)
    {
        double wwr = i.WindowToWallRatio;
        double sc = i.GlassShadingCoefficient * i.ExternalShadingCoefficient;

        double wallConduction = c.WallConduction * i.WallAbsorptance * (1.0 - wwr) * i.WallUValue;
        double glassConduction = c.GlassConduction * wwr * i.FenestrationUValue;
        double solar = c.SolarRadiation * wwr * i.CorrectionFactor * sc;

        return wallConduction + glassConduction + solar;
    }
}
