using System;
using Autodesk.AutoCAD.Runtime;
using Civil3DAIAgent.Services.Composition;
using Civil3DAIAgent.UI;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DAIAgent.Commands
{
    /// <summary>
    /// The AutoCAD command(s) exposed by the plugin. Kept intentionally minimal: resolve the automation
    /// facade and open the WPF window. All real work happens in the lower layers.
    /// </summary>
    public sealed class AgentCommands
    {
        /// <summary>
        /// <c>AICIVIL</c> — opens the Civil3D AI Agent window (modeless) so the road-design workflow can
        /// be configured and run without blocking Civil 3D.
        /// </summary>
        [CommandMethod("AICIVIL", CommandFlags.Modal)]
        public void LaunchAgent()
        {
            var ed = AcApp.DocumentManager?.MdiActiveDocument?.Editor;
            try
            {
                var automation = CompositionRoot.GetAutomationService();
                AgentLauncher.Show(automation);
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage("\nCould not open Civil3D AI Agent: " + ex.Message + "\n");
            }
        }
    }
}
