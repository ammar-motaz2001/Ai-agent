using System;
using Autodesk.AutoCAD.Runtime;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Services.Composition;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

// Register the extension application (runs Initialize on NETLOAD/auto-load) and declare the command class.
[assembly: ExtensionApplication(typeof(Civil3DAIAgent.Commands.AgentExtensionApplication))]
[assembly: CommandClass(typeof(Civil3DAIAgent.Commands.AgentCommands))]

namespace Civil3DAIAgent.Commands
{
    /// <summary>
    /// Load/unload hook for the plugin. Civil 3D calls <see cref="Initialize"/> when the DLL is loaded
    /// (via NETLOAD or the auto-load registry entry) and <see cref="Terminate"/> at shutdown. We use it
    /// to eagerly build the composition root and print a friendly banner to the command line.
    /// </summary>
    public sealed class AgentExtensionApplication : IExtensionApplication
    {
        /// <inheritdoc />
        public void Initialize()
        {
            try
            {
                // Build the DI container up front so the first command is instant and any wiring error
                // surfaces at load time rather than on first use.
                var automation = CompositionRoot.GetAutomationService();
                // Use the Info() extension (not LogLevel.*) — 'Autodesk.AutoCAD.Runtime' also declares an
                // inaccessible 'LogLevel', which would shadow ours in this file.
                automation.LogSink.Info("Civil3D AI Agent loaded. Type AICIVIL to start.", "Startup");

                var ed = AcApp.DocumentManager?.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nCivil3D AI Agent loaded. Type AICIVIL to open the automation window.\n");
            }
            catch (System.Exception ex)
            {
                var ed = AcApp.DocumentManager?.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\nCivil3D AI Agent failed to initialize: " + ex.Message + "\n");
            }
        }

        /// <inheritdoc />
        public void Terminate()
        {
            // Nothing to clean up explicitly; the DI singletons are released with the process.
        }
    }
}
