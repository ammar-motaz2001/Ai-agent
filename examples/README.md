# Examples

## Sample inputs

Two sample files ship in the repository root:

| File | What it is |
|------|-----------|
| `ROADS.dwg` | A drawing containing a road centreline polyline (and, ideally, existing-ground contours). |
| `POINTS.xlsx` | Survey points to add to the existing-ground surface (optional). |

## Run the sample end-to-end

1. Build and NETLOAD the plugin (see [../docs/INSTALLATION.md](../docs/INSTALLATION.md)).
2. In Civil 3D, type `AICIVIL`.
3. Set:
   - **Input DWG** → `...\civil\ROADS.dwg`
   - **Input Excel** → `...\civil\POINTS.xlsx`
   - **Output folder** → any writable folder, e.g. `C:\Civil3D-AI-Output`
   - **Extract length (m)** → `3000` (or `0` to use the value in `appsettings.json`)
4. Click **▶ Start**.

## What you get in the output folder

```
C:\Civil3D-AI-Output\
├─ ROADS_AI.dwg                 The finished drawing (alignment, surfaces, corridor, views, sheets)
├─ ROADS_AI_sheets.pdf          Published sheets (single multi-page PDF by default)
├─ AI-SheetSet.dsd              Sheet-set / batch-publish descriptor
└─ logs\
   └─ run_YYYYMMDD_HHMMSS.log   Full per-run log with timing and any warnings/errors
```

## Tuning the run

Open **Settings…** in the window (or edit `config/appsettings.json`) to change, for example:

- `Extraction.SegmentLengthMeters` — how much road to extract (default 3000 m).
- `Extraction.RoadPolylineLayers` / `ContourLayers` — the layer names used to auto-detect the road
  and contours in `ROADS.dwg`. **Set these to match your drawing's actual layer names.**
- `Excel.*` — the column mapping for `POINTS.xlsx` (header names or column letters).
- `Assembly.*`, `Corridor.*`, `SampleLines.*` — the design geometry.
- `Sheets.*` / `Pdf.*` — sheet scales, page setup, PDF DPI.

Click **Reload settings** after editing the JSON.

## Expected caveats on first run

- If the log warns that styles were not found, your template lacks the configured style names — the
  tool falls back to defaults. Add the styles or update the names in `appsettings.json`.
- If the corridor/volumes come out empty, add a pre-built assembly to your template named to match
  `Assembly.Name` — see
  [../docs/DEVELOPER_GUIDE.md#preparing-the-template](../docs/DEVELOPER_GUIDE.md#preparing-the-template).
