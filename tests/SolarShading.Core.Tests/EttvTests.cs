using SolarShading.Core.Ettv;
using Xunit;

namespace SolarShading.Core.Tests;

public class EttvTests
{
    [Fact]
    public void Ettv_formula_matches_hand_computation()
    {
        var inputs = new EttvInputs
        {
            WindowToWallRatio = 0.4,
            WallUValue = 2.0,
            FenestrationUValue = 5.6,
            CorrectionFactor = 0.9,
            GlassShadingCoefficient = 0.5,
            ExternalShadingCoefficient = 0.8,
        };

        // 12·0.6·2.0 + 3.4·0.4·5.6 + 211·0.4·0.9·(0.5·0.8)
        double expected = 14.4 + 7.616 + 30.384; // = 52.4
        Assert.Equal(expected, BcaEttv.Compute(inputs), 3);
    }

    [Fact]
    public void Better_external_shading_lowers_ettv()
    {
        EttvInputs Base(double sc2) => new()
        {
            WindowToWallRatio = 0.5,
            WallUValue = 2.0,
            FenestrationUValue = 5.6,
            CorrectionFactor = 1.0,
            GlassShadingCoefficient = 0.6,
            ExternalShadingCoefficient = sc2,
        };

        Assert.True(BcaEttv.Compute(Base(0.4)) < BcaEttv.Compute(Base(0.9)));
    }

    [Fact]
    public void EffectiveSc2_is_solar_weighted_sunlit_fraction()
    {
        var samples = new[]
        {
            new ShadeSample(sunlitFraction: 1.0, weight: 100), // strong sun, fully lit
            new ShadeSample(sunlitFraction: 0.0, weight: 100), // strong sun, fully shaded
            new ShadeSample(sunlitFraction: 0.5, weight: 0),   // no sun -> ignored
        };
        Assert.Equal(0.5, ShadingCoefficient.EffectiveSc2(samples), 6);
    }

    [Fact]
    public void EffectiveSc2_defaults_to_unshaded_when_no_weight()
    {
        var samples = new[] { new ShadeSample(0.3, 0.0) };
        Assert.Equal(1.0, ShadingCoefficient.EffectiveSc2(samples), 6);
    }
}
