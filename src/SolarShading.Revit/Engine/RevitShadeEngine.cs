using System.Threading.Tasks;
using Autodesk.Revit.DB;
using SolarShading.Core.Ettv;
using SolarShading.Core.Geometry;
using SolarShading.Core.Solar;
using SolarShading.Revit.Geometry;
using SolarShading.Revit.Solar;

namespace SolarShading.Revit.Engine;

/// <summary>Shading result for one window at one instant.</summary>
public readonly struct InstantShade
{
    public DateTimeOffset Instant { get; init; }
    public double AltitudeDeg { get; init; }
    public bool Lit { get; init; }
    public double ShadedFraction { get; init; }
    public double ShadedAreaM2 { get; init; }
}

/// <summary>Full shading analysis of one window across all requested instants.</summary>
public sealed class WindowShadeAnalysis
{
    public required ElementId WindowId { get; init; }
    public required double WindowAreaM2 { get; init; }
    public required IReadOnlyList<InstantShade> Instants { get; init; }
    /// <summary>Effective external shading coefficient (SC2) for ETTV.</summary>
    public required double EffectiveSc2 { get; init; }
}

/// <summary>A window plus the shading devices near it, for batch analysis.</summary>
public readonly record struct ShadeInput(FamilyInstance Window, IReadOnlyList<Element> Devices);

/// <summary>
/// Orchestrates the shading computation. Runs in three phases for whole-model speed:
/// (A) on the Revit thread, extract each window's receiver and its occluders (cached by
/// element id, T1); (B) in parallel across windows, the pure-maths projection + clipping
/// (T2) — no Revit API is touched, and the sun for each instant is precomputed once;
/// (C) the caller writes results in a single transaction. Bounding-box / back-face culling
/// and polygon simplification (T3–T5) happen inside the core calculator.
/// </summary>
public sealed class RevitShadeEngine
{
    private readonly SiteSun _sun;
    private readonly OccluderCache _cache;
    private readonly ShadingCalculator _calc = new();
    private readonly Func<DateTimeOffset, Vec3, Vec3, double, double> _weight;

    /// <param name="weight">
    /// (instant, toSun, surfaceNormal, altitudeDeg) → sample weight for SC2 aggregation.
    /// Default is the ASHRAE clear-sky beam irradiance incident on the window.
    /// </param>
    public RevitShadeEngine(Document doc, Func<DateTimeOffset, Vec3, Vec3, double, double>? weight = null)
    {
        _sun = new SiteSun(doc);
        _cache = new OccluderCache(doc);
        _weight = weight ?? DefaultWeight;
    }

    public ModelSun SunAt(DateTimeOffset instant) => _sun.ForInstant(instant);

    /// <summary>
    /// Analyze many windows. Phase A (this thread) builds the receivers and resolves cached
    /// occluders; phase B runs the per-window maths in parallel. Returns one analysis per
    /// window that yielded a valid receiver.
    /// </summary>
    public IReadOnlyList<WindowShadeAnalysis> AnalyzeBatch(
        IReadOnlyList<ShadeInput> inputs, IReadOnlyList<DateTimeOffset> instants)
    {
        // ---- Phase A: Revit-thread extraction ----
        var jobs = new List<Job>(inputs.Count);
        foreach (ShadeInput input in inputs)
        {
            WindowReceiver? receiver = WindowReceiver.FromWindow(input.Window);
            if (receiver == null)
                continue;

            var occluders = new List<OccluderObject>(_cache.Get(input.Devices));
            // Shading fins modelled inside the window family (self-shading).
            OccluderObject? self = RevitGeometryExtractor.ToSelfShadingOccluder(input.Window, receiver);
            if (self != null)
                occluders.Add(self);

            jobs.Add(new Job(input.Window.Id, receiver, occluders));
        }

        var suns = new ModelSun[instants.Count];
        for (int i = 0; i < instants.Count; i++)
            suns[i] = _sun.ForInstant(instants[i]);

        // ---- Phase B: parallel pure-maths ----
        var results = new WindowShadeAnalysis[jobs.Count];
        Parallel.For(0, jobs.Count, i => results[i] = ComputeJob(jobs[i], suns, instants));
        return results;
    }

    /// <summary>Analyze a single window (convenience wrapper over <see cref="AnalyzeBatch"/>).</summary>
    public WindowShadeAnalysis? AnalyzeWindow(
        FamilyInstance window, IReadOnlyList<Element> shadingDevices, IReadOnlyList<DateTimeOffset> instants)
        => AnalyzeBatch(new[] { new ShadeInput(window, shadingDevices) }, instants).FirstOrDefault();

    /// <summary>Shaded region of a window at one instant, for drawing an overlay. Null if not applicable.</summary>
    public (WindowReceiver Receiver, Clipper2Lib.PathsD Region)? RegionAt(
        FamilyInstance window, IReadOnlyList<Element> shadingDevices, DateTimeOffset instant)
    {
        WindowReceiver? receiver = WindowReceiver.FromWindow(window);
        if (receiver == null)
            return null;

        ModelSun sun = _sun.ForInstant(instant);
        if (!sun.IsDaytime || !ShadingCalculator.FacesSun(receiver.OutwardNormal, sun.ToSun))
            return (receiver, new Clipper2Lib.PathsD());

        var faces = new List<OccluderFace>();
        foreach (OccluderObject occ in _cache.Get(shadingDevices))
            faces.AddRange(occ.Faces);
        OccluderObject? self = RevitGeometryExtractor.ToSelfShadingOccluder(window, receiver);
        if (self != null)
            faces.AddRange(self.Faces);

        var (_, region) = _calc.ComputeRegion(receiver.Outline, receiver.Plane, faces, sun.ToSun);
        return (receiver, region);
    }

    private WindowShadeAnalysis ComputeJob(Job job, ModelSun[] suns, IReadOnlyList<DateTimeOffset> instants)
    {
        var samples = new List<ShadeSample>();
        var results = new List<InstantShade>(instants.Count);

        for (int k = 0; k < instants.Count; k++)
        {
            ModelSun sun = suns[k];
            DateTimeOffset instant = instants[k];
            bool lit = sun.IsDaytime && ShadingCalculator.FacesSun(job.Receiver.OutwardNormal, sun.ToSun);
            if (!lit)
            {
                results.Add(new InstantShade
                {
                    Instant = instant, AltitudeDeg = sun.AltitudeDeg, Lit = false,
                    ShadedFraction = 0, ShadedAreaM2 = 0,
                });
                continue;
            }

            ShadeResult r = _calc.Compute(job.Receiver.Outline, job.Receiver.Plane, job.Occluders, sun.ToSun);
            double w = _weight(instant, sun.ToSun, job.Receiver.OutwardNormal, sun.AltitudeDeg);
            samples.Add(new ShadeSample(r.SunlitFraction, w));

            results.Add(new InstantShade
            {
                Instant = instant, AltitudeDeg = sun.AltitudeDeg, Lit = true,
                ShadedFraction = r.ShadedFraction, ShadedAreaM2 = r.ShadedAreaM2,
            });
        }

        return new WindowShadeAnalysis
        {
            WindowId = job.Id,
            WindowAreaM2 = job.Receiver.AreaM2,
            Instants = results,
            EffectiveSc2 = ShadingCoefficient.EffectiveSc2(samples),
        };
    }

    private static double DefaultWeight(DateTimeOffset instant, Vec3 toSun, Vec3 normal, double altitudeDeg)
        => ClearSkyIrradiance.OnSurface(instant.Month, altitudeDeg, toSun, normal);

    private sealed class Job
    {
        public ElementId Id { get; }
        public WindowReceiver Receiver { get; }
        public IReadOnlyList<OccluderObject> Occluders { get; }
        public Job(ElementId id, WindowReceiver receiver, IReadOnlyList<OccluderObject> occluders)
        {
            Id = id;
            Receiver = receiver;
            Occluders = occluders;
        }
    }
}
