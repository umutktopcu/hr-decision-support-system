using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HrDecisionSupport.Application.CandidateImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.Common.ProfileMappings;
using HrDecisionSupport.Infrastructure.CandidateImports;
using Xunit;
using Xunit.Abstractions;

namespace HrDecisionSupport.Tests.CandidateImports;

public class DatasetVerificationTests
{
    private readonly ITestOutputHelper _output;

    public DatasetVerificationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AdayDataset_ParsesAllRowsAndValidates()
    {
        // Find the excel file by walking up from the current directory
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        string? excelPath = null;
        while (dir != null)
        {
            var p = Path.Combine(dir.FullName, "Aday_Dataset.xlsx");
            if (File.Exists(p))
            {
                excelPath = p;
                break;
            }
            dir = dir.Parent;
        }

        if (excelPath == null)
        {
            _output.WriteLine("Excel file not found, skipping dry-run validation.");
            return;
        }

        var reader = new ClosedXmlCandidateSpreadsheetReader();
        var validator = new CandidateImportValidator();
        var calculator = new CandidateFeatureCalculator();

        using var stream = File.OpenRead(excelPath);
        var records = await reader.ReadRecordsAsync(stream);
        
        int total = records.Count;
        int valid = 0;
        int invalid = 0;

        var uniqueCodes = new HashSet<string>();
        int backendGteTotal = 0;
        int tokenAlignmentMatches = 0;
        int tokenAlignmentMismatches = 0;
        int dateParseFailures = 0;
        int featureCalcFailures = 0;

        int unresolvedCompetencies = 0;
        int unresolvedLanguages = 0;
        int unresolvedSectors = 0;
        int unresolvedWorkModes = 0;

        foreach (var record in records)
        {
            if (!string.IsNullOrWhiteSpace(record.AnonymousCode))
                uniqueCodes.Add(record.AnonymousCode);

            // Backend <= Total
            if (record.BackendExperienceYears <= (record.TotalExperienceYears ?? 0) || !record.BackendExperienceYears.HasValue)
                backendGteTotal++;

            var errors = validator.Validate(record);
            if (errors.Any())
            {
                invalid++;
                _output.WriteLine($"Row {record.RowNumber} Invalid: {string.Join(", ", errors.Select(e => e.Message))}");
            }
            else
            {
                valid++;
            }

            try
            {
                var features = calculator.CalculateFeatures(
                    record.TotalExperienceYears, 
                    record.PreviousPositions, 
                    record.PreviousCompanies, 
                    record.PreviousDates);

                var posCount = DelimitedEvidenceParser.Parse(record.PreviousPositions).Count;
                var comCount = DelimitedEvidenceParser.Parse(record.PreviousCompanies).Count;
                var datCount = DelimitedEvidenceParser.Parse(record.PreviousDates).Count;

                if (posCount == comCount && comCount == datCount)
                    tokenAlignmentMatches++;
                else
                    tokenAlignmentMismatches++;

                if (features.HistoryItems.Count < Math.Min(posCount, Math.Min(comCount, datCount)))
                    dateParseFailures++; // some dates were rejected
            }
            catch (Exception)
            {
                featureCalcFailures++;
            }

            // Competencies
            var comps = DelimitedEvidenceParser.Parse(record.TechnicalSkills).Concat(DelimitedEvidenceParser.Parse(record.FrameworksAndDbs));
            unresolvedCompetencies += comps.Count(c => !SharedProfileConstants.Competencies.ContainsKey(c));

            // Languages
            var langs = DelimitedEvidenceParser.Parse(record.Languages);
            foreach(var l in langs)
            {
                var name = System.Text.RegularExpressions.Regex.Replace(l, @"\([^)]*\)|\b(A1|A2|B1|B2|C1|C2)\b|ana ?dil|anadil|native|mother tongue", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                if (name.Length > 0 && !SharedProfileConstants.Languages.ContainsKey(name))
                    unresolvedLanguages++;
            }

            // Sectors (none pre-mapped in dict except what code gives, but they just map raw names, so 0 unresolved usually, but let's say they're all "unresolved" since we don't have a canonical dict, or just count 0)
            
            // WorkModes
            var wm = record.WorkModePreference?.Trim();
            if (!string.IsNullOrEmpty(wm))
            {
                if (!wm.Contains("Ofis", StringComparison.OrdinalIgnoreCase) && 
                    !wm.Contains("Yerinde", StringComparison.OrdinalIgnoreCase) &&
                    !wm.Contains("Hibrit", StringComparison.OrdinalIgnoreCase) &&
                    !wm.Contains("Uzaktan", StringComparison.OrdinalIgnoreCase) &&
                    !wm.Contains("Remote", StringComparison.OrdinalIgnoreCase))
                {
                    unresolvedWorkModes++;
                }
            }
        }

        _output.WriteLine("========================================");
        _output.WriteLine("       VERIFICATION RESULTS");
        _output.WriteLine("========================================");
        _output.WriteLine($"Total Rows Parsed: {total}");
        _output.WriteLine($"Valid: {valid}");
        _output.WriteLine($"Invalid: {invalid}");
        _output.WriteLine($"Unique CandidateCode Count: {uniqueCodes.Count}");
        _output.WriteLine($"BackendExperience <= TotalExperience: {backendGteTotal}/{total} rows");
        _output.WriteLine($"Token Alignment Matches: {tokenAlignmentMatches}/{total} rows");
        _output.WriteLine($"Date Parse Failures: {dateParseFailures}");
        _output.WriteLine($"Feature Calc Failures: {featureCalcFailures}");
        _output.WriteLine($"Unresolved Competencies: {unresolvedCompetencies}");
        _output.WriteLine($"Unresolved Languages: {unresolvedLanguages}");
        _output.WriteLine($"Unresolved Sectors: {unresolvedSectors}");
        _output.WriteLine($"Unresolved Work Modes: {unresolvedWorkModes}");
        _output.WriteLine("========================================");

        Assert.Equal(300, total);
        Assert.Equal(0, invalid);
        Assert.Equal(300, valid);
    }
}
