# Shadow Computation — SolarShading

An open-source Revit add-in for **envelope solar shading and ETTV/OTTV compliance** —
exact shadow areas, window shading coefficients (SC2) and the envelope thermal-transfer
value for tropical green-building codes — on Revit 2025–2027 (.NET 8 / .NET 10).

See [EXPANSION_PLAN.md](EXPANSION_PLAN.md) for the standards-driven roadmap.

## Structure

```
src/SolarShading.Core      .NET 8 class library — NO Revit dependency, fully unit-tested
  Geometry/                Vec3, Plane3, Polygon3, OccluderFace, ShadowProjector,
                           PolygonClipper (Clipper2), ShadingCalculator
  Solar/                   SolarPosition (NOAA), SunVector, ISolarPositionAlgorithm
  Ettv/                    BcaEttv + EttvAssessment (Singapore ETTV, pass/fail),
                           ShadingCoefficient (SC2), Orientation, Glazing, CorrectionFactors
src/SolarShading.Revit     Revit add-in (net8.0 for Revit 2025/2026, net10.0 for 2027)
  Geometry/                Units, RevitGeometryExtractor, WindowReceiver, ShadowVisualizer
  Solar/                   SiteSun (analytic sun in model coords — no transactions)
  Engine/                  RevitShadeEngine (read-only orchestration → SC2 + areas)
  Parameters/              ForgeTypeId shared params (results + shading-device flag)
  Commands/                GetShadingDevices, ShadingOnWindows, BuildingShadowOnGround
  App.cs, SolarShading.addin
tests/SolarShading.Core.Tests   xUnit — solar position vs NREL SPA reference + analytic shadow areas
```

### Build

```
dotnet test                                                  # Core + 20 unit tests
dotnet build src/SolarShading.Revit -p:RevitVersion=2026     # add-in for Revit 2025/2026 (.NET 8)
dotnet build src/SolarShading.Revit -p:RevitVersion=2027     # add-in for Revit 2027 (.NET 10)
```
Deploy: copy `SolarShading.Revit.dll`, `SolarShading.Core.dll`, `Clipper2Lib.dll` and
`SolarShading.addin` into `%AppData%\Autodesk\Revit\Addins\2026\` (fix the Assembly path
in the .addin or place the DLLs alongside it).

## Algorithm

1. **Sun position by math** (`SolarPosition`) — NOAA series, validated within 0.3° of
   the NREL SPA reference case. Computed analytically — no `SunAndShadowSettings` and no
   per-hour Revit transaction (a major performance win).
2. **Shadow by silhouette projection + 2D clipping** (`ShadowProjector` + `PolygonClipper`)
   — each occluder face is projected along the sun ray onto the receiver plane, unioned,
   and intersected with the window outline using **Clipper2** (robust Weiler–Atherton).
   Exact areas via shoelace. Replaces fragile 3D boolean solids + the tessellation retry DLL.
3. **ETTV / SC** (`BcaEttv`, `ShadingCoefficient`) — solar-weighted effective external
   shading coefficient (SC2) feeds the Singapore BCA ETTV formula.

## Build & test

```
dotnet test
```

## Status

- ✅ Core engine (solar position, silhouette projection, 2D clipping, holes, ETTV, clear-sky) — **28 tests pass**.
- ✅ Solar engine validated from first principles across hemispheres/seasons + the NREL SPA reference point.
- ✅ Revit add-in builds against RevitAPI 2026 (.NET 8) and 2027 (.NET 10).
- ✅ Three commands: tag shading devices, shading-on-windows (SC2 + ETTV pass/fail + CSV), building-shadow-on-ground.
- ✅ **WPF UI**: configuration dialog (dates, hours, glazing, threshold, outputs) + per-orientation ETTV results table.
- ✅ **Deploy script** (`deploy/Deploy.ps1`) installs into `%AppData%\...\Addins\<ver>\`.
- ✅ SC2 weighted by **ASHRAE clear-sky** incident irradiance; window outline from the **rough opening** (RevitAPIIFC) with largest-face fallback.
- ✅ **Per-element glazing**: U-value and SHGC→SC1 read from each window's family/type, area-weighted per orientation (dialog glazing is only a fallback).
- ✅ User guide: [USER_GUIDE.md](USER_GUIDE.md) / [USER_GUIDE.pdf](USER_GUIDE.pdf).
- ✅ **Whole-model performance (T1–T6)**: occluder geometry cache (T1); 3-phase **parallel** analysis — Revit-thread extract → parallel pure-maths → single write transaction (T2); back-face cull (T3); polygon simplification (T4); bounding-box / wrong-side occluder culling (T5); coarse curved-face tessellation (T6). 31 tests pass; the fast path is proven to match the plain path.

## Next

- **In-Revit validation**: load the add-in and run on a real model (cannot be done headless here).
- Optional NREL SPA / Grena behind `ISolarPositionAlgorithm` (NOAA is already < 0.3° of SPA — ample for shadow geometry).
- EPW/measured irradiance instead of the clear-sky proxy; per-element glazing/U-value instead of one default.
- Verify regulatory constants (BCA correction factors, ETTV threshold) against the current edition.
