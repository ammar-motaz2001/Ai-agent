using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Workflow;
using Civil3DAIAgent.Services.Facade;
using Civil3DAIAgent.UI.Mvvm;

namespace Civil3DAIAgent.UI.ViewModels
{
    /// <summary>
    /// The main window's view model. Binds the input pickers, run/cancel commands, progress bar, live
    /// log, and per-step status list to the automation facade. The workflow runs synchronously on the
    /// main (document) thread to satisfy Civil 3D's thread affinity — see <see cref="StartAsync"/>.
    /// </summary>
    public sealed class MainViewModel : ObservableObject
    {
        private const int MaxLogItems = 5000;

        private readonly IAutomationService _automation;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _cts;

        private string _inputDwgPath = "";
        private string _inputExcelPath = "";
        private string _outputFolder = "";
        private double _segmentLengthMeters;
        private int _maxStep = 1; // DEBUG default: run only step 1. Set 0 to run all 23.
        private bool _isRunning;
        private double _progressValue;
        private string _statusText = "Ready.";
        private string _currentStepText = "";

        /// <summary>Creates the view model and wires the log sink.</summary>
        public MainViewModel(IAutomationService automation)
        {
            _automation = automation ?? throw new ArgumentNullException(nameof(automation));
            _dispatcher = Dispatcher.CurrentDispatcher;

            // Seed inputs from configured defaults.
            var paths = _automation.Settings.Paths;
            _inputDwgPath = paths.DefaultInputDwg ?? "";
            _inputExcelPath = paths.DefaultInputExcel ?? "";
            _outputFolder = paths.DefaultOutputFolder ?? "";
            _segmentLengthMeters = _automation.Settings.Extraction.SegmentLengthMeters;

            BuildStepList();

            // Live log streaming from the routing logger's UI sink.
            _automation.LogSink.EntryLogged += OnEntryLogged;

            BrowseDwgCommand = new RelayCommand(BrowseDwg);
            BrowseExcelCommand = new RelayCommand(BrowseExcel);
            BrowseOutputCommand = new RelayCommand(BrowseOutput);
            OpenSettingsCommand = new RelayCommand(OpenSettings);
            ReloadSettingsCommand = new RelayCommand(ReloadSettings);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => Directory.Exists(OutputFolder));
            ClearLogCommand = new RelayCommand(() => LogItems.Clear());
            StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning);
            CancelCommand = new RelayCommand(Cancel, () => IsRunning);
        }

        // ------------------------------------------------------------------ Bindable state
        /// <summary>Path to the source DWG.</summary>
        public string InputDwgPath { get => _inputDwgPath; set => SetProperty(ref _inputDwgPath, value); }

        /// <summary>Path to the (optional) Excel points file.</summary>
        public string InputExcelPath { get => _inputExcelPath; set => SetProperty(ref _inputExcelPath, value); }

        /// <summary>Output folder for DWG/PDF/sheets/logs.</summary>
        public string OutputFolder
        {
            get => _outputFolder;
            set { if (SetProperty(ref _outputFolder, value)) OpenOutputFolderCommand?.RaiseCanExecuteChanged(); }
        }

        /// <summary>Extraction length (metres); overrides the configured default when &gt; 0.</summary>
        public double SegmentLengthMeters { get => _segmentLengthMeters; set => SetProperty(ref _segmentLengthMeters, value); }

        /// <summary>
        /// DEBUG: run only the first N steps (0 = all 23). Increase one at a time to isolate a crashing
        /// step. The full run log (and C:\Temp\Civil3DAIAgent.log) records exactly where it stops.
        /// </summary>
        public int MaxStep { get => _maxStep; set => SetProperty(ref _maxStep, value); }

        /// <summary>True while a run is in progress (drives control enable/disable).</summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    StartCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Progress percentage (0-100).</summary>
        public double ProgressValue { get => _progressValue; private set => SetProperty(ref _progressValue, value); }

        /// <summary>Status line shown under the progress bar.</summary>
        public string StatusText { get => _statusText; private set => SetProperty(ref _statusText, value); }

        /// <summary>Name of the step currently executing.</summary>
        public string CurrentStepText { get => _currentStepText; private set => SetProperty(ref _currentStepText, value); }

        /// <summary>The 23 steps with live status.</summary>
        public ObservableCollection<StepItemViewModel> Steps { get; } = new ObservableCollection<StepItemViewModel>();

        /// <summary>The live log lines.</summary>
        public ObservableCollection<LogItemViewModel> LogItems { get; } = new ObservableCollection<LogItemViewModel>();

        // ------------------------------------------------------------------ Commands
        /// <summary>Browse for the input DWG.</summary>
        public RelayCommand BrowseDwgCommand { get; }
        /// <summary>Browse for the Excel points file.</summary>
        public RelayCommand BrowseExcelCommand { get; }
        /// <summary>Browse for the output folder.</summary>
        public RelayCommand BrowseOutputCommand { get; }
        /// <summary>Open appsettings.json in the default editor.</summary>
        public RelayCommand OpenSettingsCommand { get; }
        /// <summary>Reload settings from disk.</summary>
        public RelayCommand ReloadSettingsCommand { get; }
        /// <summary>Open the output folder in Explorer.</summary>
        public RelayCommand OpenOutputFolderCommand { get; }
        /// <summary>Clear the on-screen log.</summary>
        public RelayCommand ClearLogCommand { get; }
        /// <summary>Start the full workflow.</summary>
        public AsyncRelayCommand StartCommand { get; }
        /// <summary>Cancel the running workflow.</summary>
        public RelayCommand CancelCommand { get; }

        // ------------------------------------------------------------------ Run
        private async Task StartAsync()
        {
            var request = new WorkflowRequest
            {
                InputDwgPath = InputDwgPath?.Trim(),
                InputExcelPath = InputExcelPath?.Trim(),
                OutputFolder = OutputFolder?.Trim(),
                SegmentLengthMetersOverride = SegmentLengthMeters,
                MaxStep = MaxStep
            };

            AppendLog(LogLevel.Information, $"START clicked. MaxStep={MaxStep} (0=all). Crash log: C:\\Temp\\Civil3DAIAgent.log", "UI");

            // Pre-flight validation with friendly feedback.
            var validation = _automation.ValidateInputs(request);
            foreach (var w in validation.Warnings) AppendLog(LogLevel.Warning, w, "Validate");
            if (validation.Failed)
            {
                System.Windows.MessageBox.Show(validation.Message, "Cannot start",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            ResetStepList();
            IsRunning = true;
            ProgressValue = 0;
            StatusText = "Running...";
            _cts = new CancellationTokenSource();

            var progress = new Progress<WorkflowProgress>(OnProgress); // callbacks marshalled to this (UI) thread

            WorkflowResult result = null;
            try
            {
                AppendLog(LogLevel.Information, "Starting workflow on the main document thread…", "UI");

                // IMPORTANT: This runs synchronously on the current (main AutoCAD/UI) thread, which is
                // application context here (the modeless window has no active command). That satisfies
                // Civil 3D's thread affinity AND allows creating a new document (step 4). We deliberately
                // do NOT use ExecuteInCommandContextAsync (it binds to the original document and faults
                // when the workflow switches to the new drawing) and do NOT hop threads.
                result = await _automation.RunAsync(request, progress, _cts.Token);
            }
            catch (Exception ex)
            {
                // Absolute UI-level backstop so nothing escapes into Civil 3D.
                AppendLog(LogLevel.Critical, "Run failed unexpectedly: " + ex.Message, "UI");
                System.Windows.MessageBox.Show(
                    "The run stopped due to an unexpected error:\n\n" + ex.Message +
                    "\n\nSee the log window and the run log file for details.",
                    "Civil3D AI Agent", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsRunning = false;
                _cts?.Dispose();
                _cts = null;
                FinishRun(result);
            }
        }

        private void Cancel()
        {
            StatusText = "Cancelling...";
            _cts?.Cancel();
        }

        private void OnProgress(WorkflowProgress p)
        {
            ProgressValue = p.PercentComplete;
            CurrentStepText = $"Step {(int)p.CurrentStep}/23 — {p.CurrentStepName} ({p.CurrentStatus})";
            foreach (var s in Steps)
            {
                if (s.Step == p.CurrentStep) { s.Status = p.CurrentStatus; break; }
            }
        }

        private void FinishRun(WorkflowResult result)
        {
            if (result == null)
            {
                StatusText = "Finished (see log).";
                return;
            }

            // Reconcile the step list with the authoritative per-step results.
            foreach (var sr in result.Steps)
                foreach (var s in Steps)
                    if (s.Step == sr.Step) { s.Status = sr.Status; break; }

            ProgressValue = result.WasCancelled ? ProgressValue : 100;
            StatusText = result.WasCancelled
                ? $"Cancelled after {result.TotalDuration.TotalSeconds:F0}s."
                : (result.OverallSuccess
                    ? $"Completed successfully in {result.TotalDuration.TotalSeconds:F0}s."
                    : $"Completed with {result.FailureCount} failure(s), {result.WarningCount} warning(s) in {result.TotalDuration.TotalSeconds:F0}s.");

            OpenOutputFolderCommand.RaiseCanExecuteChanged();
        }

        // ------------------------------------------------------------------ Pickers / settings
        private void BrowseDwg()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "AutoCAD Drawing (*.dwg)|*.dwg|All files (*.*)|*.*" };
            if (!string.IsNullOrEmpty(InputDwgPath)) dlg.InitialDirectory = SafeDir(InputDwgPath);
            if (dlg.ShowDialog() == true) InputDwgPath = dlg.FileName;
        }

        private void BrowseExcel()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Excel (*.xlsx;*.xls)|*.xlsx;*.xls|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true) InputExcelPath = dlg.FileName;
        }

        private void BrowseOutput()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Select the output folder" })
            {
                if (Directory.Exists(OutputFolder)) dlg.SelectedPath = OutputFolder;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) OutputFolder = dlg.SelectedPath;
            }
        }

        private void OpenSettings()
        {
            try
            {
                string path = LocateSettingsFile();
                if (path != null && File.Exists(path))
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                else
                    System.Windows.MessageBox.Show("appsettings.json was not found next to the plugin. Edit the file in your deployment's config folder.",
                        "Settings", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppendLog(LogLevel.Warning, "Could not open settings: " + ex.Message, "UI");
            }
        }

        private void ReloadSettings()
        {
            _automation.ReloadSettings();
            SegmentLengthMeters = _automation.Settings.Extraction.SegmentLengthMeters;
            AppendLog(LogLevel.Information, "Settings reloaded from appsettings.json.", "UI");
        }

        private void OpenOutputFolder()
        {
            if (Directory.Exists(OutputFolder))
                Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
        }

        // ------------------------------------------------------------------ Logging plumbing
        private void OnEntryLogged(object sender, LogEntry entry) => AppendLog(entry.Level, FormatEntry(entry), entry.Category, entry);

        private static string FormatEntry(LogEntry e) => e.Message;

        private void AppendLog(LogLevel level, string message, string category, LogEntry existing = null)
        {
            var entry = existing ?? new LogEntry(DateTime.UtcNow, level, message, category, null);
            void Append()
            {
                LogItems.Add(new LogItemViewModel(entry));
                if (LogItems.Count > MaxLogItems) LogItems.RemoveAt(0);
            }
            if (_dispatcher.CheckAccess()) Append();
            else _dispatcher.BeginInvoke((Action)Append);
        }

        // ------------------------------------------------------------------ Helpers
        private void BuildStepList()
        {
            Steps.Clear();
            int n = 1;
            foreach (WorkflowStepType step in Enum.GetValues(typeof(WorkflowStepType)))
                Steps.Add(new StepItemViewModel(step, n++, Humanize(step)));
        }

        private void ResetStepList()
        {
            foreach (var s in Steps) s.Status = StepStatus.Pending;
        }

        private static string Humanize(WorkflowStepType step)
        {
            // Insert spaces before capitals: "CreateAlignment" -> "Create Alignment".
            var raw = step.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && !char.IsUpper(raw[i - 1])) sb.Append(' ');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        private static string SafeDir(string path)
        {
            try { return Path.GetDirectoryName(path) ?? ""; } catch { return ""; }
        }

        private static string LocateSettingsFile()
        {
            var baseDir = Path.GetDirectoryName(typeof(MainViewModel).Assembly.Location) ?? "";
            var candidates = new[]
            {
                Path.Combine(baseDir, "appsettings.json"),
                Path.Combine(baseDir, "config", "appsettings.json"),
                Path.Combine(Directory.GetParent(baseDir)?.FullName ?? baseDir, "config", "appsettings.json")
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            return candidates[0];
        }
    }
}
