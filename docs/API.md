# API Reference

The public contracts you implement, call, or replace. Full XML documentation is on every type in the
source (and in the generated `.xml` doc files next to each DLL).

---

## Facade — start here

### `IAutomationService` (Services)
The single entry point used by the UI and command line.

```csharp
UiLogSink LogSink { get; }                              // subscribe for live log entries
AppSettings Settings { get; }                           // current effective settings
AppSettings ReloadSettings();                           // re-read appsettings.json
OperationResult ValidateInputs(WorkflowRequest request);
Task<WorkflowResult> RunAsync(WorkflowRequest request,
    IProgress<WorkflowProgress> progress, CancellationToken cancellationToken);
```

Resolve it via `CompositionRoot.GetAutomationService()`.

---

## Workflow contracts (Core)

### `IWorkflowStep`
One of the 23 operations.
```csharp
WorkflowStepType StepType { get; }                 // also defines run order
string DisplayName { get; }
bool CanExecute(IWorkflowContext context);         // false => Skipped
OperationResult Execute(IWorkflowContext context);
```

### `IWorkflowEngine`
```csharp
Task<WorkflowResult> RunAsync(WorkflowRequest request,
    IProgress<WorkflowProgress> progress, CancellationToken cancellationToken);
```
Never throws; inspect the returned `WorkflowResult`.

### `IWorkflowContext`
Shared state threaded through steps: `Request`, `Settings`, `Logger`, `CancellationToken`, a typed
state bag (`Set`/`TryGet`/`Get`/`Contains`), and `RegisterForDisposal`. Keys are constants in
`WorkflowContextKeys`.

---

## Ports (Core) — implemented in Infrastructure or Civil3D

| Interface | Default impl | Layer |
|-----------|--------------|-------|
| `IConfigurationProvider` | `JsonConfigurationProvider` | Infrastructure |
| `IExcelPointReader` | `ClosedXmlPointReader` | Infrastructure |
| `IFileService` | `FileService` | Infrastructure |
| `IInputValidator` | `InputValidator` | Infrastructure |
| `IExceptionExplainer` | `Civil3DExceptionExplainer` | Civil3D |
| `IPdfPublisher` | `PdfPublisher` | Civil3D |

To replace any of these, implement the interface and change its registration in `CompositionRoot`.

---

## Civil 3D operation services (Civil3D)

Each takes primitive inputs + a `Document` and returns `OperationResult<T>`.

| Interface | Steps | Key methods |
|-----------|-------|-------------|
| `IDrawingService` | 1–6 | `OpenSourceDatabase`, `SelectRoadPolyline`, `ExtractFirstSegment`, `CreateNewDrawing`, `PastePolyline`, `CopyContours` |
| `IAlignmentService` | 7 | `CreateFromPolyline` |
| `ISurfaceService` | 8 | `CreateExistingGround` |
| `IProfileService` | 9–10 | `CreateExistingGroundProfile`, `CreateDesignProfile` |
| `IAssemblyService` | 11 | `GetOrCreateAssembly` |
| `ICorridorService` | 12–14 | `CreateCorridor`, `CreateTopSurface`, `CreateDatumSurface` |
| `ISampleLineService` | 15 | `CreateSampleLines` |
| `IMaterialService` | 16–17 | `ComputeMaterials`, `ComputeCutFill` |
| `ISectionViewService` | 18 | `CreateSectionViews` |
| `IProfileViewService` | 19 | `CreateProfileViews` |
| `ISheetService` | 20–21 | `GenerateLayouts`, `CreateSheetSet` |
| `ISaveService` | 23 | `SaveDrawing` |

---

## Result & data types (Models)

- **`OperationResult` / `OperationResult<T>`** — `Succeeded`/`Failed`, `Message`, `Exception`,
  `Warnings`; factory methods `Ok(...)` / `Fail(...)`.
- **`WorkflowRequest`** — `InputDwgPath`, `InputExcelPath`, `OutputFolder`,
  `SegmentLengthMetersOverride`, `SelectedPolylineHandle`.
- **`WorkflowResult`** — `Steps` (per-step `StepResult`), `TotalDuration`, `OverallSuccess`,
  `FailureCount`, `WarningCount`, `OutputDwgPath`, `OutputPdfPaths`.
- **`StepResult`** — `Step`, `DisplayName`, `Status`, `Message`, `Warnings`, `Duration`, `Exception`.
- **`WorkflowProgress`** — `CompletedSteps`, `TotalSteps`, `CurrentStep`, `CurrentStepName`,
  `CurrentStatus`, `PercentComplete`.
- **`SurveyPoint`**, **`PolylineData`**, **`VolumeSummary`**, **`AppSettings`** (+ sub-sections).

---

## Logging (Logging)

`ILogger.Log(level, message, category, exception)` with extension helpers `Info`/`Warn`/`Error`/…​ and
`TimeOperation(...)`. Implementations: `FileLogger`, `UiLogSink` (raises `EntryLogged`),
`CompositeLogger`, `RoutingLogger`, `NullLogger`.
