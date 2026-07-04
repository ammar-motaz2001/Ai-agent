using System.Globalization;

namespace Civil3DAIAgent.Models.Volumes
{
    /// <summary>
    /// Aggregate earthwork quantities produced by the material / cut-fill computation. Volumes are in
    /// cubic drawing units (cubic metres for the metric template).
    /// </summary>
    public sealed class VolumeSummary
    {
        /// <summary>Total cut (excavation) volume, adjusted by the cut factor.</summary>
        public double CutVolume { get; set; }

        /// <summary>Total fill (embankment) volume, adjusted by the fill factor.</summary>
        public double FillVolume { get; set; }

        /// <summary>Net volume (cut minus fill). Positive = surplus cut; negative = borrow required.</summary>
        public double NetVolume => CutVolume - FillVolume;

        /// <summary>True when neither cut nor fill was produced (e.g. an empty corridor).</summary>
        public bool IsEmpty => CutVolume <= 0 && FillVolume <= 0;

        /// <summary>Human-readable one-line summary for logging and the run report.</summary>
        public string ToDisplayString() =>
            string.Format(CultureInfo.InvariantCulture,
                "Cut = {0:N1} m³, Fill = {1:N1} m³, Net = {2:N1} m³ ({3})",
                CutVolume, FillVolume, NetVolume, NetVolume >= 0 ? "surplus" : "borrow");
    }
}
