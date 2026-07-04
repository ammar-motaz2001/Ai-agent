# Folder Structure

```
civil/
├─ Civil3DAIAgent.sln                  Visual Studio solution (11 projects + tests)
├─ Directory.Build.props               Shared build config: net48, x64, Civil 3D path resolution
├─ README.md                           Overview & quick start
├─ ROADS.dwg                           Sample source drawing (your input)
├─ POINTS.xlsx                         Sample survey points (your input)
│
├─ config/
│  └─ appsettings.json                 ALL tunable parameters (copy next to the plugin DLL)
│
├─ docs/
│  ├─ INSTALLATION.md                  Build, NETLOAD, auto-load, troubleshooting
│  ├─ DEVELOPER_GUIDE.md               Architecture, template prep, [VERSION] list, extending
│  ├─ ARCHITECTURE.md                  Diagrams, layer responsibilities, sequences
│  ├─ API.md                           Public interfaces & data types
│  └─ FOLDER_STRUCTURE.md              This file
│
├─ examples/
│  └─ README.md                        How to run against ROADS.dwg + POINTS.xlsx
│
├─ src/
│  ├─ Civil3D.References.props         Shared Autodesk assembly references (CopyLocal=false)
│  │
│  ├─ Civil3DAIAgent.Models/           ── Pure data (no dependencies) ──
│  │  ├─ Enums/                        WorkflowStepType, StepStatus, LogLevel, SurfaceKind
│  │  ├─ Configuration/AppSettings.cs  Full settings tree (maps to appsettings.json)
│  │  ├─ Points/SurveyPoint.cs
│  │  ├─ Geometry/PolylineData.cs
│  │  ├─ Results/OperationResult.cs
│  │  ├─ Volumes/VolumeSummary.cs
│  │  └─ Workflow/                     WorkflowRequest, StepResult, WorkflowResult, WorkflowProgress
│  │
│  ├─ Civil3DAIAgent.Logging/          ── Logging (refs Models) ──
│  │  ├─ ILogger.cs, LogEntry.cs, LoggerExtensions.cs
│  │  └─ FileLogger, UiLogSink, CompositeLogger, RoutingLogger, NullLogger
│  │
│  ├─ Civil3DAIAgent.Core/             ── Abstractions (refs Models, Logging) ──
│  │  ├─ Abstractions/                 IConfigurationProvider, IExcelPointReader, IFileService,
│  │  │                                IPdfPublisher, IExceptionExplainer, IInputValidator
│  │  └─ Workflow/                     IWorkflowStep, IWorkflowEngine, IWorkflowContext,
│  │                                   WorkflowContext, WorkflowContextKeys
│  │
│  ├─ Civil3DAIAgent.Utilities/        ── Pure helpers (refs Models) ──
│  │  ├─ Guard.cs
│  │  ├─ Units/UnitConverter.cs
│  │  └─ Text/TokenReplacer.cs, NameUtils.cs
│  │
│  ├─ Civil3DAIAgent.Infrastructure/   ── Non-Civil-3D implementations ──
│  │  ├─ Configuration/JsonConfigurationProvider.cs   (Newtonsoft.Json)
│  │  ├─ Excel/ClosedXmlPointReader.cs                (ClosedXML)
│  │  ├─ IO/FileService.cs
│  │  └─ Validation/InputValidator.cs
│  │
│  ├─ Civil3DAIAgent.Civil3D/          ── The Autodesk API layer ──
│  │  ├─ Diagnostics/Civil3DExceptionExplainer.cs
│  │  ├─ Support/                      CivilDocProvider, TransactionHelper, StyleResolver, HandleUtils
│  │  └─ Services/                     DrawingService, AlignmentService, SurfaceService,
│  │                                   ProfileService, AssemblyService, CorridorService,
│  │                                   SampleLineService, MaterialService, ProfileViewService,
│  │                                   SectionViewService, SheetService, PdfPublisher, SaveService
│  │
│  ├─ Civil3DAIAgent.Application/      ── Orchestration ──
│  │  └─ Workflow/                     WorkflowEngine, WorkflowStepBase
│  │     └─ Steps/                     DrawingSteps, AlignmentSurfaceProfileSteps, CorridorSteps,
│  │                                   QuantityAndViewSteps, OutputSteps  (23 step classes)
│  │
│  ├─ Civil3DAIAgent.Services/         ── Composition root + facade ──
│  │  ├─ Composition/CompositionRoot.cs
│  │  └─ Facade/IAutomationService.cs, AutomationService.cs
│  │
│  ├─ Civil3DAIAgent.UI/               ── WPF MVVM (hosted in Civil 3D) ──
│  │  ├─ Mvvm/ObservableObject.cs, RelayCommand.cs
│  │  ├─ ViewModels/MainViewModel.cs, StepItemViewModel.cs, LogItemViewModel.cs
│  │  ├─ Views/MainWindow.xaml (+ .cs)
│  │  └─ AgentLauncher.cs
│  │
│  └─ Civil3DAIAgent.Commands/         ── NETLOAD entry point ──
│     ├─ AgentCommands.cs              [CommandMethod("AICIVIL")]
│     └─ AgentExtensionApplication.cs  IExtensionApplication load hooks
│
└─ tests/
   └─ Civil3DAIAgent.Tests/            xUnit tests
      ├─ Utilities/UtilitiesTests.cs
      ├─ Models/ModelsTests.cs
      ├─ Core/WorkflowContextTests.cs
      ├─ Logging/LoggingTests.cs
      ├─ Infrastructure/InfrastructureTests.cs
      └─ Application/WorkflowEngineTests.cs
```

## Build outputs

| Project | Output | Notes |
|---------|--------|-------|
| `Civil3DAIAgent.Commands` | `Commands.dll` (+ all deps) | **This is what you NETLOAD** |
| Other `src` projects | class libraries | Copied next to Commands.dll |
| `Civil3DAIAgent.Tests` | test assembly | Run with `dotnet test` |

The Autodesk DLLs (`acdbmgd`, `acmgd`, `AeccDbMgd`, …) are referenced with `CopyLocal=false` and are
**not** in the output — Civil 3D provides them at runtime.
