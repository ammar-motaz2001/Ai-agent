using System;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Logging;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Infrastructure.Excel
{
    /// <summary>
    /// Reads survey points from an .xlsx workbook using ClosedXML. Each configured column can be
    /// specified either as a header caption (matched against <see cref="ExcelSettings.HeaderRow"/>)
    /// or as a spreadsheet column letter (e.g. "E"). Malformed rows are skipped and reported as
    /// warnings rather than aborting the whole read.
    /// </summary>
    public sealed class ClosedXmlPointReader : IExcelPointReader
    {
        private readonly ILogger _logger;

        /// <summary>Creates the reader.</summary>
        public ClosedXmlPointReader(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <inheritdoc />
        public OperationResult<IReadOnlyList<SurveyPoint>> ReadPoints(string excelFilePath, ExcelSettings excelSettings)
        {
            // Optional input: no path means "no points", which is a success, not an error.
            if (string.IsNullOrWhiteSpace(excelFilePath))
                return OperationResult<IReadOnlyList<SurveyPoint>>.Ok(new List<SurveyPoint>(), "No Excel file provided.");

            if (!System.IO.File.Exists(excelFilePath))
                return OperationResult<IReadOnlyList<SurveyPoint>>.Fail(
                    "The Excel file was not found: " + excelFilePath);

            var settings = excelSettings ?? new ExcelSettings();
            var points = new List<SurveyPoint>();
            var warnings = new List<string>();

            try
            {
                using (var workbook = new XLWorkbook(excelFilePath))
                {
                    var worksheet = ResolveWorksheet(workbook, settings.SheetName);
                    if (worksheet == null)
                        return OperationResult<IReadOnlyList<SurveyPoint>>.Fail(
                            "The worksheet '" + settings.SheetName + "' was not found in the workbook.");

                    var used = worksheet.RangeUsed();
                    if (used == null)
                        return OperationResult<IReadOnlyList<SurveyPoint>>.Ok(points, "The worksheet was empty.");

                    int firstRow = used.FirstRow().RowNumber();
                    int lastRow = used.LastRow().RowNumber();

                    // Resolve each logical field to a physical 1-based column number.
                    int colEast = ResolveColumn(worksheet, settings.HeaderRow, settings.EastingColumn);
                    int colNorth = ResolveColumn(worksheet, settings.HeaderRow, settings.NorthingColumn);
                    int colElev = ResolveColumn(worksheet, settings.HeaderRow, settings.ElevationColumn);
                    int colNum = ResolveColumn(worksheet, settings.HeaderRow, settings.PointNumberColumn);
                    int colDesc = ResolveColumn(worksheet, settings.HeaderRow, settings.DescriptionColumn);

                    if (colEast <= 0 || colNorth <= 0 || colElev <= 0)
                        return OperationResult<IReadOnlyList<SurveyPoint>>.Fail(
                            "Could not locate the Easting/Northing/Elevation columns. Check the column " +
                            "mapping in appsettings.json (Excel section) against your file's headers.");

                    // Data starts after the header row (or at the first used row if no header).
                    int dataStart = settings.HeaderRow > 0 ? Math.Max(firstRow, settings.HeaderRow + 1) : firstRow;

                    for (int row = dataStart; row <= lastRow; row++)
                    {
                        var eCell = worksheet.Cell(row, colEast);
                        var nCell = worksheet.Cell(row, colNorth);
                        var zCell = worksheet.Cell(row, colElev);

                        // Skip fully blank rows silently.
                        if (eCell.IsEmpty() && nCell.IsEmpty() && zCell.IsEmpty())
                            continue;

                        if (!TryGetDouble(eCell, out double easting) ||
                            !TryGetDouble(nCell, out double northing) ||
                            !TryGetDouble(zCell, out double elevation))
                        {
                            warnings.Add($"Row {row}: could not parse numeric coordinates; row skipped.");
                            continue;
                        }

                        long pointNumber = 0;
                        if (colNum > 0)
                        {
                            TryGetLong(worksheet.Cell(row, colNum), out pointNumber);
                        }

                        string description = colDesc > 0
                            ? worksheet.Cell(row, colDesc).GetString()?.Trim() ?? ""
                            : "";

                        var point = new SurveyPoint(pointNumber, easting, northing, elevation, description);
                        if (point.IsValid)
                            points.Add(point);
                        else
                            warnings.Add($"Row {row}: coordinate values were not finite; row skipped.");
                    }
                }

                _logger.Info($"Read {points.Count} survey point(s) from {System.IO.Path.GetFileName(excelFilePath)} " +
                             $"({warnings.Count} row warning(s)).", "Excel");

                return OperationResult<IReadOnlyList<SurveyPoint>>.Ok(points,
                    $"Parsed {points.Count} point(s).", warnings);
            }
            catch (Exception ex)
            {
                return OperationResult<IReadOnlyList<SurveyPoint>>.Fail(
                    "Failed to read the Excel file. It may be open in another program, corrupt, or an " +
                    "unsupported format. Details: " + ex.Message, ex);
            }
        }

        /// <summary>Selects the worksheet by name, or the first sheet when no name is configured.</summary>
        private static IXLWorksheet ResolveWorksheet(XLWorkbook workbook, string sheetName)
        {
            if (string.IsNullOrWhiteSpace(sheetName))
                return workbook.Worksheets.Count > 0 ? workbook.Worksheet(1) : null;

            foreach (var ws in workbook.Worksheets)
            {
                if (string.Equals(ws.Name, sheetName, StringComparison.OrdinalIgnoreCase))
                    return ws;
            }
            return null;
        }

        /// <summary>
        /// Resolves a column specifier to a 1-based column index. First tries to match the header
        /// caption in <paramref name="headerRow"/>; if that fails, treats the specifier as a column
        /// letter. Returns 0 when it cannot be resolved.
        /// </summary>
        private static int ResolveColumn(IXLWorksheet worksheet, int headerRow, string specifier)
        {
            if (string.IsNullOrWhiteSpace(specifier)) return 0;
            specifier = specifier.Trim();

            // 1) Header-caption match.
            if (headerRow > 0)
            {
                var header = worksheet.Row(headerRow);
                var lastCol = worksheet.RangeUsed()?.LastColumn()?.ColumnNumber() ?? 0;
                for (int c = 1; c <= lastCol; c++)
                {
                    var caption = header.Cell(c).GetString()?.Trim();
                    if (!string.IsNullOrEmpty(caption) &&
                        string.Equals(caption, specifier, StringComparison.OrdinalIgnoreCase))
                    {
                        return c;
                    }
                }
            }

            // 2) Column-letter fallback (e.g. "E", "AB").
            if (IsColumnLetter(specifier))
            {
                try { return XLHelper.GetColumnNumberFromLetter(specifier.ToUpperInvariant()); }
                catch { return 0; }
            }

            return 0;
        }

        /// <summary>True when the string is only letters (a valid A1-style column reference).</summary>
        private static bool IsColumnLetter(string s)
        {
            foreach (var ch in s)
            {
                if (!char.IsLetter(ch)) return false;
            }
            return s.Length > 0 && s.Length <= 3;
        }

        /// <summary>Reads a cell as a double, accepting numeric cells and numeric-looking text.</summary>
        private static bool TryGetDouble(IXLCell cell, out double value)
        {
            value = 0;
            if (cell == null || cell.IsEmpty()) return false;

            if (cell.DataType == XLDataType.Number)
            {
                value = cell.GetDouble();
                return true;
            }

            var text = cell.GetString()?.Trim();
            return !string.IsNullOrEmpty(text) &&
                   double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>Reads a cell as a long, tolerating decimals and text.</summary>
        private static bool TryGetLong(IXLCell cell, out long value)
        {
            value = 0;
            if (TryGetDouble(cell, out double d))
            {
                value = (long)Math.Round(d);
                return true;
            }
            return false;
        }
    }
}
