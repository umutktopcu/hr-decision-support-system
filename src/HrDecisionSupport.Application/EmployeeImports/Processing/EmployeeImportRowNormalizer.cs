using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed class EmployeeImportRowNormalizer : IEmployeeImportRowNormalizer
{
    private static readonly IReadOnlyDictionary<string, string> CanonicalCompetencyCodeOverrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["C#"] = "C_SHARP", ["C++"] = "CPP", [".NET"] = "DOTNET", ["ASP.NET Core"] = "ASP_NET_CORE", ["Vue.js"] = "VUE_JS", ["Node.js"] = "NODE_JS", ["CI/CD"] = "CI_CD" };
    private static readonly Dictionary<string, (string Name, CompetencyCategory Category)> Competencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = ("C", CompetencyCategory.ProgrammingLanguage),
        ["C#"] = ("C#", CompetencyCategory.ProgrammingLanguage),
        ["C Sharp"] = ("C#", CompetencyCategory.ProgrammingLanguage),
        ["Java"] = ("Java", CompetencyCategory.ProgrammingLanguage),
        ["Python"] = ("Python", CompetencyCategory.ProgrammingLanguage),
        ["JavaScript"] = ("JavaScript", CompetencyCategory.ProgrammingLanguage),
        ["JS"] = ("JavaScript", CompetencyCategory.ProgrammingLanguage),
        ["TypeScript"] = ("TypeScript", CompetencyCategory.ProgrammingLanguage),
        ["TS"] = ("TypeScript", CompetencyCategory.ProgrammingLanguage),
        ["PHP"] = ("PHP", CompetencyCategory.ProgrammingLanguage),
        ["Go"] = ("Go", CompetencyCategory.ProgrammingLanguage),
        ["C++"] = ("C++", CompetencyCategory.ProgrammingLanguage),
        ["Kotlin"] = ("Kotlin", CompetencyCategory.ProgrammingLanguage),
        ["ASP.NET Core"] = ("ASP.NET Core", CompetencyCategory.Framework),
        ["ASP.NET"] = ("ASP.NET Core", CompetencyCategory.Framework),
        [".NET"] = (".NET", CompetencyCategory.Framework),
        ["Spring Boot"] = ("Spring Boot", CompetencyCategory.Framework),
        ["Django"] = ("Django", CompetencyCategory.Framework),
        ["Flask"] = ("Flask", CompetencyCategory.Framework),
        ["React"] = ("React", CompetencyCategory.Framework),
        ["Angular"] = ("Angular", CompetencyCategory.Framework),
        ["Vue.js"] = ("Vue.js", CompetencyCategory.Framework),
        ["Node.js"] = ("Node.js", CompetencyCategory.Framework),
        ["PostgreSQL"] = ("PostgreSQL", CompetencyCategory.Database),
        ["Postgres"] = ("PostgreSQL", CompetencyCategory.Database),
        ["SQL Server"] = ("SQL Server", CompetencyCategory.Database),
        ["MSSQL"] = ("SQL Server", CompetencyCategory.Database),
        ["MySQL"] = ("MySQL", CompetencyCategory.Database),
        ["Oracle"] = ("Oracle", CompetencyCategory.Database),
        ["MongoDB"] = ("MongoDB", CompetencyCategory.Database),
        ["Redis"] = ("Redis", CompetencyCategory.Database),
        ["Git"] = ("Git", CompetencyCategory.Tool),
        ["Docker"] = ("Docker", CompetencyCategory.Tool),
        ["Kubernetes"] = ("Kubernetes", CompetencyCategory.Tool),
        ["K8s"] = ("Kubernetes", CompetencyCategory.Tool),
        ["Kafka"] = ("Kafka", CompetencyCategory.Tool),
        ["RabbitMQ"] = ("RabbitMQ", CompetencyCategory.Tool),
        ["Jenkins"] = ("Jenkins", CompetencyCategory.Tool),
        ["Azure DevOps"] = ("Azure DevOps", CompetencyCategory.Tool),
        ["Jira"] = ("Jira", CompetencyCategory.Tool),
        ["Mikroservis mimarisi"] = ("Mikroservis mimarisi", CompetencyCategory.TechnicalConcept),
        ["Nesne yönelimli programlama"] = ("Nesne yönelimli programlama", CompetencyCategory.TechnicalConcept),
        ["REST API"] = ("REST API", CompetencyCategory.TechnicalConcept),
        ["Mesajlaşma sistemleri"] = ("Mesajlaşma sistemleri", CompetencyCategory.TechnicalConcept),
        ["Event-driven architecture"] = ("Event-driven architecture", CompetencyCategory.TechnicalConcept),
        ["CI/CD"] = ("CI/CD", CompetencyCategory.TechnicalConcept)
    };
    private static readonly Dictionary<string, DegreeLevel> Degrees = new(StringComparer.OrdinalIgnoreCase) { ["Lise"] = DegreeLevel.HighSchool, ["Ön Lisans"] = DegreeLevel.Associate, ["Lisans"] = DegreeLevel.Bachelor, ["Yüksek Lisans"] = DegreeLevel.Master, ["Doktora"] = DegreeLevel.Doctorate, ["Diğer"] = DegreeLevel.Other, ["Other"] = DegreeLevel.Other };
    private static readonly HashSet<string> CertificateSentinels = new(StringComparer.OrdinalIgnoreCase) { "Yok", "Sertifika yok", "Bulunmuyor", "None", "No certificate" };
    private static readonly Dictionary<string, (string Code, string Name)> Languages = new(StringComparer.OrdinalIgnoreCase) { ["Türkçe"] = ("TR", "Türkçe"), ["English"] = ("EN", "English"), ["İngilizce"] = ("EN", "İngilizce"), ["Almanca"] = ("DE", "Almanca"), ["Fransızca"] = ("FR", "Fransızca"), ["İspanyolca"] = ("ES", "İspanyolca"), ["İtalyanca"] = ("IT", "İtalyanca") };

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
    private static IReadOnlyList<NormalizedCompetencyItem> ParseCompetencies(string? skills, string? tools, int row, ICollection<EmployeeImportDiagnostic> d) { var tokens = DelimitedEvidenceParser.Parse(skills).Concat(DelimitedEvidenceParser.Parse(tools)).ToArray(); foreach (var token in tokens.Where(x => !Competencies.ContainsKey(x))) d.Add(Diagnostic("unknown_competency", token, row, EmployeeImportDiagnosticSeverity.Warning)); return tokens.Where(Competencies.ContainsKey).Select(x => Competencies[x]).GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Select(x => new NormalizedCompetencyItem(Code(x.Key), x.Key, x.First().Category)).ToArray(); }
    private static IReadOnlyList<NormalizedSectorExperience> ParseSectors(string? raw, int row, ICollection<EmployeeImportDiagnostic> d)
    { var result = new List<NormalizedSectorExperience>(); foreach (var token in DelimitedEvidenceParser.Parse(raw)) { var m = Regex.Match(token, @"^(?<n>.*?)(?:\s*\((?<d>[^)]+)\))?$"); var name = m.Groups["n"].Value.Trim(); var duration = m.Groups["d"].Success ? DurationToMonthsParser.Parse(m.Groups["d"].Value) : new(null, true); if (!duration.IsValid) d.Add(Diagnostic("invalid_sector_duration", name, row, EmployeeImportDiagnosticSeverity.Warning)); var i = result.FindIndex(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)); var item = new NormalizedSectorExperience(Code(name), name, duration.Months); if (i < 0) result.Add(item); else if (result[i].ExperienceMonths is null && item.ExperienceMonths is not null) result[i] = item; } return result; }
    private static DegreeLevel? MapEducation(string? raw, int row, ICollection<EmployeeImportDiagnostic> d) { var value = Trim(raw); if (value is null) return null; if (Degrees.TryGetValue(value, out var degree)) return degree; d.Add(Diagnostic("unknown_education_level", "EducationLevel", row, EmployeeImportDiagnosticSeverity.Warning)); return null; }
    private static IReadOnlyList<string> ParseCertificates(string? raw) => DelimitedEvidenceParser.Parse(raw).Where(x => !CertificateSentinels.Contains(x)).ToArray();
    private static IReadOnlyList<NormalizedLanguageExperience> ParseLanguages(string? raw, int row, ICollection<EmployeeImportDiagnostic> d)
    { var result = new List<NormalizedLanguageExperience>(); foreach (var token in DelimitedEvidenceParser.Parse(raw)) { var native = Regex.IsMatch(token, @"ana ?dil|anadil|native|mother tongue", RegexOptions.IgnoreCase); var levelMatch = Regex.Match(token, @"\b(A1|A2|B1|B2|C1|C2)\b", RegexOptions.IgnoreCase); var level = levelMatch.Success ? Enum.Parse<LanguageProficiencyLevel>(levelMatch.Value, true) : (LanguageProficiencyLevel?)null; var name = Regex.Replace(token, @"\([^)]*\)|\b(A1|A2|B1|B2|C1|C2)\b|ana ?dil|anadil|native|mother tongue", "", RegexOptions.IgnoreCase).Trim(); if (name.Length == 0) continue; if (!native && !level.HasValue) d.Add(Diagnostic("language_level_missing", name, row, EmployeeImportDiagnosticSeverity.Warning)); var canonical = Languages.TryGetValue(name, out var l) ? l : ("LANG_" + Hash(name), name); var existing = result.FindIndex(x => x.Code == canonical.Item1); var item = new NormalizedLanguageExperience(canonical.Item1, canonical.Item2, level, native); if (existing < 0) result.Add(item); else { var prior = result[existing]; if (prior.ProficiencyLevel.HasValue && level.HasValue && prior.ProficiencyLevel != level) d.Add(Diagnostic("conflicting_language_level", name, row, EmployeeImportDiagnosticSeverity.Warning)); result[existing] = prior with { IsNative = prior.IsNative || native, ProficiencyLevel = prior.ProficiencyLevel ?? level }; } } return result; }
    private static IReadOnlyList<NormalizedWorkModeExperience> ParseWorkModes(string? raw, int row, ICollection<EmployeeImportDiagnostic> d) { var text = Trim(raw); if (text is null) return []; var list = new List<NormalizedWorkModeExperience>(); void Add(string code, string name) => list.Add(new(code, name, null)); if (Contains(text, "Ofis") || Contains(text, "Yerinde")) Add("ON_SITE", "On-site"); if (Contains(text, "Hibrit")) Add("HYBRID", "Hybrid"); if (Contains(text, "Uzaktan") || Contains(text, "Remote")) Add("REMOTE", "Remote"); if (list.Count == 0) d.Add(Diagnostic("unknown_work_mode", "WorkMode", row, EmployeeImportDiagnosticSeverity.Warning)); return list; }
    private static EmployeeRetentionLabelValue? ParseLabel(int? typed, IReadOnlyDictionary<string, string?> raw, int row, ICollection<EmployeeImportDiagnostic> d) { raw.TryGetValue(EmployeeImportSpreadsheetHeaders.StayLabel, out var rawLabel); var value = typed?.ToString() ?? Trim(rawLabel); var map = new Dictionary<string, EmployeeRetentionLabelValue>(StringComparer.OrdinalIgnoreCase) { { "0", EmployeeRetentionLabelValue.Short }, { "Short", EmployeeRetentionLabelValue.Short }, { "Kısa", EmployeeRetentionLabelValue.Short }, { "1", EmployeeRetentionLabelValue.Normal }, { "Normal", EmployeeRetentionLabelValue.Normal }, { "2", EmployeeRetentionLabelValue.Long }, { "Long", EmployeeRetentionLabelValue.Long }, { "Uzun", EmployeeRetentionLabelValue.Long } }; if (value is null) return null; if (map.TryGetValue(value, out var label)) return label; d.Add(Diagnostic("invalid_stay_label", "StayLabel", row, EmployeeImportDiagnosticSeverity.Error)); return null; }
    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim(); private static bool Contains(string x, string y) => x.Contains(y, StringComparison.OrdinalIgnoreCase); private static string Code(string name) => CanonicalCompetencyCodeOverrides.TryGetValue(name, out var code) ? code : Regex.Replace(name.Normalize().ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_'); private static string Hash(string x) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(x)))[..8]; private static EmployeeImportDiagnostic Diagnostic(string code, string property, int row, EmployeeImportDiagnosticSeverity severity) => new(code, property, $"{property} could not be normalized.", severity, row);
}
