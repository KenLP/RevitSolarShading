# Shadow Computation — SolarShading

An open-source Revit add-in for **envelope solar shading and ETTV/OTTV compliance** —
exact shadow areas, window shading coefficients (SC2) and the envelope thermal-transfer
value for tropical green-building codes — on Revit 2025–2027 (.NET 8 / .NET 10).

Docs: [USER_GUIDE.md](USER_GUIDE.md) · [PARAMETERS_AND_SETUP.md](PARAMETERS_AND_SETUP.md)

## Install

Grab the latest `SolarShading-<version>.zip` from
[Releases](https://github.com/KenLP/RevitSolarShading/releases), then:

1. **Close Revit.**
2. Unzip, right-click **`Install.ps1` → Run with PowerShell**
   (or in a PowerShell window: `.\Install.ps1`).
3. Start Revit → the **Solar Shading** ribbon tab appears.
4. In each project, click **Setup Parameters** once.

The installer detects every Revit 2025–2027 on the machine and installs the matching
build (.NET 8 for 2025/2026, .NET 10 for 2027). Useful switches:

```powershell
.\Install.ps1 -RevitVersion 2026   # one version only
.\Install.ps1 -AllUsers            # machine-wide (run PowerShell as Administrator)
.\Uninstall.ps1                    # remove
```

If Windows blocks the script, run `Set-ExecutionPolicy -Scope Process -Bypass` once in
that PowerShell window.

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
  Commands/                SetupParameters, ShadingDevices (tag/untag/review),
                           ShadingOnWindows, BuildingShadowOnGround
  App.cs, SolarShading.addin
tests/SolarShading.Core.Tests   xUnit — solar position vs NREL SPA reference + analytic shadow areas
installer/                 Install.ps1 / Uninstall.ps1 (ship with the release),
                           Build-Package.ps1 (makes the ZIP), SolarShading.iss (optional .exe)
deploy/Deploy.ps1          one-step build + install for local development
```

### Build

```
dotnet test                                                  # Core — 41 unit tests
dotnet build src/SolarShading.Revit -p:RevitVersion=2026     # add-in for Revit 2025/2026 (.NET 8)
dotnet build src/SolarShading.Revit -p:RevitVersion=2027     # add-in for Revit 2027 (.NET 10)
```

While developing, `deploy/Deploy.ps1 -RevitVersion 2026` builds and installs in one step
(Revit must be closed).

### Package a release

```
installer/Build-Package.ps1 -Version 1.0.0
```
Builds every payload whose Revit API is installed and writes `dist/SolarShading-<version>.zip`
— a self-contained installer needing no build tools on the end user's machine. If
[Inno Setup 6](https://jrsoftware.org/isdl.php) is installed it also compiles
`dist/SolarShading-<version>-Setup.exe` from `installer/SolarShading.iss`.

## Algorithm

1. **Sun position by math** (`SolarPosition`) — NOAA series, validated within 0.3° of
   the NREL SPA reference case. Computed analytically — no `SunAndShadowSettings` and no
   per-hour Revit transaction (a major performance win).
2. **Shadow by silhouette projection + 2D clipping** (`ShadowProjector` + `PolygonClipper`)
   — each occluder face is clipped to the **sun side of the receiver plane** (geometry behind
   the glass can't shadow it), projected along the sun ray onto the receiver plane, unioned,
   and intersected with the window outline using **Clipper2** (robust Weiler–Atherton).
   Exact areas via shoelace. Replaces fragile 3D boolean shadow solids.
3. **ETTV / SC** (`BcaEttv`, `ShadingCoefficient`) — solar-weighted effective external
   shading coefficient (SC2) feeds the Singapore BCA ETTV formula.

## Build & test

```
dotnet test
```

## Status

- ✅ Core engine (solar position, silhouette projection, 2D clipping, holes, ETTV, clear-sky) — **41 tests pass**.
- ✅ Solar engine validated from first principles across hemispheres/seasons + the NREL SPA reference point.
- ✅ Revit add-in builds against RevitAPI 2026 (.NET 8) and 2027 (.NET 10).
- ✅ Three commands: tag shading devices, shading-on-windows (SC2 + ETTV pass/fail + CSV; red overlay re-drawn each run), building-shadow-on-ground (Mass selection, date/time picker).
- ✅ **WPF UI**: configuration dialog (dates, hours, glazing, threshold, outputs) + per-orientation ETTV results table.
- ✅ **Installer**: `installer/Build-Package.ps1` produces a self-contained ZIP (per-user or all-users install, auto-detects Revit 2025–2027, matching .NET 8 / .NET 10 payload, clean uninstall); optional Inno Setup `.exe`. `deploy/Deploy.ps1` covers the developer build-and-install loop.
- ✅ SC2 weighted by **ASHRAE clear-sky** incident irradiance; window outline from the **rough opening** (RevitAPIIFC) with largest-face fallback.
- ✅ **Per-element glazing**: U-value and SHGC→SC1 read from each window's family/type, area-weighted per orientation (dialog glazing is only a fallback).
- ✅ User guide: [USER_GUIDE.md](USER_GUIDE.md) / [USER_GUIDE.pdf](USER_GUIDE.pdf).
- ✅ **Whole-model performance (T1–T6)**: occluder geometry cache (T1); 3-phase **parallel** analysis — Revit-thread extract → parallel pure-maths → single write transaction (T2); back-face cull (T3); polygon simplification (T4); bounding-box / wrong-side occluder culling (T5); coarse curved-face tessellation (T6). 41 tests pass; the fast path is proven to match the plain path.
- ✅ **Validated in Revit**: run on a real multi-storey model (Revit 2026) — shading devices tagged, SC2 / ETTV written to shared parameters, per-window red overlays and the mass building-shadow verified live. Occluder geometry behind the glass is clipped out so deep fins no longer paint spurious shadows.
- ✅ **Extensible tessellation** (`ITessellator`): curved / organic shading faces tessellate through the Revit API out of the box, and the same seam lets anyone plug in a custom tessellator as a drop-in assembly.

## Next

- Optional NREL SPA / Grena behind `ISolarPositionAlgorithm` (NOAA is already < 0.3° of SPA — ample for shadow geometry).
- EPW / measured irradiance instead of the clear-sky proxy.
- Verify regulatory constants (BCA correction factors, ETTV threshold) against the current edition.
