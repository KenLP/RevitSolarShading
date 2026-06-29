namespace SolarShading.Core.Ettv;

/// <summary>
/// Solar correction factor (CF) per façade orientation for the Singapore BCA ETTV
/// solar-radiation term.
///
/// ⚠ REGULATORY VALUES — VERIFY BEFORE SUBMISSION. The numbers below are the commonly
/// cited indicative CF values for vertical fenestration in Singapore. They MUST be
/// confirmed against the BCA "Guidelines on Envelope Thermal Transfer Value" edition
/// in force (and re-derived for any other jurisdiction / latitude). They are isolated
/// here so an editor can override them without touching the engine.
/// </summary>
public sealed class BcaCorrectionFactors
{
    private readonly IReadOnlyDictionary<Orientation, double> _cf;

    public BcaCorrectionFactors(IReadOnlyDictionary<Orientation, double> cf) => _cf = cf;

    public double For(Orientation o) => _cf[o];

    /// <summary>Indicative default set — flagged for verification (see class remarks).</summary>
    public static BcaCorrectionFactors IndicativeDefault { get; } = new(
        new Dictionary<Orientation, double>
        {
            [Orientation.N] = 0.90,
            [Orientation.NE] = 1.08,
            [Orientation.E] = 1.23,
            [Orientation.SE] = 1.20,
            [Orientation.S] = 0.96,
            [Orientation.SW] = 1.13,
            [Orientation.W] = 1.20,
            [Orientation.NW] = 1.08,
        });
}
