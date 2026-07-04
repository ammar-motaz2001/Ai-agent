using System;
using System.IO;
using ClosedXML.Excel;
using Civil3DAIAgent.Core.Abstractions;
using Civil3DAIAgent.Infrastructure.Configuration;
using Civil3DAIAgent.Infrastructure.Excel;
using Civil3DAIAgent.Infrastructure.IO;
using Civil3DAIAgent.Infrastructure.Validation;
using Civil3DAIAgent.Models.Configuration;
using Civil3DAIAgent.Models.Enums;
using Civil3DAIAgent.Models.Results;
using Civil3DAIAgent.Models.Workflow;
using Xunit;

namespace Civil3DAIAgent.Tests.Infrastructure
{
    public class JsonConfigurationProviderTests
    {
        [Fact]
        public void Loads_Values_And_EnumFromString()
        {
            string path = TempFile(".json");
            File.WriteAllText(path, @"{ ""Extraction"": { ""SegmentLengthMeters"": 1500 }, ""Logging"": { ""MinimumLevel"": ""Warning"" } }");
            try
            {
                var provider = new JsonConfigurationProvider(null, path);
                Assert.Equal(1500, provider.Settings.Extraction.SegmentLengthMeters);
                Assert.Equal(LogLevel.Warning, provider.Settings.Logging.MinimumLevel);
                // Unspecified values keep their defaults.
                Assert.Equal("AI-Alignment-01", provider.Settings.Alignment.Name);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void MissingFile_YieldsDefaults_NoThrow()
        {
            var provider = new JsonConfigurationProvider(null, Path.Combine(Path.GetTempPath(), "does_not_exist.json"));
            Assert.NotNull(provider.Settings);
            Assert.Equal(3000, provider.Settings.Extraction.SegmentLengthMeters);
        }

        [Fact]
        public void MalformedJson_FallsBackToDefaults()
        {
            string path = TempFile(".json");
            File.WriteAllText(path, "{ this is not valid json ");
            try
            {
                var provider = new JsonConfigurationProvider(null, path);
                Assert.NotNull(provider.Settings);
            }
            finally { File.Delete(path); }
        }

        private static string TempFile(string ext) => Path.Combine(Path.GetTempPath(), "c3dai_" + Guid.NewGuid().ToString("N") + ext);
    }

    public class FileServiceTests
    {
        [Fact]
        public void EnsureDirectory_CreatesFolder()
        {
            var fs = new FileService();
            string dir = Path.Combine(Path.GetTempPath(), "c3dai_" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = fs.EnsureDirectory(dir);
                Assert.True(result.Succeeded);
                Assert.True(Directory.Exists(dir));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir); }
        }

        [Fact]
        public void GetUniquePath_AvoidsExistingFile()
        {
            var fs = new FileService();
            string dir = Path.Combine(Path.GetTempPath(), "c3dai_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string first = Path.Combine(dir, "out.dwg");
                File.WriteAllText(first, "x");
                string unique = fs.GetUniquePath(dir, "out.dwg");
                Assert.Equal(Path.Combine(dir, "out (1).dwg"), unique);
            }
            finally { Directory.Delete(dir, true); }
        }
    }

    public class InputValidatorTests
    {
        [Fact]
        public void Fails_WhenDwgMissing()
        {
            var validator = new InputValidator(new FakeFileService(fileExists: false, dirOk: true));
            var result = validator.Validate(new WorkflowRequest { InputDwgPath = "x.dwg", OutputFolder = "out" });
            Assert.True(result.Failed);
        }

        [Fact]
        public void Succeeds_WithValidInputs_AndWarnsOnMissingExcel()
        {
            var validator = new InputValidator(new FakeFileService(fileExists: true, dirOk: true));
            var result = validator.Validate(new WorkflowRequest { InputDwgPath = "x.dwg", OutputFolder = "out" });
            Assert.True(result.Succeeded);
            Assert.Contains(result.Warnings, w => w.Contains("Excel"));
        }

        [Fact]
        public void Fails_WhenOutputNotCreatable()
        {
            var validator = new InputValidator(new FakeFileService(fileExists: true, dirOk: false));
            var result = validator.Validate(new WorkflowRequest { InputDwgPath = "x.dwg", OutputFolder = "out" });
            Assert.True(result.Failed);
        }

        private sealed class FakeFileService : IFileService
        {
            private readonly bool _fileExists;
            private readonly bool _dirOk;
            public FakeFileService(bool fileExists, bool dirOk) { _fileExists = fileExists; _dirOk = dirOk; }
            public bool FileExists(string path) => _fileExists;
            public bool DirectoryExists(string path) => _dirOk;
            public OperationResult EnsureDirectory(string path) => _dirOk ? OperationResult.Ok() : OperationResult.Fail("cannot create");
            public string GetUniquePath(string folder, string fileName) => Path.Combine(folder, fileName);
            public OperationResult<int> PurgeOldLogs(string logFolder, int retainDays) => OperationResult<int>.Ok(0);
        }
    }

    public class ClosedXmlPointReaderTests
    {
        [Fact]
        public void ReadsPoints_FromHeaderedWorkbook()
        {
            string path = Path.Combine(Path.GetTempPath(), "c3dai_" + Guid.NewGuid().ToString("N") + ".xlsx");
            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Points");
                // Headers match POINTS.xlsx: POINT, EASTING, NORTHING, ELEVATION.
                ws.Cell(1, 1).Value = "POINT"; ws.Cell(1, 2).Value = "EASTING";
                ws.Cell(1, 3).Value = "NORTHING"; ws.Cell(1, 4).Value = "ELEVATION";
                ws.Cell(2, 1).Value = 1; ws.Cell(2, 2).Value = 100.5; ws.Cell(2, 3).Value = 200.5; ws.Cell(2, 4).Value = 12.3;
                ws.Cell(3, 1).Value = 2; ws.Cell(3, 2).Value = 101.0; ws.Cell(3, 3).Value = 201.0; ws.Cell(3, 4).Value = 12.6;
                wb.SaveAs(path);
            }

            try
            {
                var reader = new ClosedXmlPointReader();
                var result = reader.ReadPoints(path, new ExcelSettings());
                Assert.True(result.Succeeded);
                Assert.Equal(2, result.Value.Count);
                Assert.Equal(100.5, result.Value[0].Easting, 3);
                Assert.Equal(200.5, result.Value[0].Northing, 3);
                Assert.Equal(12.3, result.Value[0].Elevation, 3);
                Assert.Equal(1, result.Value[0].PointNumber);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void EmptyPath_IsSuccess_WithNoPoints()
        {
            var reader = new ClosedXmlPointReader();
            var result = reader.ReadPoints("", new ExcelSettings());
            Assert.True(result.Succeeded);
            Assert.Empty(result.Value);
        }
    }
}
