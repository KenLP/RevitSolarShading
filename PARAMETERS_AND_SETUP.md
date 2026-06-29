# Parameters & Setup Guide

The add-in is self-contained: it creates and manages its own shared parameters, computes
window orientation itself, and works on **any** Revit model. The only thing you must set is
the **project Site Location** (Manage → Location), because the sun depends on it.

## 1. The shared parameters the add-in uses

All parameters live in a shared-parameter group named **`SolarShading`** and have **fixed
GUIDs**, so they are the *same* definition in every project and on every machine (schedules,
exports and round-trips line up).

| Parameter | Type | Bound to | Meaning |
|---|---|---|---|
| `SS_SHADING_DEVICE` | Yes/No | walls, generic models, framing, columns, roofs, floors, mass, mullions, casework | Marks an element as a shading device |
| `SS_EXTERNAL_SC2` | Number | Windows | Effective external shading coefficient (SC2) for ETTV |
| `SS_SHADED_MARCH` / `SS_SHADED_JUNE` / `SS_SHADED_DEC` | Text | Windows | Hourly shaded area (m²) per reference date, `;`-separated (`NA` when the window doesn't face the sun) |

> These are **independent** of any other parameters already in your families (e.g. an
> existing `ET_*` set). The add-in does **not** read or modify them and will not conflict.

## 2. Setting up the parameters

**Automatic** — the parameters are created and bound the first time you run *Shading on
Windows* or *Shading Devices*. Nothing to do.

**Explicit (recommended for a clean project)** — click **Solar Shading → Setup Parameters**
once. It creates all the shared parameters with their fixed GUIDs and binds them to the right
categories, then tells you where the shared-parameter file is.

### Where the shared-parameter file lives
By default the add-in keeps its own file at:

```
%LocalAppData%\SolarShading\SolarShading_SharedParameters.txt
```

This path is stable across Revit sessions (Revit redirects `%TEMP%` per session, so it is not
used). **If you already have your own shared-parameter file set** in Revit
(*Manage → Shared Parameters*), the add-in keeps it and simply adds its `SolarShading` group to
it — so you can manage everything in one file.

### Declaring the parameters yourself (optional)
If your office standardises shared parameters, you can pre-create them in your own file with the
exact **names and GUIDs** in §1 and bind them to the categories above. The add-in will then find
and reuse them instead of creating its own. Keep the GUIDs identical or the values won't map.

## 3. Choosing shading devices

External shading devices (separate elements — overhangs, fins, ledges, brise-soleil) are flagged
with `SS_SHADING_DEVICE`. Shading fins modelled **inside** a window family are detected
automatically and don't need flagging.

Workflow — **Solar Shading → Shading Devices** opens a small menu:

1. **Tag selected as shading devices** — select the elements first, then tag them (saved in the model).
2. **Untag selected** — remove the selection from the set.
3. **Select all tagged (review)** — selects every tagged device so you can review, zoom, or filter.

Because the flag is a normal Yes/No parameter, you can also **schedule** or **filter** by it, and
re-run *Tag* any time to add more.

## 4. Running an analysis

1. **Setup Parameters** (once, optional).
2. **Shading Devices** → tag the external shades (skip if all shading is inside the window families).
3. **Shading on Windows** → pick the compliance **Code** (BCA / MS1525 / QCVN 09), dates, hours,
   glazing fallback, then **Run**. Results go to the `SS_*` parameters, a CSV, an ETTV/OTTV +
   RTTV **compliance report** (opens in your browser), and an on-screen pass/fail table.

See [USER_GUIDE.md](USER_GUIDE.md) for the full walkthrough.
