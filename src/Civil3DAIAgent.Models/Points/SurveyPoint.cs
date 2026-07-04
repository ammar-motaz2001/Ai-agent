using System;

namespace Civil3DAIAgent.Models.Points
{
    /// <summary>
    /// A single survey / COGO point read from the Excel input file. These points can be used to
    /// build or supplement the Existing Ground surface, or to be inserted as Civil 3D COGO points.
    /// </summary>
    /// <remarks>
    /// The coordinate convention follows Civil 3D / survey usage:
    /// <list type="bullet">
    /// <item><see cref="Easting"/> maps to the drawing X axis.</item>
    /// <item><see cref="Northing"/> maps to the drawing Y axis.</item>
    /// <item><see cref="Elevation"/> maps to the drawing Z axis.</item>
    /// </list>
    /// This class is immutable to guarantee that parsed data cannot be mutated downstream.
    /// </remarks>
    public sealed class SurveyPoint
    {
        /// <summary>Creates an immutable survey point.</summary>
        /// <param name="pointNumber">Optional point number / id from the source file.</param>
        /// <param name="easting">Easting (X) coordinate in drawing units.</param>
        /// <param name="northing">Northing (Y) coordinate in drawing units.</param>
        /// <param name="elevation">Elevation (Z) coordinate in drawing units.</param>
        /// <param name="description">Optional raw description / code (e.g. "EP", "CL", "TREE").</param>
        public SurveyPoint(long pointNumber, double easting, double northing, double elevation, string description)
        {
            PointNumber = pointNumber;
            Easting = easting;
            Northing = northing;
            Elevation = elevation;
            Description = description ?? string.Empty;
        }

        /// <summary>Point number / identifier from the source file (0 if the file had no id column).</summary>
        public long PointNumber { get; }

        /// <summary>Easting (X) coordinate in drawing units.</summary>
        public double Easting { get; }

        /// <summary>Northing (Y) coordinate in drawing units.</summary>
        public double Northing { get; }

        /// <summary>Elevation (Z) coordinate in drawing units.</summary>
        public double Elevation { get; }

        /// <summary>Raw description / feature code. Never null (empty string when absent).</summary>
        public string Description { get; }

        /// <summary>
        /// Returns <c>true</c> when the coordinate values are finite (not NaN / infinity). Used by the
        /// Excel reader to reject malformed rows without throwing.
        /// </summary>
        public bool IsValid =>
            !double.IsNaN(Easting) && !double.IsInfinity(Easting) &&
            !double.IsNaN(Northing) && !double.IsInfinity(Northing) &&
            !double.IsNaN(Elevation) && !double.IsInfinity(Elevation);

        /// <inheritdoc />
        public override string ToString() =>
            string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "#{0} E={1:F3} N={2:F3} Z={3:F3} '{4}'",
                PointNumber, Easting, Northing, Elevation, Description);
    }
}
