using System.Collections.Generic;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Points;
using Civil3DAIAgent.Models.Results;

namespace Civil3DAIAgent.Core.Abstractions
{
    /// <summary>
    /// Reads survey / COGO points from an Excel workbook. Implemented in the Infrastructure layer.
    /// Because the Excel file is optional, callers must handle an empty result gracefully.
    /// </summary>
    public interface IExcelPointReader
    {
        /// <summary>
        /// Parses <paramref name="excelFilePath"/> into a list of <see cref="SurveyPoint"/> using the
        /// column mapping in <paramref name="excelSettings"/>. Malformed rows are skipped (and reported
        /// as warnings), never thrown. Returns an empty list (success) when the path is empty.
        /// </summary>
        /// <param name="excelFilePath">Full path to the .xlsx/.xls file, or empty to skip.</param>
        /// <param name="excelSettings">Sheet/header/column mapping options.</param>
        /// <returns>A result carrying the parsed points and any per-row warnings.</returns>
        OperationResult<IReadOnlyList<SurveyPoint>> ReadPoints(string excelFilePath, ExcelSettings excelSettings);
    }
}
