# Expansion & Optimization Plan — Standards-Driven

> Where the tool goes next, grounded in the research (`Shading Computation research.md`)
> and what our geometric engine can credibly deliver.

## 0. Positioning (the honest boundary)

Our engine is a **geometric solar engine**: exact sun position + silhouette projection +
2D polygon clipping. Per the research, this is the *correct* tool for everything that is
**sun-geometry-based** — shadows, sun-hours, shading coefficients, solar envelopes — and is
**not** a substitute for a photometric/CBDM engine (Radiance) for illuminance-based metrics
(sDA, ASE, VSC, daylight factor, glare). The strategy is therefore:

> **Own the geometric-compliance space end-to-end; hand off the photometric/CBDM space to
> Radiance/Honeybee by exporting geometry + sensor grids.**

This keeps us defensible (we're not re-implementing Radiance badly) and broad (geometric
compliance covers a large, underserved market, especially SE Asia ETTV/OTTV).

## 1. What standards actually need vs. our engine

| Standard / market | Metric | Engine fit | Status |
|---|---|---|---|
| **BCA Green Mark (SG)** | ETTV (SC2 from shading) | ✅ exact | **have** |
| **MS1525 (MY)** | OTTV + **RTTV** (roofs) | ✅ extend to roofs | extend |
| **QCVN 09:2017 (VN)** | OTTV-style / SHGC, envelope | ✅ extend | extend |
| **Thailand BEC, Indonesia SNI** | OTTV/RTTV | ✅ profile | extend |
| **EN 17037 — sunlight exposure** | sunlight hours on a reference date | ✅ sun-hours | **new module** |
| **BRE 209 — amenity / overshadowing** | ≥2 h sun on 21 Mar over ≥50 % of area | ✅ sun-hours on a grid | **new module** |
| **BRE 209 — APSH** | annual probable sunlight hours (S-facing) | ✅ annual sun-hours | new module |
| **Solar access / zoning** | solar envelope volume | ✅ cutting planes from sun vectors | new module |
| **ASHRAE 90.1 / Title 24 / PHPP** | shading reduction factor | ✅ export shading factors | export |
| **BRE 209 — VSC / no-sky-line** | sky-vault illuminance ratio | ⚠️ geometric approx only | partial |
| **LEED / EN 17037 daylight** | sDA / ASE / DF / UDI | ❌ needs Radiance | **export to Honeybee** |
| **Glare (DGP)** | luminance | ❌ needs Radiance | out of scope |

→ The geometric engine credibly serves the entire **OTTV/ETTV family**, the entire
**sunlight-hours / overshadowing family**, **solar envelopes**, and **shading-factor export**.
That is a coherent product: *"Geometric solar & shading compliance for the building envelope."*

## 2. The key optimization: a Compliance-Profile system

Today the reference dates, hours, thresholds and coefficients are constants in code. Standards
specify these **exactly and differently per jurisdiction**. The scalable design is a declarative
**`ComplianceProfile`**:

```
ComplianceProfile {
  Code            // "BCA Green Mark 2021", "QCVN 09:2017", "BRE 209:2022 amenity", ...
  Metric          // ETTV | OTTV | RTTV | SunlightHours | Overshadowing | SolarEnvelope
  ReferenceDates  // e.g. [21 Jun], [21 Mar], [annual]
  Hours           // e.g. 7–18, or "sun-up", or APSH annual set
  GridResolutionM // for area-based metrics (e.g. 0.5 m amenity grid)
  Thresholds      // e.g. ETTV ≤ 50 W/m²; ≥2 h over ≥50 % area; SC2 charts
  Coefficients    // formula constants + orientation correction factors (flagged: verify)
  Report          // submission template id
}
```

Adding a new country/code becomes **config, not a rewrite**. This is the single highest-leverage
change for "real standard requirements" — it turns the tool from a BCA-ETTV calculator into a
multi-jurisdiction compliance platform. All regulatory constants stay isolated and flagged
"verify against the edition in force" (per the research's caveats).

## 3. Algorithm extensions (and the right method for each)

The research distinguishes two area methods: **analytic clipping** (exact, what we use for
shaded-area-at-an-instant) and **raster/grid sampling** (scales to complex queries). We should
use each where it fits:

- **Shaded area / SC2 (ETTV)** — analytic clipping. ✅ done.
- **Sun-hours on a surface or ground area** (EN 17037, BRE amenity/APSH) — **raster grid**: lay a
  grid (e.g. 0.5 m), for each cell ray-cast to the sun each timestep, count sunlit hours, then
  report "% of area with ≥ N hours". This is the standard method (Ladybug "Direct Sun Hours").
  Reuses our sun engine + occluder set; add a point-in-shadow test (already implicit in projection).
- **Overshadowing / transient shadow** — extend *building-shadow-on-ground* to a **time series**:
  shadow polygons per hour, union/accumulate, plus "% of garden shaded > X h". Analytic clipping
  for the footprints; accumulate over time.
- **Solar envelope** — intersect half-spaces from sun vectors at the code's cut-off times. New but
  small; pure geometry.
- **RTTV (roofs)** — same shaded-area machinery with a horizontal receiver; add the roof thermal terms.

## 4. Interop & deliverables (what gets a project approved)

- **Submission reports** per code (PDF/Excel from the existing CSV/result pipeline) — this is the
  actual deliverable architects hand to authorities; a strong differentiator.
- **Export to Honeybee/Radiance** (geometry + sensor grids) for the CBDM side (sDA/ASE) — we become
  the BIM-side geometry front-end, not a competitor to Radiance.
- **gbXML / IFC** export for energy models (shading factors → EnergyPlus/PHPP).

## 5. Phased roadmap

**Phase A — Deepen the wedge (highest ROI):**
1. Compliance-Profile system (§2).
2. Multi-code ETTV/OTTV + RTTV: QCVN 09 (VN), MS1525 (MY), BCA (SG), Thailand BEC.
3. Per-code submission report templates.

**Phase B — Adjacent geometric modules (reuse the engine):**
4. Sun-hours module (raster) → EN 17037 sunlight exposure, BRE 209 amenity (2 h/21 Mar), APSH.
5. Overshadowing / transient shadow time-series (neighbour & garden impact).
6. Shading-factor export (ASHRAE/Title 24/PHPP).

**Phase C — Interop & scale:**
7. Honeybee/Radiance geometry+grid export (hand off CBDM).
8. gbXML/IFC; urban/multi-building batching with progress + LOD.

## 6. Optimization themes (cross-cutting)

- **Correctness per standard**: encode exact reference dates/hours/grids/thresholds in profiles;
  a validation case per metric (sun-hours vs. Ladybug, shaded-area vs. hand calc).
- **Performance at scale**: the parallel pipeline + culling already handle whole façades; for
  raster sun-hours add per-cell parallelism + a BVH over occluders; offer LOD (timestep stride,
  grid coarseness) for fast preview vs. submission-grade.
- **Trust**: keep all regulatory constants isolated and flagged for verification; ship the
  validation suite; never present geometric output as photometric/CBDM compliance.

## 7. Recommendation

Lead with **Phase A** (Compliance-Profile system + multi-code OTTV/ETTV + reports) — it converts the
working BCA engine into a multi-jurisdiction SE-Asia compliance product, which is the clearest,
most-defensible market need. Then **Phase B** (sun-hours / overshadowing) broadens into the
international geometric-compliance space using the same engine. Keep CBDM as **export**, never rebuild.
