using System.Windows.Media;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.UI.Mvvm;

namespace Civil3DAIAgent.UI.ViewModels
{
    /// <summary>
    /// Row in the step-list panel: shows one workflow step's name and live status (with a colour and
    /// glyph), so the user sees exactly where the run is and which steps succeeded/failed/were skipped.
    /// </summary>
    public sealed class StepItemViewModel : ObservableObject
    {
        private StepStatus _status = StepStatus.Pending;

        /// <summary>Creates a row for a step.</summary>
        public StepItemViewModel(WorkflowStepType step, int number, string displayName)
        {
            Step = step;
            Number = number;
            DisplayName = displayName;
        }

        /// <summary>The step this row represents.</summary>
        public WorkflowStepType Step { get; }

        /// <summary>1-based ordinal shown in the list.</summary>
        public int Number { get; }

        /// <summary>Friendly step name.</summary>
        public string DisplayName { get; }

        /// <summary>Current status; setting it refreshes the glyph, colour, and status text.</summary>
        public StepStatus Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(Glyph));
                    OnPropertyChanged(nameof(StatusBrush));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>A single-character status glyph.</summary>
        public string Glyph
        {
            get
            {
                switch (_status)
                {
                    case StepStatus.Running: return "▶";
                    case StepStatus.Succeeded: return "✔";
                    case StepStatus.CompletedWithWarnings: return "⚠";
                    case StepStatus.Failed: return "✖";
                    case StepStatus.Skipped: return "–";
                    case StepStatus.Cancelled: return "■";
                    default: return "○";
                }
            }
        }

        /// <summary>Text label for the status.</summary>
        public string StatusText => _status.ToString();

        /// <summary>Colour used for the glyph/label.</summary>
        public Brush StatusBrush
        {
            get
            {
                switch (_status)
                {
                    case StepStatus.Running: return Brushes.DodgerBlue;
                    case StepStatus.Succeeded: return Brushes.ForestGreen;
                    case StepStatus.CompletedWithWarnings: return Brushes.DarkGoldenrod;
                    case StepStatus.Failed: return Brushes.Firebrick;
                    case StepStatus.Skipped: return Brushes.Gray;
                    case StepStatus.Cancelled: return Brushes.DimGray;
                    default: return Brushes.DarkGray;
                }
            }
        }
    }
}
