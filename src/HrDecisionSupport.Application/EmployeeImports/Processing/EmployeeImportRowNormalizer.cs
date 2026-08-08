using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

using HrDecisionSupport.Application.Common.ProfileMappings;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed class EmployeeImportRowNormalizer : IEmployeeImportRowNormalizer
{


    public EmployeeImportNormalizedRow Normalize(EmployeeImportSourceRow source)
    {
        var diagnostics = source.Diagnostics.ToList();
        int? Duration(decimal? years, string property) { var value = DurationToMonthsParser.Parse(years); if (!value.IsValid) diagnostics.Add(Diagnostic("invalid_duration", property, source.SourceRowNumber, EmployeeImportDiagnosticSeverity.Error)); return value.Months; }
        var totalMonths = Duration(source.TotalExperienceYears, "TotalExperience"); var backendMonths = Duration(source.BackendExperienceYears, "BackendExperience");
        var sectors = ParseSectors(source.SectorExperienceRaw, source.SourceRowNumber, diagnostics);
        var languages = ParseLanguages(source.ForeignLanguagesRaw, source.SourceRowNumber, diagnostics);
        var education = MapEducation(source.EducationLevelRaw, source.SourceRowNumber, diagnostics);
        return new(source.SourceRowNumber, source.AnonymousEmployeeCode, source.CurrentPosition, source.TotalExperienceYears, totalMonths, source.BackendExperienceYears, backendMonths,
            DelimitedEvidenceParser.Parse(source.PreviousPositionsRaw), ParseCompetencies(source.TechnicalSkillsRaw, source.TechnologiesAndToolsRaw, source.SourceRowNumber, diagnostics), DelimitedEvidenceParser.Parse(source.ProjectExperiencesRaw), sectors, education, Trim(source.EducationFieldRaw), ParseCertificates(source.CertificatesRaw), languages, ParseWorkModes(source.WorkModeExperienceRaw, source.SourceRowNumber, diagnostics), source.HireDate, source.TerminationDate, Duration(source.CompanyTenureYears, "CompanyTenure"), source.PreviousCompanyAverageStayMonths, source.ShortestPreviousJobMonths, source.LongestPreviousJobMonths, source.LastPreviousCompanyStayMonths, source.CompanyChangeCount, source.JobChangeRate, ParseLabel(source.StayLabel, source.RawValues, source.SourceRowNumber, diagnostics), source.RawValues, diagnostics.AsReadOnly());
    }
    public EmployeeImportCompetencyNormalization NormalizeCompetencies(string? skills, string? tools)
    {
        var tokens = DelimitedEvidenceParser.Parse(skills).Concat(DelimitedEvidenceParser.Parse(tools)).ToArray();
        var unresolved = tokens.Where(token => !SharedProfileConstants.Competencies.ContainsKey(token)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var occurrences = tokens.Where(SharedProfileConstants.Competencies.ContainsKey).Select(token => SharedProfileConstants.Competencies[token]).Select(item => new NormalizedCompetencyItem(SharedProfileConstants.Code(item.Name), item.Name, item.Category)).ToArray();
        var resolved = occurrences.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToArray();
        return new(tokens, occurrences, resolved, unresolved);
    }
    private IReadOnlyList<NormalizedCompetencyItem> ParseCompetencies(string? skills, string? tools, int row, ICollection<EmployeeImportDiagnostic> d) { var result = NormalizeCompetencies(skills, tools); foreach (var token in result.UnresolvedTokens) d.Add(Diagnostic("unknown_competency", token, row, EmployeeImportDiagnosticSeverity.Warning)); return result.ResolvedCompetencies; }
    private static IReadOnlyList<NormalizedSectorExperience> ParseSectors(string? raw, int row, ICollection<EmployeeImportDiagnostic> d)
    { var result = new List<NormalizedSectorExperience>(); foreach (var token in DelimitedEvidenceParser.Parse(raw)) { var m = Regex.Match(token, @"^(?<n>.*?)(?:\s*\((?<d>[^)]+)\))?$"); var name = m.Groups["n"].Value.Trim(); var duration = m.Groups["d"].Success ? DurationToMonthsParser.Parse(m.Groups["d"].Value) : new(null, true); if (!duration.IsValid) d.Add(Diagnostic("invalid_sector_duration", name, row, EmployeeImportDiagnosticSeverity.Warning)); var i = result.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); var item = new NormalizedSectorExperience(SharedProfileConstants.Code(name), name, duration.Months); if (i < 0) result.Add(item); else if (result[i].ExperienceMonths is null && item.ExperienceMonths is not null) result[i] = item; } return result; }
    private static DegreeLevel? MapEducation(string? raw, int row, ICollection<EmployeeImportDiagnostic> d) { var value = Trim(raw); if (value is null) return null; if (SharedProfileConstants.Degrees.TryGetValue(value, out var degree)) return degree; d.Add(Diagnostic("unknown_education_level", "EducationLevel", row, EmployeeImportDiagnosticSeverity.Warning)); return null; }
    private static IReadOnlyList<string> ParseCertificates(string? raw) => DelimitedEvidenceParser.Parse(raw).Where(x => !SharedProfileConstants.CertificateSentinels.Contains(x)).ToArray();
    private static IReadOnlyList<NormalizedLanguageExperience> ParseLanguages(string? raw, int row, ICollection<EmployeeImportDiagnostic> d)
    { var result = new List<NormalizedLanguageExperience>(); foreach (var token in DelimitedEvidenceParser.Parse(raw)) { var native = Regex.IsMatch(token, @"ana ?dil|anadil|native|mother tongue", RegexOptions.IgnoreCase); var levelMatch = Regex.Match(token, @"\b(A1|A2|B1|B2|C1|C2)\b", RegexOptions.IgnoreCase); var level = levelMatch.Success ? Enum.Parse<LanguageProficiencyLevel>(levelMatch.Value, true) : (LanguageProficiencyLevel?)null; var name = Regex.Replace(token, @"\([^)]*\)|\b(A1|A2|B1|B2|C1|C2)\b|ana ?dil|anadil|native|mother tongue", "", RegexOptions.IgnoreCase).Trim(); if (name.Length == 0) continue; if (!native && !level.HasValue) d.Add(Diagnostic("language_level_missing", name, row, EmployeeImportDiagnosticSeverity.Warning)); var canonical = SharedProfileConstants.Languages.TryGetValue(name, out var l) ? l : ("LANG_" + SharedProfileConstants.Hash(name), name); var existing = result.FindIndex(x => x.Code == canonical.Item1); var item = new NormalizedLanguageExperience(canonical.Item1, canonical.Item2, level, native); if (existing < 0) result.Add(item); else { var prior = result[existing]; if (prior.ProficiencyLevel.HasValue && level.HasValue && prior.ProficiencyLevel != level) d.Add(Diagnostic("conflicting_language_level", name, row, EmployeeImportDiagnosticSeverity.Warning)); result[existing] = prior with { IsNative = prior.IsNative || native, ProficiencyLevel = prior.ProficiencyLevel ?? level }; } } return result; }
    private static IReadOnlyList<NormalizedWorkModeExperience> ParseWorkModes(string? raw, int row, ICollection<EmployeeImportDiagnostic> d) { var text = Trim(raw); if (text is null) return []; var list = new List<NormalizedWorkModeExperience>(); void Add(string code, string name) => list.Add(new(code, name, null)); if (Contains(text, "Ofis") || Contains(text, "Yerinde")) Add("ON_SITE", "On-site"); if (Contains(text, "Hibrit")) Add("HYBRID", "Hybrid"); if (Contains(text, "Uzaktan") || Contains(text, "Remote")) Add("REMOTE", "Remote"); if (list.Count == 0) d.Add(Diagnostic("unknown_work_mode", "WorkMode", row, EmployeeImportDiagnosticSeverity.Warning)); return list; }
    private static EmployeeRetentionLabelValue? ParseLabel(int? typed, IReadOnlyDictionary<string, string?> raw, int row, ICollection<EmployeeImportDiagnostic> d) { raw.TryGetValue(EmployeeImportSpreadsheetHeaders.StayLabel, out var rawLabel); var value = typed?.ToString() ?? Trim(rawLabel); var map = new Dictionary<string, EmployeeRetentionLabelValue>(StringComparer.OrdinalIgnoreCase) { { "0", EmployeeRetentionLabelValue.Short }, { "Short", EmployeeRetentionLabelValue.Short }, { "Kısa", EmployeeRetentionLabelValue.Short }, { "1", EmployeeRetentionLabelValue.Normal }, { "Normal", EmployeeRetentionLabelValue.Normal }, { "2", EmployeeRetentionLabelValue.Long }, { "Long", EmployeeRetentionLabelValue.Long }, { "Uzun", EmployeeRetentionLabelValue.Long } }; if (value is null) return null; if (map.TryGetValue(value, out var label)) return label; d.Add(Diagnostic("invalid_stay_label", "StayLabel", row, EmployeeImportDiagnosticSeverity.Error)); return null; }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim(); private static bool Contains(string x, string y) => x.Contains(y, StringComparison.OrdinalIgnoreCase); private static EmployeeImportDiagnostic Diagnostic(string code, string property, int row, EmployeeImportDiagnosticSeverity severity) => new(code, property, $"{property} could not be normalized.", severity, row);
}

public sealed record EmployeeImportCompetencyNormalization(IReadOnlyList<string> SourceTokens, IReadOnlyList<NormalizedCompetencyItem> ResolvedTokenOccurrences, IReadOnlyList<NormalizedCompetencyItem> ResolvedCompetencies, IReadOnlyList<string> UnresolvedTokens);
