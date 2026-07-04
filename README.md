# Civil3D AI Agent — Road Design Automation

A production-grade Autodesk **Civil 3D 2022** plugin that automates the entire preliminary
road-design workflow. You pick a source DWG (and an optional Excel points file), press **Start**, and
the tool runs all 23 Civil 3D operations end-to-end — from extracting the first 3 km of a road
polyline through corridor modelling, quantities, section/profile views, layout sheets, PDF export, and
saving the finished drawing.

> Built with clean architecture, dependency injection, SOLID, the repository/service patterns, MVVM,
> full logging, and "never-crash" error handling.

---

## What it does (the 23 automated steps)

| # | Step | # | Step |
|---|------|---|------|
| 1 | Open source DWG | 13 | Create Top surface |
| 2 | Select road polyline | 14 | Create Datum surface |
| 3 | Extract first 3 km | 15 | Create sample lines |
| 4 | Create new drawing | 16 | Compute materials |
| 5 | Paste polyline (preserving coordinates) | 17 | Compute cut & fill |
| 6 | Copy contours | 18 | Create section views |
| 7 | Create alignment | 19 | Create profile views |
| 8 | Create existing-ground surface | 20 | Generate layout sheets |
| 9 | Create existing-ground profile | 21 | Create sheet set |
| 10 | Create design profile (by layout) | 22 | Generate PDFs |
| 11 | Create assembly | 23 | Save final DWG |
| 12 | Create corridor | | |

---

## Quick start (5 steps)

1. **Build** the solution on a machine with Civil 3D 2022 installed (see
   [docs/INSTALLATION.md](docs/INSTALLATION.md)).
2. Launch **Civil 3D 2022**.
3. Type `NETLOAD` and select `Civil3DAIAgent.Commands.dll` from the build output.
4. Type `AICIVIL` — the automation window opens.
5. Choose the input DWG, (optional) Excel, and output folder, then click **▶ Start**.

The provided sample inputs `ROADS.dwg` and `POINTS.xlsx` (in the repo root) are ready to use.

---

## Key features

- **One-click automation** of the full road-design pipeline (23 steps).
- **Professional WPF UI** (MVVM): input pickers, live progress bar, per-step status list, streaming
  colour-coded log, Start/Cancel, Settings.
- **Never crashes**: every step runs inside its own try/catch + timing scope; failures are recorded,
  explained in plain English, and (by config) either skipped or abort the run.
- **Plain-language Civil 3D error explanations** — cryptic `ErrorStatus` codes become actionable text.
- **Full logging**: on-screen log window **and** a timestamped per-run log file, with execution time
  per step and a final summary.
- **Everything configurable** via [`config/appsettings.json`](config/appsettings.json) — no recompile
  to change lane widths, slopes, intervals, styles, scales, DPI, etc.
- **Clean architecture** with dependency injection, so each layer is testable and replaceable.

---

## Documentation

| Doc | Purpose |
|-----|---------|
| [Installation Guide](docs/INSTALLATION.md) | Prerequisites, building, NETLOAD, auto-load setup |
| [Developer Guide](docs/DEVELOPER_GUIDE.md) | Architecture deep-dive, template preparation, the version-sensitive API list, extending the workflow |
| [Architecture](docs/ARCHITECTURE.md) | Layer diagram, dependency rules, data flow |
| [API Reference](docs/API.md) | The key interfaces and how to implement/replace them |
| [Folder Structure](docs/FOLDER_STRUCTURE.md) | Every project and what lives in it |

---

## Solution layout

```
Civil3DAIAgent.sln
├─ src/
│  ├─ Civil3DAIAgent.Models          Pure DTOs, enums, config, results (no dependencies)
│  ├─ Civil3DAIAgent.Logging         ILogger + file/UI/composite/routing loggers
│  ├─ Civil3DAIAgent.Core            Abstractions (ports) + workflow engine contracts
│  ├─ Civil3DAIAgent.Utilities       Pure helpers (units, naming, tokens, guards)
│  ├─ Civil3DAIAgent.Infrastructure  Config (JSON), Excel (ClosedXML), files, validation
│  ├─ Civil3DAIAgent.Civil3D         All 23 Civil 3D API operations + PDF publisher
│  ├─ Civil3DAIAgent.Application     Workflow engine + 23 step classes
│  ├─ Civil3DAIAgent.Services        DI composition root + automation facade
│  ├─ Civil3DAIAgent.UI              WPF MVVM window (hosted in Civil 3D)
│  └─ Civil3DAIAgent.Commands        NETLOAD entry point (AICIVIL command)
├─ tests/Civil3DAIAgent.Tests        xUnit tests (pure logic + engine)
├─ config/appsettings.json           All tunable parameters
├─ docs/                             Documentation
└─ examples/                         Sample inputs & usage notes
```

---

## Requirements

- Windows 10/11 (64-bit)
- Autodesk **Civil 3D 2022**
- .NET Framework 4.8
- Visual Studio 2022 (to build)

---

## Important notes

- The plugin runs **inside** Civil 3D (it is not a standalone .exe) because the Civil 3D .NET API is
  only available in-process. This is the standard, reliable deployment model.
- For the corridor to produce geometry, your template should contain a **pre-built assembly** named as
  configured (`Assembly.Name`). The .NET API cannot instantiate stock subassemblies; see
  [docs/DEVELOPER_GUIDE.md → Preparing the template](docs/DEVELOPER_GUIDE.md#preparing-the-template).

## License

Provided as-is for the commissioning client. See your engagement agreement.
