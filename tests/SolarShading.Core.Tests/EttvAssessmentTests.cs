using SolarShading.Core.Ettv;
using Xunit;

namespace SolarShading.Core.Tests;

public class EttvAssessmentTests
{
    [Fact]
    public void Orientation_classified_from_azimuth()
    {
        Assert.Equal(Orientation.N, OrientationExtensions.FromAzimuth(0));
        Assert.Equal(Orientation.E, OrientationExtensions.FromAzimuth(90));
        Assert.Equal(Orientation.S, OrientationExtensions.FromAzimuth(179));
        Assert.Equal(Orientation.W, OrientationExtensions.FromAzimuth(271));
        Assert.Equal(Orientation.N, OrientationExtensions.FromAzimuth(359));
    }

    [Fact]
    public void Facade_wwr_and_ettv_computed()
    {
        var assess = new EttvAssessment();
        var facade = new FacadeData
        {
            Orientation = Orientation.W,
            GrossWallAreaM2 = 100,
            WindowAreaM2 = 40,
            WallUValue = 2.0,
            Glazing = GlazingLibrary.DoubleLowE,
            ExternalShadingCoefficient = 0.6,
        };
        FacadeEttvResult r = assess.EvaluateFacade(facade);
        Assert.Equal(0.4, r.WindowToWallRatio, 6);
        Assert.True(r.Ettv > 0);
    }

    [Fact]
    public void Heavy_glazing_without_shading_fails_and_shading_helps()
    {
        var assess = new EttvAssessment();

        FacadeData Build(Glazing g, double sc2) => new()
        {
            Orientation = Orientation.W,   // high correction factor
            GrossWallAreaM2 = 100,
            WindowAreaM2 = 80,             // 80% WWR, solar-dominated
            WallUValue = 3.0,
            Glazing = g,
            ExternalShadingCoefficient = sc2,
        };

        var bad = assess.EvaluateEnvelope(new[] { Build(GlazingLibrary.SingleClear6mm, 1.0) });
        var good = assess.EvaluateEnvelope(new[] { Build(GlazingLibrary.DoubleLowE, 0.4) });

        Assert.False(bad.Passes);                 // unshaded clear glass, west façade
        Assert.True(good.Ettv < bad.Ettv);        // low-E + external shading lowers ETTV
    }

    [Fact]
    public void Envelope_ettv_is_area_weighted()
    {
        var assess = new EttvAssessment();
        var facades = new[]
        {
            new FacadeData { Orientation = Orientation.N, GrossWallAreaM2 = 100, WindowAreaM2 = 20,
                WallUValue = 2.0, Glazing = GlazingLibrary.DoubleLowE, ExternalShadingCoefficient = 0.5 },
            new FacadeData { Orientation = Orientation.W, GrossWallAreaM2 = 300, WindowAreaM2 = 60,
                WallUValue = 2.0, Glazing = GlazingLibrary.DoubleLowE, ExternalShadingCoefficient = 0.5 },
        };
        var env = assess.EvaluateEnvelope(facades);

        double w = (env.Facades[0].Ettv * 100 + env.Facades[1].Ettv * 300) / 400.0;
        Assert.Equal(w, env.Ettv, 6);
    }
}
