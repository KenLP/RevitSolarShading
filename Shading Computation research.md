# Accurate Shadow and Shading Computation in 3D Software for the Built Environment
## Tính toán bóng đổ và che nắng chính xác trong phần mềm 3D cho môi trường xây dựng

## TL;DR
- **For physically-accurate work, the chain is settled:** regulatory metrics (EN 17037 sunlight hours, BRE 209 VSC/APSH/NSL, LEED/IES LM-83 sDA & ASE) map to two algorithm families — deterministic **sun-ray casting** (sunlight hours, shadow footprints, solar access) and backward **ray tracing via Radiance** (illuminance, sDA, ASE, daylight factor) — implemented in a small set of validated tools (Radiance, ClimateStudio, Honeybee/Ladybug, IES VE, DAYSIM, DesignBuilder, and UK rights-of-light packages such as MBS Waldram Tools).
- **Visual shadows ≠ validated solar analysis.** Game-engine/BIM shadow maps and Revit "sun studies" are geometrically correct for *position* but are not energy- or compliance-grade; for credits and planning you need a CIE 171-validated engine and a physically based sky model (Perez all-weather), plus NREL SPA sun positions.
- **The field is moving from static to climate-based metrics and from CPU to GPU.** GPU path tracing (ClimateStudio, Accelerad), AI surrogate models, and cloud early-stage tools (Autodesk Forma) are collapsing run times from hours to seconds, while open problems remain in soft-shadow/penumbra accuracy, inter-reflection, vegetation, weather/sky uncertainty, urban-scale cost, and the gap between simplified standard methods and full simulation.

*Tóm tắt: Có hai họ thuật toán chính — chiếu tia mặt trời tất định (giờ nắng, diện tích bóng đổ, quyền tiếp cận ánh nắng) và dò tia ngược qua Radiance (độ rọi, sDA, ASE). Bóng đổ trực quan trong game engine/BIM không phải là phân tích hợp chuẩn. Xu hướng: chuyển từ chỉ số tĩnh sang chỉ số dựa trên khí hậu, từ CPU sang GPU.*

## Key Findings

1. **Two distinct application domains use overlapping math.** (a) *Building energy & daylighting* (solar gain, cooling/heating loads, sDA/ASE/DA/UDI, glare) is dominated by backward ray tracing and the Radiance daylight-coefficient/3-/5-phase methods. (b) *Right-to-light / overshadowing* (VSC, APSH, no-sky line, sun-on-ground, shadow footprints, solar envelopes) is dominated by deterministic geometric sun-ray casting and, for legal rights of light in the UK, the Waldram sky-factor method.

2. **Sun position is essentially a solved problem.** Reda & Andreas (2004, *Solar Energy* 76(5):577–589; NREL/TP-560-34302, rev. Jan 2008) describe "a step by step procedure for implementing an algorithm to calculate the solar zenith and azimuth angles in the period from the year −2000 to 6000, with uncertainties of ±0.0003°." They frame the prior state of the art (e.g., Spencer 1971 Fourier-series models) as: "The best uncertainty achieved in most of these articles is greater than ±0.01°." Since site survey, atmospheric refraction and cloud uncertainty dominate, the simpler models are adequate for architectural shadow/solar work.

3. **The sky model, not the sun, is the main accuracy lever.** The Perez all-weather model — Perez, Ineichen, Seals, Michalsky & Stewart (1990), "Modeling daylight availability and irradiance components from direct and global irradiance," *Solar Energy* 44(5):271–289; and Perez, Seals & Michalsky (1993), "All-weather model for sky luminance distribution," *Solar Energy* 50(3):235–245 — is the de-facto standard for converting direct-normal/diffuse-horizontal irradiance from TMY weather files into a sky luminance/radiance distribution; it is what Radiance's `gendaymtx`/`gendaylit` and EnergyPlus implement.

4. **Standards have shifted from static daylight factor to climate-based daylight modeling (CBDM).** IES LM-83 (sDA 300/50%, ASE 1000,250) underpins LEED v4/v4.1 daylight credit Option 1; EN 17037:2018 introduced target daylight factor + sunlight-exposure hours; BRE BR209 (3rd ed., 2022) re-aligned UK planning practice with EN 17037 after BS 8206-2 was withdrawn.

5. **GPU acceleration is the dominant performance story.** ClimateStudio (Solemma) runs Radiance in a cacheless progressive path-tracing mode with AI denoising, computing sDA for a side-lit office in ~10 seconds. The Accelerad project page (MIT, Nathaniel Jones) states "the RTX architecture make[s] Accelerad up to one hundred times faster than Radiance thanks to OptiX™"; Jones's MIT validation reports "similar accuracy to Radiance at a speedup of 16 to 44 times… 10 times faster than DAYSIM and 25 times faster than the five-phase method."

## Details

### PART 1 — Design standards and regulatory requirements (international)

**EN 17037:2018 "Daylight in Buildings" (CEN/TC 169).** Four criteria, each with minimum/medium/high recommendation levels: (1) **daylight provision** — either a target daylight factor derived from the median external diffuse horizontal illuminance, or an absolute-illuminance climate-based method requiring 300 lx over 50% of the reference plane and ≥100 lx over 95% of the space for at least half the daylight hours; (2) **view out** (width, distance, layers); (3) **exposure to sunlight** — a minimum number of sunlight-exposure hours on a reference date (the standard requires that at least one habitable room in dwellings, hospital patient rooms and nursery playrooms meets the minimum sunlight-exposure level); (4) **protection from glare** via Daylight Glare Probability (DGP). The sunlight criterion is the part most directly served by shadow/sun-hour computation. Slovenia's national guidance, for example, requires the building "collecting area" to receive sun ≥2 h on 21 December, ≥4 h at equinoxes, ≥6 h at summer solstice.

**BRE BR209 (3rd ed., 2022) "Site Layout Planning for Daylight and Sunlight."** Non-statutory but the primary UK planning reference (supersedes the 2011 edition; aligns with BS EN 17037). Key metrics and thresholds:
- **Vertical Sky Component (VSC):** ratio of direct sky illuminance on the vertical window face to that from an unobstructed hemisphere. Target ≥27% for good daylight; if a development reduces a neighbour's VSC below 0.8× (>20% loss) the impact is considered significant.
- **No Sky Line / Daylight Distribution (NSL):** the contour on the working plane (850 mm) beyond which no sky is visible; ≥50% of a room should see sky, and a reduction below 0.8× former area is significant.
- **Annual Probable Sunlight Hours (APSH):** desirable ≥25% of annual probable sunlight hours including ≥5% in winter (Sept–Mar); assessed only for windows within 90° of due south; reduction below 0.8× former value is noticeable.
- **Overshadowing of gardens/amenity (sun-on-ground):** ≥50% of the garden/amenity area should receive ≥2 h of sun on 21 March. Larger developments use **transient overshadowing** plots showing shaded areas by time of day and date.
- Screening angles: 25° (for neighbouring buildings) and ~43° (for adjoining development land, measured from 1.6 m at the boundary).

**UK Rights of Light (legal, distinct from BR209 planning).** Based on the Prescription Act 1832 / Rights of Light Act 1959; "ancient lights" easements after 20 years' enjoyment. Uses the **Waldram method**: under a uniform CIE overcast sky, the **sky factor** is computed; Percy Waldram's **0.2% sky-factor "grumble line"** (≈1 foot-candle ≈10 lux under a ~5000 lux sky) marks the threshold of adequacy, measured at the **850 mm** working plane. The **50/50 rule**: a room is treated as adequately lit if ≥50% of its working-plane area receives ≥0.2% sky factor. Practitioners plot **Waldram diagrams** (each grid square = 0.1% of the sky dome; two squares = the 0.2% threshold). The 0.2% standard is itself contested in the literature (Defoe, Chynoweth) as a "rule of thumb" lacking empirical basis; controlled experiments suggested ~0.56% (28 lux) is needed. The 2025 *Cooper & Powell v Ludgate House Ltd* judgment reaffirmed Waldram as the industry-standard method ("accepted as the appropriate standard across the industry").

**Solar envelope / solar access (US).** Ralph Knowles (USC, 1974–1981, *Sun Rhythm Form*) defined the **solar envelope**: "a container to regulate development within limits derived from the sun's relative motion" — the largest buildable volume that avoids casting shadow beyond property lines during chosen "cut-off times" (e.g., 9 am–3 pm winter, 7 am–7 pm summer). Implemented in US solar-access ordinances (e.g., Ashland OR §18.70, Santa Barbara) and zoning precedents (New York 1916 setback law; Zurich "two-hour shadow" rule). Influenced Jeanne Gang's Solar Carve Tower on the High Line.

**Green building / energy rating systems requiring shading or solar studies:**
- **LEED v4/v4.1** EQ Daylight credit, Option 1 (simulation): per IES LM-83, sDA 300/50% of 40% = 1 credit (added in v4.1), 55% = 2 credits, 75% = 3 credits, over regularly occupied area, with ASE 1000,250 ≤10% of floor area (in v4.0 over-lit areas are disqualified; in v4.1 ASE >10% must be justified in writing). Note (per Autodesk Insight documentation): the 1000-lux ASE threshold is "very, very specific to the Radiance 5-phase method… LM83 was developed exclusively using Radiance models of 61 sample buildings."
- **BREEAM** Hea 01 (Visual comfort/daylighting): average and minimum point daylight illuminance criteria (Option 4b) or EN 17037 metrics.
- **WELL** (light/circadian), **ASHRAE 90.1** (envelope SHGC/U-factor limits by climate zone; VT/SHGC ratio ≥1.1–1.25; daylight-area labeling; Appendix G modeling requires a site plan of shading buildings/topography), **ASHRAE 55** (thermal comfort), **California Title 24 Part 6** (prescriptive max SHGC of 0.20–0.23 for most climate zones; exterior shading devices like awnings/sunscreens credited via the CF1R-ENV-03-E SHGC worksheet), **Passive House / PHPP** (monthly shading reduction factors).

**Solar position / irradiance reference data:** NREL SPA, ASHRAE clear-sky model (Fundamentals Ch. 14), Perez all-weather sky, TMY/EPW weather files, IES LM-83 CBDM methodology.

### PART 2 — Algorithms and theory

**Solar geometry.** Inputs: solar declination, hour angle, latitude → solar altitude and azimuth. Hierarchy of accuracy: full **NREL SPA** (heliocentric VSOP87 terms + nutation, aberration, ΔT, topocentric and refraction corrections; ±0.0003°) → Grena 1–5 algorithms → Spencer (1971) Fourier approximation (~±0.01°) → simple textbook declination formulas. For architectural shadow/solar work the simpler models suffice; full SPA matters for CSP heliostat tracking.

**Shadow computation algorithm families:**
- **Planar/projection shadows:** project geometry onto a ground plane via a projection matrix; cheap, only flat receivers, used for quick massing shadow on a single plane.
- **Shadow volumes (stencil shadows):** Frank Crow, 1977; build polygonal volumes of occluded space, use the stencil buffer (z-pass/z-fail) to test pixels. Pixel-accurate hard shadows, expensive fill rate.
- **Shadow mapping:** Lance Williams, 1978; render a depth map from the light's view, compare scene depth. Image-space, GPU-standard, resolution-limited (aliasing/"shadow acne"); the basis of cascaded shadow maps and Unreal Engine's Virtual Shadow Maps.
- **Ray casting / ray tracing for solar:** cast a ray from each sensor/surface point toward the sun direction; if the ray hits geometry, the point is shadowed. This is the core of "sunlight hours" and "direct sun hours" analysis. Acceleration via **BVH (bounding volume hierarchy)** and ray-triangle intersection (Möller–Trumbore). **Distributed ray tracing** samples many points across the solar disc for physically correct penumbra.

**Backward vs forward ray tracing; the Radiance methodology.** Radiance (Greg Ward, LBNL) is the reference physically-based engine and traces rays **backward** from sensors/eye into the scene (`rtrace`, `rpict`). Key methods:
- **Daylight Coefficient method (Tregenza/Mardaljevic):** precompute the contribution of each discretized sky patch to each sensor, then multiply by the time-varying sky from weather data — enables fast annual (8760-hour) simulation.
- **Sky discretization:** the **Tregenza sky** divides the hemisphere into **145 patches** in 8 altitude bands plus a zenith cap. The origin is P.R. Tregenza (1987), "Subdivision of the sky hemisphere for luminance measurements," *Lighting Research & Technology* 19(1):13–14 (DOI 10.1177/096032718701900103): "Tregenza's original design called for 151 such patches… in later experimental implementation this number was reduced to 145" (the 145-patch sky was adopted by the CIE in 1994). **Reinhart's** refinement subdivides each patch by a multiplication factor MF: **MF:1 = 145, MF:2 = 577, MF:4 = 2305 patches** (general form C = 144·x² + ⌈x²/2⌉).
- **`gendaymtx`/`gendaylit`:** generate Perez-model sky matrices; `-m 1` = Tregenza 145 (+1 ground), `-m 4` = 2305 patches. A sun is normally smeared across the **four nearest patches**.
- **Three-phase method:** Vmtx · Tmtx (BSDF) · Dmtx · sky — efficient for complex fenestration but smears direct sun across patches.
- **Five-phase method:** adds a separately, deterministically computed **direct-sun component** (using the sun as a `light` primitive sized at the true **0.533°** and an MF:6 ≈ 5165 sun-position basis) and subtracts the smeared direct part. Validated against `rtrace` ground truth: matches horizontal illuminance within 5% for 91% of the period vs. 40% for three-phase in scenes with direct sun penetration — this is why ASE (a direct-sun metric) requires five-phase or an "enhanced two-phase" sun calculation.

**Computing shadow AREA specifically.** Two routes: (1) **rasterization/sampling** — project a grid of sensor points on ground/façade, ray-cast each toward the sun, count shadowed cells × cell area (Ladybug "Direct Sun Hours"/"Sunlight Hours" and Forma sun-hours work this way; resolution-limited). (2) **Analytic/vector** — project occluder silhouettes onto the receiver plane and compute the union/intersection polygon area via **polygon clipping**: **Sutherland–Hodgman** (1974, clips against a convex window; O(Sₛ·Sᶜ); can produce coincident edges, fine for rendering but problematic for exact shadow Boolean), **Weiler–Atherton** (1977, handles concave polygons and returns separate components — correct for shadow/area Boolean), then the **shoelace formula** (A = ½|Σ(xᵢyᵢ₊₁ − xᵢ₊₁yᵢ)|) for area. CSG/Boolean union of overlapping shadow polygons handles multiple occluders. Analytic methods give exact areas (used for shadow-footprint and solar-envelope generation); mesh/raster methods scale better to complex geometry.

**Cumulative / time-integrated studies.** Sum shadow or sun-hours over a day/year; produce **solar insolation maps** (kWh/m²) by integrating the Perez sky over the **Tregenza/Reinhart patches** (Ladybug "Radiation Analysis," GenCumulativeSky cumulative-sky approach). GIS tools integrate over the sky vault (r.sun's two-part beam+diffuse model; ArcGIS Solar Analyst's hemispherical viewshed).

**Accuracy issues.** (1) **Penumbra/soft shadows** — the sun subtends ~**0.53°**, so true shadow edges have an umbra/penumbra; hard-shadow ray casting and shadow maps ignore this; distributed ray tracing across the solar disc captures it at high cost. (2) **Sky diffuse vs direct beam** — must be separated (direct gives crisp shadows and the dominant gain spike; diffuse fills shadows). (3) **Inter-reflection** — internal reflectance materially raises illuminance beyond the no-sky line and is the main reason Waldram's no-reflectance method underestimates; backward ray tracing with several ambient bounces is needed. (4) **Self-shadowing**, **shadow-map aliasing/bias**, and **stochastic noise** in path tracing (addressed by more passes or AI denoising).

### PART 3 — Practical software / tools today

**Radiance** (LBNL, Greg Ward) — the validated reference engine; CIE 171-tested; underlies most other daylight tools. **DAYSIM** — Radiance-based annual CBDM (DA, sDA, ASE, UDI), now largely superseded. **Accelerad** (MIT) — GPU (OptiX/RTX) Radiance, up to ~100× faster (16–44× at matched accuracy per Jones's validation); AcceleradRT gives real-time interactive daylight/glare and VR.

**Ladybug Tools (free, open-source) on Grasshopper/Rhino:**
- **Ladybug** — sun path, "Sunlight Hours Analysis," "Direct Sun Hours," radiation analysis, shadow studies, using sun vectors and ray casting against context meshes (coarse, fast).
- **Honeybee** — wraps **Radiance** and **EnergyPlus/OpenStudio**; "HB Annual Daylight" uses an enhanced 2-phase method (tracing rays to the actual solar position each hour) to compute DA, sDA, ASE, UDI; "HB Direct Sun Hours" uses Radiance for scalability.

**Rhino/Grasshopper plugins:** **ClimateStudio** (Solemma; progressive GPU-accelerated path tracing + Intel Open Image Denoise; LEED/CHPS compliance, glare, radiation, single-zone energy; the modern successor to **DIVA-for-Rhino**, also Solemma). **Ladybug/Honeybee** (above).

**Autodesk ecosystem:** **Revit** sun & shadow studies (geometric, visualization-grade) and **Insight**/Solar Analysis (Insight's lighting implements LM-83 sDA/ASE with Radiance-derived calibration); **FormIt** early massing; **Autodesk Forma** (formerly **Spacemaker**, acquired 2020) — cloud, AI-accelerated early-stage analyses (sun hours, daylight potential via VSC, wind, noise, microclimate using UTCI, solar energy) returning results in seconds; integrates with Revit/Rhino. (Forma's "Building Design" schematic-design module was announced for general availability in 2026 — a vendor projection.)

**Other desktop tools:** **SketchUp** shadows (geometric) + **Sefaira** (Radiance-based daylight); **IES VE** with **SunCast** (shadow/solar) and Radiance-based daylight (sDA/ASE, EN 17037); **DesignBuilder** (EnergyPlus + Radiance; LEED v4.1 Option 1, BREEAM Hea 01); **DIALux/Relux** (electric + daylight, CIE 171-validated); **VELUX Daylight Visualizer** (ray tracing; per Labayrade, Jensen & Jensen 2009, "Validation of Velux Daylight Visualizer 2 against CIE 171:2006 Test Cases," validated "with an approximate average deviation of 1.6%").

**Urban scale & GIS:** **Forma**, **Rhino+Grasshopper/Ladybug**, **UMI** (Urban Modeling Interface), **CityEngine**, and **GIS-based solar** — **ArcGIS Solar Analyst**, **GRASS r.sun**, **SAGA**, **CitySim**, **SimStadt**, and **UMEP/SOLWEIG** (Lindberg et al., 2008/2018; computes mean radiant temperature Tmrt, shadow patterns, UTCI/PET in 2.5D from DSMs + vegetation, for urban heat/thermal comfort). A 2025 *Transactions in GIS* comparison (León-Sánchez et al.) benchmarked ArcGIS Pro, GRASS, SAGA, CitySim, Ladybug, SimStadt and UMEP against weather-station ground truth.

**Real-time / game engines (Unreal, Unity).** Excellent for shadow *visualization* (Virtual Shadow Maps, Lumen, RTX ray-traced shadows) but estimate-based; not validated for compliance or energy. This is the core "visual shadow vs physically accurate solar analysis" gap: position is correct, but irradiance, inter-reflection, spectral and penumbra physics are not compliance-grade.

**UK rights-of-light / BRE specialist software.** The dominant vendor is **MBS Software**: **Waldram Tools** (v6, runs in AutoCAD and Revit; computes VSC, APSH, NSL, 2-h sunlight-to-amenity, ADF, rights-of-light contours, transient shadow images, DGP, solar radiation, and CBDM metrics sDA/ASE/UDI; uses Radiance; conforms to BR209, BREEAM, LEED, EN 17037), **Daylight for AutoCAD**, and **Daylight for SketchUp** (produces Waldram diagrams with pass/fail). Technical defaults: VSC/APSH points on the outside wall face, ADF at wall mid-point, 0.3 m grid spacing, BRE tree-transmittance materials from BR209 Appendix H Table H1. **Cadplan** provides survey geometry; **MADE Design** built a Grasshopper/Rhino Waldram tool (reducing analysis from up to a week to ~20 minutes). Consultancies (Anstey Horne, GIA, Blackacre, Blue Sky) run such tools in-house.

**Validation & certification.** **CIE 171:2006** "Test Cases to Assess the Accuracy of Lighting Computer Programs" is the benchmark; Radiance, VELUX Daylight Visualizer, DIALux, Relux, IDA ICE, AGi32, and (more recently) Ladybug and Solemma parametric workflows have been tested against it (the latter within ±10% even in unfavourable cases). Caveat: validation of the *engine* does not guarantee correctness of every parametric *workflow* built on top of it.

### PART 4 — Development directions and challenges

- **GPU acceleration & real-time CBDM:** ClimateStudio (progressive path tracing + OIDN denoising), Accelerad (OptiX/RTX), SOLWEIG-GPU (Rust+GPU tiled rasters). Collapsing annual simulations from hours to seconds.
- **Machine-learning surrogate models:** ANN/GAN/multimodal predictors trained on Radiance/Ladybug datasets to predict sDA/UDI/DA or energy in milliseconds for early-stage and generative optimization (recent multimodal models report R²≈0.90 on test sets). Differentiable Monte-Carlo ray tracing and inverse rendering enable gradient-based **inverse design** (recover geometry/material/shading from a target daylight/energy outcome).
- **BIM integration & automation:** IFC interoperability, automated sensor-grid generation, early-stage cloud analysis (Forma), and **generative design** optimizing massing for solar access/shading (e.g., BIM-integrated solar-envelope generators).
- **Urban-scale & digital twins:** LiDAR/point-cloud + GIS DSMs feeding r.sun/SOLWEIG/UMEP; city-scale insolation and Tmrt maps; coupling mesoscale (WRF) with microscale (SOLWEIG) for heat-resilient planning.
- **Open challenges:** accuracy-vs-speed tradeoffs; soft-shadow/penumbra fidelity; inter-reflection and specular/redirecting systems; vegetation and seasonal/dynamic shading; weather/sky-model uncertainty (the largest error source); the **standard-to-simulation gap** (simplified VSC/Waldram methods vs full CBDM); BIM data quality and level-of-detail; computational cost at urban scale; and the lack of unified cross-tool benchmarks beyond CIE 171. The clear trajectory is from static (daylight factor, VSC) toward **climate-based metrics** (sDA, ASE, UDI, EN 17037 absolute illuminance).

### Mapping: standard requirement → metric → algorithm → tool

**Energy / daylighting chain:**
- LEED v4.1 Daylight Opt 1 → **sDA 300/50%** → annual backward ray tracing, daylight-coefficient/enhanced-2-phase, Perez sky over Tregenza/Reinhart patches → Radiance, ClimateStudio, Honeybee, IES VE, DesignBuilder, DAYSIM.
- LEED v4.1 → **ASE 1000,250** → five-phase or enhanced-2-phase with deterministic 0.533° direct sun → Radiance 5-phase, ClimateStudio, Honeybee.
- EN 17037 daylight provision → **target DF / absolute illuminance** → CBDM or DF ray tracing → Radiance-based tools, VELUX Daylight Visualizer.
- EN 17037 sunlight exposure → **sunlight-exposure hours on reference date** → sun-ray casting against geometry → Ladybug Direct Sun Hours, IES SunCast, Waldram Tools.
- ASHRAE 90.1 / Title 24 → **SHGC × shading factor, solar gain** → solar-geometry shading-factor calc + hourly heat-balance → EnergyPlus, IES VE, DesignBuilder, PHPP.
- Passive House → **monthly shading reduction factor** → geometric horizon/overhang shading → PHPP, designPH.

**Right-to-light / overshadowing chain:**
- BRE 209 neighbour daylight → **VSC (≥27%, 0.8× test)** → sky-vault geometry / ray casting to window centre → MBS Waldram Tools / Daylight for AutoCAD/SketchUp, IES VE, Forma (VSC).
- BRE 209 interior distribution → **No Sky Line (≥50%)** → working-plane (850 mm) sky-visibility ray casting → Waldram Tools, specialist packages.
- BRE 209 sunlight → **APSH (25% annual / 5% winter)** → annual sun-ray casting on south-facing windows → Waldram Tools, IES SunCast.
- BRE 209 amenity → **sun-on-ground (≥50% area, ≥2 h on 21 March)** → ground-grid sun-ray casting + transient shadow plots → Ladybug, Forma, SunCast, Waldram Tools.
- UK Rights of Light (legal) → **0.2% sky factor / 50-50 rule** → uniform-sky sky-factor on Waldram diagram → MBS Waldram Tools/Daylight suite.
- Solar access zoning → **solar envelope volume** → cutting-plane geometry from sun vectors at cut-off times → Revit/DIVA solar-envelope generators, Grasshopper.

## Recommendations

1. **Match the engine to the deliverable.**
   - *Planning/right-of-light in the UK:* use a BR209-aware specialist package (MBS Waldram Tools / Daylight for AutoCAD or SketchUp) for VSC/APSH/NSL/amenity and Waldram rights-of-light contours. Do not substitute a Revit sun study.
   - *LEED/BREEAM/EN 17037 credits:* use a CIE 171-validated Radiance engine — ClimateStudio (fastest), Honeybee, IES VE, or DesignBuilder. Insist on **five-phase or enhanced-2-phase** when ASE is in scope.
   - *Early massing/urban:* use Forma or Ladybug for sun-hours and overshadowing screening, then confirm with Radiance later.

2. **Standardize inputs.** Always drive shadow/solar work with **NREL SPA** sun positions, the **Perez all-weather** sky, and a project-appropriate **TMY/EPW** file. Document the sky-patch resolution (Tregenza MF:1 vs Reinhart MF:4) and ambient-bounce settings — these silently change credit pass/fail (e.g., DIVA's 2-bounce default can flip a LEED result vs. the recommended 7 bounces).

3. **Treat game-engine/BIM shadows as visualization only.** Use them for design communication and qualitative transient overshadowing, never for compliance numbers.

4. **For shadow-area/footprint deliverables,** prefer analytic polygon clipping (Weiler–Atherton + shoelace) when exact areas and Booleans matter; use grid/ray-cast sampling for complex geometry, and state the grid resolution as an uncertainty.

5. **Adopt climate-based metrics now.** Lead with sDA/ASE/UDI and EN 17037 absolute-illuminance methods over daylight factor; they correlate better with real performance and are where standards are heading.

6. **Pilot AI surrogates and GPU tools for iteration, validate with Radiance for sign-off.** Use ML surrogates / Forma / ClimateStudio for rapid design-space exploration, but keep a validated Radiance run as the final compliance record.

**Thresholds that would change these recommendations:** if a jurisdiction adopts EN 17037 absolute-illuminance as mandatory (shift fully to CBDM); if ASE is dropped or de-emphasized (as v4.1 began), reduce reliance on five-phase; if AI surrogates achieve documented CIE 171-level validation, promote them from screening to sign-off; at very large urban scale (>10⁶ sensors), move from Radiance to GPU (Accelerad) or 2.5D raster (SOLWEIG/r.sun).

## Caveats

- **Promotional vs primary sources:** specific UK rights-of-light product feature claims come largely from vendor (MBS Software) marketing pages; the underlying methods (Waldram, 0.2% sky factor, 850 mm plane, 50/50, 2-h/21-March amenity) are corroborated by independent surveyor sources and BRE. Product names beyond the MBS suite (and the MADE Grasshopper tool / Cadplan survey data) could not be independently confirmed.
- **The 0.2% "grumble line" is contested:** peer-reviewed work (Defoe et al.) argues it lacks empirical basis and that ~0.56% (28 lux) better reflects adequacy; it nonetheless remains the legal industry standard, reaffirmed in *Cooper & Powell v Ludgate House* (2025).
- **Validation gaps:** CIE 171 validates engines, not every parametric workflow; ASE thresholds (1000 lux / 250 h) are calibrated specifically to Radiance's 5-phase output and do not transfer cleanly to other engines without conversion factors.
- **Sky-model uncertainty** (especially cloudy/partly-cloudy conditions) and **weather-file representativeness** are typically larger error sources than sun-position or ray-tracing precision.
- **Forecasts/forward-looking items** (Forma "Building Design" general availability 2026; emerging ML-for-daylight maturity) are vendor/industry projections, not established capabilities, and should be treated as such.
- Some retrieved figures (e.g., exact APSH "≈1,486 hours" equivalent of 25%) are secondary-source interpretations of BRE tables; verify against the current BR209 (2022) and EN 17037 National Annex before formal submission.

*Lưu ý: Báo cáo dựa trên tiêu chuẩn quốc tế (EN 17037, BRE 209, IES LM-83, ASHRAE 90.1, Title 24, LEED, BREEAM, Passive House). Đối với hồ sơ pháp lý/quy hoạch tại Việt Nam, cần đối chiếu thêm với QCVN và tiêu chuẩn quốc gia liên quan trước khi nộp.*