namespace SolarShading.Core.Ettv;

/// <summary>A glazing system: thermal transmittance and its own shading coefficient (SC1).</summary>
public sealed record Glazing(string Name, double UValue, double ShadingCoefficient);

/// <summary>
/// Small starter library of common glazing systems. Values are INDICATIVE typical
/// figures for early-stage modelling and must be replaced with the actual product's
/// certified U-value and SC before any compliance submission.
/// </summary>
public static class GlazingLibrary
{
    public static readonly Glazing SingleClear6mm = new("Single clear 6mm", UValue: 5.8, ShadingCoefficient: 0.95);
    public static readonly Glazing SingleTinted6mm = new("Single tinted 6mm", UValue: 5.7, ShadingCoefficient: 0.70);
    public static readonly Glazing DoubleClear = new("Double glazed clear", UValue: 2.8, ShadingCoefficient: 0.78);
    public static readonly Glazing DoubleLowE = new("Double glazed low-E", UValue: 1.8, ShadingCoefficient: 0.40);

    public static IReadOnlyList<Glazing> All { get; } =
        new[] { SingleClear6mm, SingleTinted6mm, DoubleClear, DoubleLowE };
}
