using System.Windows;
using Civil3DAIAgent.Services.Facade;
using Civil3DAIAgent.UI.ViewModels;
using Civil3DAIAgent.UI.Views;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace Civil3DAIAgent.UI
{
    /// <summary>
    /// Bootstraps and shows the WPF UI as a modeless window owned by Civil 3D. Keeping the WPF creation
    /// here (rather than in the Commands project) means the command entry point never touches WPF types.
    /// Ensures only one window exists at a time.
    /// </summary>
    public static class AgentLauncher
    {
        private static MainWindow _window;

        /// <summary>
        /// Shows the automation window (or brings the existing one to the front). Must be called on the
        /// main thread from an AutoCAD command.
        /// </summary>
        public static void Show(IAutomationService automation)
        {
            if (_window != null)
            {
                // Already open — just re-focus it.
                _window.Activate();
                return;
            }

            _window = new MainWindow(new MainViewModel(automation));
            _window.Closed += (s, e) => _window = null;

            // ShowModelessWindow lets the window float above Civil 3D without blocking the CAD session.
            AcApp.ShowModelessWindow(_window);
        }
    }
}
