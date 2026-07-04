using System;
using System.Collections.Generic;
using Civil3DAIAgent.Utilities;
using Civil3DAIAgent.Utilities.Text;
using Civil3DAIAgent.Utilities.Units;
using Xunit;

namespace Civil3DAIAgent.Tests.Utilities
{
    public class NameUtilsTests
    {
        [Fact]
        public void MakeUnique_ReturnsDesired_WhenNotTaken()
        {
            var result = NameUtils.MakeUnique("Alignment", new[] { "Other" });
            Assert.Equal("Alignment", result);
        }

        [Fact]
        public void MakeUnique_AppendsCounter_WhenTaken()
        {
            var result = NameUtils.MakeUnique("Surface", new[] { "Surface" });
            Assert.Equal("Surface (2)", result);
        }

        [Fact]
        public void MakeUnique_SkipsExistingCounters()
        {
            var result = NameUtils.MakeUnique("Surface", new[] { "Surface", "Surface (2)" });
            Assert.Equal("Surface (3)", result);
        }

        [Fact]
        public void MakeUnique_IsCaseInsensitive()
        {
            var result = NameUtils.MakeUnique("road", new[] { "ROAD" });
            Assert.Equal("road (2)", result);
        }

        [Fact]
        public void MakeUnique_HandlesEmptyInput()
        {
            var result = NameUtils.MakeUnique("", Array.Empty<string>());
            Assert.Equal("Item", result);
        }
    }

    public class TokenReplacerTests
    {
        [Fact]
        public void Replace_SubstitutesKnownTokens_CaseInsensitively()
        {
            var tokens = new Dictionary<string, string> { { "drawing", "ROADS" } };
            Assert.Equal("ROADS_sheets.pdf", TokenReplacer.Replace("{DRAWING}_sheets.pdf", tokens));
        }

        [Fact]
        public void Replace_LeavesUnknownTokensUntouched()
        {
            var tokens = new Dictionary<string, string> { { "drawing", "X" } };
            Assert.Equal("X_{unknown}", TokenReplacer.Replace("{drawing}_{unknown}", tokens));
        }

        [Fact]
        public void ReplaceDrawingAndDate_FormatsDate()
        {
            var date = new DateTime(2026, 7, 4);
            var result = TokenReplacer.ReplaceDrawingAndDate("{drawing}-{date}.pdf", "ROADS", date);
            Assert.Equal("ROADS-20260704.pdf", result);
        }
    }

    public class UnitConverterTests
    {
        [Theory]
        [InlineData(-2.0, -0.02)]
        [InlineData(4.0, 0.04)]
        public void PercentToSlope_Converts(double percent, double expected)
        {
            Assert.Equal(expected, UnitConverter.PercentToSlope(percent), 6);
        }

        [Fact]
        public void RunToRiseToSlope_InvertsRatio()
        {
            Assert.Equal(0.5, UnitConverter.RunToRiseToSlope(2.0), 6); // 2:1 -> 0.5
        }

        [Fact]
        public void RunToRiseToSlope_ZeroIsSafe()
        {
            Assert.Equal(0.0, UnitConverter.RunToRiseToSlope(0.0), 6);
        }

        [Fact]
        public void FeetMeters_RoundTrip()
        {
            double meters = UnitConverter.FeetToMeters(10);
            Assert.Equal(10.0, UnitConverter.MetersToFeet(meters), 6);
        }
    }

    public class GuardTests
    {
        [Fact]
        public void NotNull_Throws_OnNull()
        {
            Assert.Throws<ArgumentNullException>(() => Guard.NotNull<object>(null, "x"));
        }

        [Fact]
        public void NotNullOrEmpty_Throws_OnEmpty()
        {
            Assert.Throws<ArgumentException>(() => Guard.NotNullOrEmpty("", "x"));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void Positive_Throws_OnNonPositive(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Positive(value, "x"));
        }

        [Fact]
        public void Positive_Returns_OnValid()
        {
            Assert.Equal(3.5, Guard.Positive(3.5, "x"));
        }
    }
}
