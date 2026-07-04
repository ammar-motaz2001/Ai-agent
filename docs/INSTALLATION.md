# Installation Guide

This guide assumes **no prior Civil 3D programming experience**. Follow it top to bottom.

---

## 1. Prerequisites

Install these on the machine where you will **build** the plugin (usually your workstation that also
runs Civil 3D):

| Software | Version | Notes |
|----------|---------|-------|
| Windows | 10 or 11, 64-bit | Civil 3D is 64-bit only |
| Autodesk Civil 3D | **2022** | Provides the .NET API DLLs the plugin references |
| .NET Framework | 4.8 | Ships with modern Windows; Civil 3D 2022 targets it |
| Visual Studio | 2022 (Community is fine) | Install the **.NET desktop development** workload |
| .NET SDK | 6.0+ | For `dotnet build`/`dotnet test` from the CLI (optional) |

> Civil 3D 2022 installs its managed API DLLs at `C:\Program Files\Autodesk\AutoCAD 2022`. The build
> is pre-configured for this path.

---

## 2. Get the source

Copy/clone the repository so the folder looks like:

```
civil\
  Civil3DAIAgent.sln
  Directory.Build.props
  src\  tests\  docs\  config\  examples\
  ROADS.dwg   POINTS.xlsx        (your sample inputs)
```

---

## 3. Point the build at your Civil 3D install (only if non-default)

The build reads the Civil 3D DLLs from `C:\Program Files\Autodesk\AutoCAD 2022`. If yours is elsewhere,
set an environment variable **once** (Start → "Edit the system environment variables"):

```
CIVIL3D_PATH = D:\Autodesk\AutoCAD 2022
```

Or edit `Civil3DPath` in [`Directory.Build.props`](../Directory.Build.props).

---

## 4. Build

**Option A — Visual Studio**
1. Open `Civil3DAIAgent.sln`.
2. Set the configuration to **Release | x64**.
3. Build → Build Solution (`Ctrl+Shift+B`).

**Option B — Command line**
```powershell
dotnet restore Civil3DAIAgent.sln
dotnet build Civil3DAIAgent.sln -c Release
```

The output you care about is in:
```
src\Civil3DAIAgent.Commands\bin\x64\Release\net48\
```
This folder contains `Civil3DAIAgent.Commands.dll` plus all its dependencies (the other project DLLs,
Newtonsoft.Json, ClosedXML, Microsoft.Extensions.DependencyInjection). The Autodesk DLLs are **not**
copied here on purpose — Civil 3D provides them.

> **Copy `config\appsettings.json` next to `Civil3DAIAgent.Commands.dll`** (or into a `config`
> sub-folder there). The plugin searches both locations. Without it, built-in defaults are used.

---

## 5. Load the plugin (per session)

1. Start **Civil 3D 2022** and open (or create) any drawing.
2. Type `NETLOAD` on the command line and press Enter.
3. Browse to `...\Civil3DAIAgent.Commands\bin\x64\Release\net48\Civil3DAIAgent.Commands.dll` and open it.
4. You should see: `Civil3D AI Agent loaded. Type AICIVIL to open the automation window.`

If Windows blocks the DLL (downloaded file), right-click each DLL → Properties → **Unblock**, or run
this once in the output folder:
```powershell
Get-ChildItem -Recurse *.dll | Unblock-File
```

---

## 6. Load automatically on startup (optional)

To avoid `NETLOAD` every session, register the plugin in the Windows Registry (per Civil 3D version).
Create `Civil3DAIAgent.reg`, edit the path, and double-click it:

```
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\Autodesk\AutoCAD\R24.1\ACAD-5101:409\Applications\Civil3DAIAgent]
"DESCRIPTION"="Civil3D AI Agent"
"LOADCTRLS"=dword:00000002
"MANAGED"=dword:00000001
"LOADER"="C:\\Path\\To\\Civil3DAIAgent.Commands.dll"
```

- `R24.1` is Civil 3D **2022**'s AutoCAD release key. `ACAD-5101:409` is the US-English product key;
  adjust for your locale/product (check `HKCU\Software\Autodesk\AutoCAD\R24.1`).
- `LOADCTRLS=2` loads at startup.

---

## 7. Run it

1. Type `AICIVIL`.
2. In the window: pick **Input DWG** (`ROADS.dwg`), optionally **Input Excel** (`POINTS.xlsx`), and an
   **Output folder**.
3. Click **▶ Start** and watch the progress bar, step list, and log.
4. When done, use **Open output** to see the saved DWG, PDFs, sheet set, and `logs\` folder.

---

## Troubleshooting

| Symptom | Fix |
|--------|-----|
| `NETLOAD` says "file cannot be loaded" | Unblock the DLLs (step 5), confirm **x64 Release** build, confirm .NET Framework 4.8 |
| "Could not open Civil3D AI Agent" | A dependency DLL is missing from the output folder — rebuild; ensure `CopyLocalLockFileAssemblies` copied Newtonsoft/ClosedXML/DI |
| Styles missing warnings in the log | Your template lacks the named styles in `appsettings.json`; the tool falls back to defaults. Add the styles or update the names |
| Corridor is empty | Add a pre-built assembly to the template — see the Developer Guide |
| Build error near a `// [VERSION]` call | Your Civil 3D API differs slightly; see the Developer Guide's version list |

See [DEVELOPER_GUIDE.md](DEVELOPER_GUIDE.md) for deeper topics.
