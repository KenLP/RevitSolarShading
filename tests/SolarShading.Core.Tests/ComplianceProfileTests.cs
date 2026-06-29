using SolarShading.Core.Ettv;
using Xunit;

namespace SolarShading.Core.Tests;

public class ComplianceProfileTests
{
    [Fact]
    public void Bca_profile_reproduces_known_ettv()
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
        // 12·0.6·2.0 + 3.4·0.4·5.6 + 211·0.4·0.9·(0.5·0.8) = 52.4
        Assert.Equal(52.4, EnvelopeThermal.Compute(ComplianceProfiles.SingaporeBca.WallCoefficients, inputs), 3);
    }

    [Fact]
    public void Wall_absorptance_lowers_the_conduction_term()
    {
        EttvInputs Build(double alpha) => new()
        {
            WindowToWallRatio = 0.3,
            WallUValue = 2.0,
            FenestrationUValue = 5.6,
            CorrectionFactor = 1.0,
            GlassShadingCoefficient = 0.6,
            ExternalShadingCoefficient = 0.7,
            WallAbsorptance = alpha,
        };
        var coeffs = ComplianceProfiles.MalaysiaMs1525.WallCoefficients;
        Assert.True(EnvelopeThermal.Compute(coeffs, Build(0.4)) < EnvelopeThermal.Compute(coeffs, Build(0.9)));
    }

    [Fact]
    public void Registry_exposes_three_codes_and_lookup_works()
    {
        Assert.Equal(3, ComplianceProfiles.All.Count);
        Assert.Equal("Singapore", ComplianceProfiles.ByCode("BCA Green Mark (ETTV)").Jurisdiction);
        Assert.Equal("Malaysia", ComplianceProfiles.ByCode("MS1525 (OTTV)").Jurisdiction);
        Assert.Equal("Vietnam", ComplianceProfiles.ByCode("QCVN 09:2017 (OTTV)").Jurisdiction);
        // Unknown code falls back to BCA.
        Assert.Equal("Singapore", ComplianceProfiles.ByCode("nonsense").Jurisdiction);
    }

    [Fact]
    public void Thresholds_match_published_values()
    {
        Assert.Equal(50.0, ComplianceProfiles.SingaporeBca.WallThresholdWm2);
        Assert.Equal(50.0, ComplianceProfiles.MalaysiaMs1525.WallThresholdWm2);
        Assert.Equal(60.0, ComplianceProfiles.VietnamQcvn09.WallThresholdWm2);
        Assert.Equal(25.0, ComplianceProfiles.VietnamQcvn09.Roof!.RttvThresholdWm2);
    }

    [Fact]
    public void Assessment_uses_selected_profile_threshold_and_code()
    {
        var facade = new FacadeData
        {
            Orientation = Orientation.W,
            GrossWallAreaM2 = 100,
            WindowAreaM2 = 40,
            WallUValue = 2.0,
            Glazing = GlazingLibrary.DoubleLowE,
            ExternalShadingCoefficient = 0.6,
        };
        var vn = new EttvAssessment(ComplianceProfiles.VietnamQcvn09).EvaluateEnvelope(new[] { facade });
        Assert.Equal("QCVN 09:2017 (OTTV)", vn.Code);
        Assert.Equal(60.0, vn.Threshold);
    }

    [Fact]
    public void Roof_rttv_passfail_uses_roof_threshold()
    {
        ComplianceProfile p = ComplianceProfiles.MalaysiaMs1525;
        Assert.True(p.RoofPasses(20.0));   // ≤ 25
        Assert.False(p.RoofPasses(30.0));  // > 25
    }
}
