using System;

namespace Civil3DAIAgent.Utilities.Units
{
    /// <summary>
    /// Pure conversions between the units used in road design. The drawing/model is assumed to be in
    /// metres (the metric NCS template); these helpers make slope and grade handling explicit and
    /// testable rather than scattering magic numbers through the Civil 3D code.
    /// </summary>
    public static class UnitConverter
    {
        private const double MetersPerFoot = 0.3048;

        /// <summary>Feet to metres.</summary>
        public static double FeetToMeters(double feet) => feet * MetersPerFoot;

        /// <summary>Metres to feet.</summary>
        public static double MetersToFeet(double meters) => meters / MetersPerFoot;

        /// <summary>Converts a percent grade (e.g. -2.0) to a decimal slope (e.g. -0.02).</summary>
        public static double PercentToSlope(double percent) => percent / 100.0;

        /// <summary>Converts a decimal slope (e.g. -0.02) to a percent grade (e.g. -2.0).</summary>
        public static double SlopeToPercent(double slope) => slope * 100.0;

        /// <summary>
        /// Converts a "run:1" side-slope value (e.g. 2 meaning 2:1) to a decimal slope magnitude
        /// (0.5). Used when configuring daylight subassemblies whose parameters are decimals.
        /// </summary>
        public static double RunToRiseToSlope(double runPerRise)
        {
            if (Math.Abs(runPerRise) < double.Epsilon) return 0;
            return 1.0 / runPerRise;
        }
    }
}
