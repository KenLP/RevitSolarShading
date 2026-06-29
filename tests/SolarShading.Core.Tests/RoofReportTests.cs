using SolarShading.Core.Ettv;
using SolarShading.Core.Reporting;
using Xunit;

namespace SolarShading.Core.Tests;

public class RoofRttvTests
{
    private static RoofData Roof(double skylightArea, double roofU = 0.5) => new()
    {
        GrossRoofAreaM2 = 100,
        SkylightAreaM2 = skylightArea,
        RoofUValue = roofU,
        SkylightGlazing = GlazingLibrary.DoubleLowE,
    };

    [Fact]
    public void Opaque_roof_rttv_is_just_conduction()
    {
        var r = RoofAssessment.Evaluate(new[] { Roof(skylightArea: 0) }, ComplianceProfiles.SingaporeBca);
        // 12 · α(1) · (1−0) · 0.5 = 6
        Assert.True(r.Applicable);
        Assert.Equal(6.0, r.Rttv, 3);
        Assert.True(r.Passes); // ≤ 25
    }

    [Fact]
    public void Skylight_raises_rttv()
    {
        double opaque = RoofAssessment.Evaluate(new[] { Roof(0) }, ComplianceProfiles.SingaporeBca).Rttv;
        double withSky = RoofAssessment.Evaluate(new[] { Roof(20) }, ComplianceProfiles.SingaporeBca).Rttv;
        Assert.True(withSky > opaque);
    }

    [Fact]
    public void No_roof_is_not_applicable()
    {
        var r = RoofAssessment.Evaluate(Array.Empty<RoofData>(), ComplianceProfiles.SingaporeBca);
        Assert.False(r.Applicable);
    }
}

public class ComplianceReportTests
{
    [Fact]
    public void Report_html_contains_code_verdict_and_facade_rows()
    {
        var facade = new FacadeData
        {
            Orientation = Orientation.W, GrossWallAreaM2 = 100, WindowAreaM2 = 40,
            WallUValue = 2.0, Glazing = GlazingLibrary.DoubleLowE, ExternalShadingCoefficient = 0.6,
        };
        var assess = new EttvAssessment(ComplianceProfiles.SingaporeBca);
        EnvelopeEttvResult ettv = assess.EvaluateEnvelope(new[] { facade });
        EnvelopeRttvResult rttv = RoofAssessment.Evaluate(
            new[] { new RoofData { GrossRoofAreaM2 = 100, SkylightAreaM2 = 0, RoofUValue = 0.5, SkylightGlazing = GlazingLibrary.DoubleLowE } },
            ComplianceProfiles.SingaporeBca);

        string html = ComplianceReport.BuildHtml(new ReportData
        {
            ProjectName = "Test Tower", GeneratedOn = "2026-06-28 12:00",
            Profile = ComplianceProfiles.SingaporeBca, Ettv = ettv, Facades = new[] { facade }, Rttv = rttv,
        });

        Assert.Contains("BCA Green Mark", html);
        Assert.Contains("Test Tower", html);
        Assert.Contains("RTTV", html);
        Assert.Contains("W", html); // orientation column
        Assert.True(html.Contains("PASS") || html.Contains("FAIL"));
        Assert.StartsWith("<!DOCTYPE html>", html);
    }
}
