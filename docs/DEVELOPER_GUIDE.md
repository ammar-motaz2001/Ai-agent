# Developer Guide

Everything a developer needs to build on, extend, and troubleshoot the Civil3D AI Agent.

- [Architecture in one minute](#architecture-in-one-minute)
- [How a run flows](#how-a-run-flows)
- [Dependency injection](#dependency-injection)
- [Threading model](#threading-model)
- [Preparing the template](#preparing-the-template)  ← **read this to get real corridor output**
- [Version-sensitive API calls](#version-sensitive-api-calls)  ← **read this if a build fails**
- [Adding or changing a step](#adding-or-changing-a-step)
- [Configuration reference](#configuration-reference)
- [Testing](#testing)

---

## Architecture in one minute

Clean architecture, dependencies point **inward** (outer layers depend on inner abstractions, never the
reverse). See [ARCHITECTURE.md](ARCHITECTURE.md) for the full diagram.

```
Commands ─► UI ─► Services ─► Application ─► Civil3D ─► Core ◄─ Infrastructure
                                   │                     ▲
                                   └──────► Models ◄──────┘  (+ Logging, Utilities everywhere)
```

- **Models** — pure data. No dependencies.
- **Core** — interfaces (ports) + the workflow-engine contracts. No Autodesk types.
- **Infrastructure** — non-Civil-3D implementations (JSON config, ClosedXML Excel, files, validation).
- **Civil3D** — the only place (with Commands/UI) that touches the Autodesk API. Implements the 23
  operations and the `IPdfPublisher`/`IExceptionExplainer` ports.
- **Application** — the workflow engine + 23 thin step classes that adapt services to `IWorkflowStep`.
- **Services** — the DI composition root + the `IAutomationService` facade.
- **UI** — WPF MVVM window, hosted inside Civil 3D.
- **Commands** — the NETLOAD assembly exposing the `AICIVIL` command.

**Why in-process?** The Civil 3D .NET API only works inside the Civil 3D process. A standalone .exe
cannot reliably drive it. So the deliverable is a plugin DLL that shows a modeless WPF window.

---

## How a run flows

1. `AICIVIL` command → `AgentLauncher.Show(automation)` opens the WPF window.
2. User fills inputs, clicks **Start** → `MainViewModel.StartAsync`.
3. The VM wraps the run in `DocumentManager.ExecuteInCommandContextAsync` (guarantees a valid document
   context) and calls `IAutomationService.RunAsync`.
4. `WorkflowEngine` validates inputs, builds a `WorkflowContext`, attaches a per-run file logger, and
   executes each `IWorkflowStep` **in `WorkflowStepType` order**.
5. Each step calls a **Civil3D service** and stashes result handles in the context for later steps
   (see `WorkflowContextKeys`). Handles (strings) are the neutral currency between steps.
6. Progress + log stream to the UI; a `WorkflowResult` with per-step outcomes is returned.

Data flow between steps is documented in
[`Core/Workflow/WorkflowContextKeys.cs`](../src/Civil3DAIAgent.Core/Workflow/WorkflowContextKeys.cs).

---

## Dependency injection

The single composition root is
[`Services/Composition/CompositionRoot.cs`](../src/Civil3DAIAgent.Services/Composition/CompositionRoot.cs).
It uses `Microsoft.Extensions.DependencyInjection` and registers, as singletons:

- Logging: `UiLogSink` (permanent) + `RoutingLogger` (adds a per-run file logger) as `ILogger`.
- Ports: `IConfigurationProvider`, `IFileService`, `IExcelPointReader`, `IInputValidator`,
  `IExceptionExplainer`, `ICivilDocProvider`, `IPdfPublisher`.
- The 13 Civil 3D operation services.
- All 23 `IWorkflowStep` implementations (injected into the engine as `IEnumerable<IWorkflowStep>`).
- `IWorkflowEngine` and the `IAutomationService` facade.

To swap an implementation (e.g. a different Excel reader), change one line here.

---

## Threading model

- Civil 3D API calls must run in the application/document context. The UI enters that context via
  `ExecuteInCommandContextAsync`.
- The engine executes steps **synchronously** on that context thread but `await Task.Yield()` between
  steps so the WPF window repaints and shows progress/log updates.
- Each step acquires a document lock through `TransactionHelper.InDocumentLock` and scopes every
  database change in a committed transaction via `TransactionHelper.InTransaction`.
- Cancellation is checked at step boundaries (the natural granularity).

---

## Preparing the template

The design profile, corridor, sample lines, materials, sheets, and PDFs all read named styles from the
drawing template configured at `Paths.DrawingTemplate`. Missing styles are **not fatal** — the
`StyleResolver` falls back to the first available style and logs a warning — but for clean output your
template should contain the styles referenced in `appsettings.json`.

### The one thing that matters most: the assembly

**The Civil 3D managed .NET API cannot instantiate stock catalog subassemblies** (lanes, shoulders,
daylight). This is a well-known API limitation, not a bug in this tool. Your options:

1. **Recommended — build the assembly once in the template:**
   - Open your template DWG (`*.dwt`).
   - Build a typical section (e.g. `LaneOutsideSuper` + `ShoulderExtendSubbase` + `DaylightStandard`
     per side) using the Assembly tools.
   - Name the assembly exactly as `Assembly.Name` in `appsettings.json` (default `AI-Assembly-01`).
   - Ensure its link codes include **`Top`** and **`Datum`** (the stock subassemblies already emit
     these), so steps 13–14 can extract the Top/Datum surfaces.
   - Save the template.
   The tool will find and reuse it — the corridor then produces real geometry, surfaces, and volumes.

2. **Advanced — implement COM-based subassembly insertion** by providing a new `IAssemblyService`
   implementation (register it in the composition root). The COM API (`Autodesk.AECC.Interop.*`) can
   place catalog subassemblies.

If no assembly is found, the tool creates an **empty** one and logs a clear warning; the run continues
but the corridor/surfaces/volumes will be empty.

### Other styles to include (names configurable)

| Setting | Default name | Used by |
|---------|--------------|---------|
| `Alignment.StyleName` | `Proposed` | Step 7 |
| `Alignment.LabelSetName` | `All Labels` | Step 7 |
| `Surface.StyleName` | `Contours 1m and 5m (Background)` | Step 8 |
| `Profile.ExistingGroundStyle` / `DesignStyle` | `Existing Ground Profile` / `Design Profile` | Steps 9–10 |
| `Materials.QuantityTakeoffCriteria` | `Cut and Fill` | Steps 16–17 |
| `Sheets.PageSetupName` | `PDF-A1` | Steps 20, 22 |

---

## Version-sensitive API calls

This project targets **Civil 3D 2022**. The Civil 3D managed API changes subtly between releases,
mostly around corridors, sections, materials, and publishing. Every call that is most likely to need a
tweak on a different release is tagged in code with a `// [VERSION]` comment. If a build error or
runtime `MissingMethodException` appears, check the object browser on **your** `AeccDbMgd.dll` /
`AcPublishMgd.dll` and adjust **only** these:

| File | Call | If it differs… |
|------|------|----------------|
| `CorridorService` | `civilDoc.CorridorCollection.Add(name)` | Some releases create corridors differently; find the corridor-collection accessor on `CivilDocument` |
| `CorridorService` | `baseline.BaselineRegions.Add(name, assemblyId)` | Check the `Baselines.Add` / region overloads |
| `CorridorService` | `region.setFrequency(...)` | Already guarded; safe to remove if absent (defaults apply) |
| `CorridorService` | `corridor.CorridorSurfaces.Add(name)` + `surface.AddLinkData(code)` | Method may be `AddLinkCode`/`AddSurfaceEntity`; adjust the two calls |
| `CorridorService` | `region.AssemblyReferences.LinkCodes` | Guarded (returns "assume present"); only affects an optimisation |
| `SampleLineService` | `SampleLineGroup.Create` / `SampleLine.Create` | Confirm the static `Create` overloads |
| `SampleLineService` | `sl.SwathWidthLeft/Right`, `group.AddSampledSurface` | Guarded; defaults apply if absent |
| `MaterialService` | `group.MaterialLists.Add`, `materialList.ImportCriteria`, `Material.Volume` | Guarded; volumes may need QTO surface mapping (see Preparing the template) |
| `SectionViewService` | `SectionView.Create(sampleLineId, point)` | Confirm the section-view `Create` overload |
| `ProfileViewService` | `ProfileView.Create(alignmentId, point)` | Stable, but verify the overload |
| `PdfPublisher` | `SheetType.SinglePdf` / `SheetType.MultiPdf` | Enum member names vary; pick the PDF members present |
| `PdfPublisher` | `PlotConfigManager.SetCurrentConfig("DWG To PDF.pc3")` | Ensure the PDF plotter name matches your install |

Everything else uses stable, long-lived API (databases, transactions, polylines, alignments,
surfaces, profiles), which is consistent across 2020–2024.

---

## Adding or changing a step

1. Add a value to `WorkflowStepType` (Models) at the right ordinal.
2. Add any needed operation to a Civil3D service (or a new service + interface).
3. Create a step class deriving from `WorkflowStepBase` (Application) that calls the service and
   reads/writes `WorkflowContextKeys`.
4. Register the step in `CompositionRoot.RegisterSteps` and the service in `ConfigureServices`.

The engine discovers the step automatically (it injects `IEnumerable<IWorkflowStep>` and orders by
`StepType`). No other layer changes.

---

## Configuration reference

All settings live in [`config/appsettings.json`](../config/appsettings.json) and map 1:1 to
`Models/Configuration/AppSettings.cs`. Notable sections: `Extraction` (segment length, road/contour
layers), `Assembly` (lane/shoulder/daylight geometry), `Corridor` (frequencies), `SampleLines`
(interval, swaths), `Sheets`/`Pdf` (scales, page setup, DPI), `Logging` (level, file retention), and
`ContinueOnStepFailure` (whether one failed step aborts the run).

---

## Testing

`tests/Civil3DAIAgent.Tests` (xUnit) covers all Civil-3D-free logic and the engine:

```powershell
dotnet test Civil3DAIAgent.sln -c Release
```

Coverage: result types, models, utilities (naming/tokens/units/guards), the workflow context and its
disposal, all loggers, JSON config loading (incl. malformed fallback), file service, input validation,
the ClosedXML Excel reader (round-trips a generated workbook), and the workflow engine (ordering,
skipping, continue-vs-abort on failure, exception recovery, cancellation, progress).

The 23 Civil 3D operation classes are integration-tested **inside Civil 3D** using the sample
`ROADS.dwg`/`POINTS.xlsx`, because they require the running host.
