using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Civil3DAIAgent.UI.ViewModels;

namespace Civil3DAIAgent.UI.Views
{
    /// <summary>
    /// Code-behind for the main window. Deliberately thin (MVVM): it only sets the data context and
    /// keeps the log list auto-scrolled to the newest entry.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Creates the window with its view model.</summary>
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Auto-scroll the log to the latest line as entries arrive.
            if (viewModel.LogItems is INotifyCollectionChanged incc)
            {
                incc.CollectionChanged += (s, e) =>
                {
                    if (e.Action == NotifyCollectionChangedAction.Add && LogList.Items.Count > 0)
                        LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
                };
            }
        }
    }
}
