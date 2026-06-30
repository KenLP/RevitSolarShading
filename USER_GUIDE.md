# Solar Shading — User Guide (Revit 2026)

A Revit add-in that computes the **shaded area of windows from external shading devices**,
the resulting **external shading coefficient (SC2)**, and the **Singapore BCA ETTV** with
pass/fail. It also casts a building's shadow onto the ground.

> The sun position is computed analytically from the project's site location — no Revit
> sun-study transaction is needed, so the analysis is fast and does not depend on the
> active view's sun settings.

---

## 1. Install

**Automatic (recommended)** — from the project folder, with Revit closed:

```powershell
./deploy/Deploy.ps1 -RevitVersion 2026          # or 2025 / 2027
```

This copies the add-in to `%AppData%\Autodesk\Revit\Addins\2026\` and writes the manifest.

**First launch:** Revit shows *"The publisher of this add-in could not be verified"* because
the DLL is unsigned. Click **Always Load**. A **Solar Shading** ribbon tab will appear.

---

## 2. Before you run — model checklist

1. **Site location is set** — *Manage → Location* → set Latitude/Longitude (and time zone).
   The sun position depends entirely on this. The default Boston location gives Boston sun.
2. **Project North** is set correctly if your model isn't aligned to true north.
3. **Windows are hosted in walls** (standard window families). Curtain walls are partially supported.
4. **Shading devices have 3D solid geometry** (overhangs, fins, ledges, projecting slabs, mullions…).
5. *(Optional)* Windows carry **analytic thermal properties** (U-value, SHGC) on the family/type
   so ETTV uses the real glass. Missing values fall back to the glazing you pick in the dialog.

---

## 3. Workflow

### Step 1 — Tag the shading devices
1. Select the elements that shade the windows (overhangs, fins, ledges, projecting floors…).
2. **Solar Shading → Get Shading Devices**.
   This sets a `SS_SHADING_DEVICE` = Yes flag on the selection. Re-run any time to add more.

### Step 2 — Run the shading + ETTV analysis
1. **Solar Shading → Shading on Windows**.
2. In the dialog choose:
   - **Reference dates** — 21 Mar / 21 Jun / 21 Dec (equinox / solstices).
   - **Time of day** — hour range (default 7–18) and the year.
   - **Glazing (fallback)** — used only for windows with no thermal data in the model.
   - **Wall U** and **ETTV threshold** (default 50 W/m²).
   - **Output** — show shadow overlay, write to shared parameters, export CSV.
3. Click **Run**. The **ETTV Results** window shows a per-orientation table
   (Window area, WWR %, SC2, ETTV) and the envelope **ETTV — PASS / FAIL**.

### Step 3 — Building shadow on ground (optional)
1. Select one or more **Mass** elements.
2. **Solar Shading → Building Shadow on Ground**, then pick a **date and hour**.
   Draws the cast shadow as a coloured DirectShape on the ground and reports its area.
   Re-running replaces the previous building-shadow overlay.

---

## 4. Where the results go

| Output | Location |
|---|---|
| External shading coefficient | Window shared parameter **`SS_EXTERNAL_SC2`** |
| Hourly shaded area (m²) per date | **`SS_SHADED_MARCH` / `SS_SHADED_JUNE` / `SS_SHADED_DEC`** (semicolon-separated, one value per hour, `NA` when the window doesn't face the sun) |
| Full hourly table | **CSV on your Desktop** — `SolarShading_YYYYMMDD_HHmmss.csv` |
| Printable compliance report | **HTML on your Desktop** — `SolarShading_Report_YYYYMMDD_HHmmss.html` (opens automatically when enabled) |
| Envelope ETTV + pass/fail | **ETTV Results** window |
| Shadow preview | Red **overlay** on each window (re-drawn each run, never stacked) |

You can schedule the `SS_*` parameters in Revit (they are bound to the Windows category).

---

## 5. Interpreting the numbers

- **SC2** (0–1): the share of solar gain that still reaches the glass after the external
  shading — solar-radiation-weighted across the analysed hours. Lower = more effective shading.
- **ETTV** (W/m²): `12·(1−WWR)·Uw + 3.4·WWR·Uf + 211·WWR·CF·SC1·SC2`, area-weighted across
  façades. **≤ 50 W/m² passes** (BCA Green Mark default).
- **Shaded area series**: hour-by-hour shaded area on the glass — useful for transient studies.

---

## 6. Troubleshooting

| Message | Cause / fix |
|---|---|
| "No windows found" | Model has no window family instances. |
| "No elements are flagged as shading devices" | Run **Get Shading Devices** on the shades first. |
| "No window received shading…" | Shades are >4 m from any window, or below/behind them. |
| Sun below horizon (ground shadow) | The site/time gives night; check *Manage → Location*. |
| Results look wrong by a fixed rotation | Check **Project North** and the site location angle. |

---

## 7. Important limitations (read before using for submission)

- This is a **geometric** shading tool (correct sun position + exact shadow areas). It is **not**
  a validated daylight/energy engine (Radiance-class) — use it for ETTV/shading, not for sDA/ASE.
- **Regulatory constants are indicative** and must be verified against the current code edition:
  the BCA solar correction factors, the 50 W/m² threshold, and the built-in glazing library values.
- **SC2 weighting** uses an ASHRAE clear-sky proxy, not a project EPW weather file.
- The window outline uses the rough opening when available, else the largest glazing face.
- Always confirm a few results by hand or against a reference tool before relying on them.
