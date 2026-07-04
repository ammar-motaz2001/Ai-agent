using System.Windows.Media;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Enums;

namespace Civil3DAIAgent.UI.ViewModels
{
    /// <summary>
    /// A single line in the on-screen log window, precomputing the display text and colour from a
    /// <see cref="LogEntry"/> so the XAML stays simple.
    /// </summary>
    public sealed class LogItemViewModel
    {
        /// <summary>Wraps a log entry for display.</summary>
        public LogItemViewModel(LogEntry entry)
        {
            var local = entry.TimestampUtc.ToLocalTime();
            Text = string.Format("{0:HH:mm:ss}  {1}", local, entry.Message);
            if (!string.IsNullOrEmpty(entry.Category))
                Text = string.Format("{0:HH:mm:ss}  [{1}] {2}", local, entry.Category, entry.Message);
            Brush = BrushFor(entry.Level);
        }

        /// <summary>Formatted, ready-to-display line.</summary>
        public string Text { get; }

        /// <summary>Colour reflecting the severity.</summary>
        public Brush Brush { get; }

        private static Brush BrushFor(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Warning: return Brushes.DarkGoldenrod;
                case LogLevel.Error: return Brushes.Firebrick;
                case LogLevel.Critical: return Brushes.Red;
                case LogLevel.Debug:
                case LogLevel.Trace: return Brushes.Gray;
                default: return Brushes.Black;
            }
        }
    }
}
