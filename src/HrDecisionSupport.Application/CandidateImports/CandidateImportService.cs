using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.ProfileMappings;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.CandidateImports.Models;
using HrDecisionSupport.Application.CandidateImports.Processing;
using HrDecisionSupport.Application.CandidateImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.CandidateImports;

public sealed class CandidateImportService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly ICandidateImportSourceReader _reader;
    private readonly TimeProvider _timeProvider;

    public CandidateImportService(
        IHrDecisionSupportDbContext dbContext,
        ICandidateImportSourceReader reader,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _reader = reader;
        _timeProvider = timeProvider;
    }

    public async Task<CandidateImportResult> ImportAsync(
        System.IO.Stream sourceStream,
        CancellationToken cancellationToken = default)
    {
        // 1. Check Job Requisition
        var requisitionId = Guid.Parse("0eeb3ac1-6e82-4251-ba70-2e0ebf46ed87");
        var requisition = await _dbContext.JobRequisitions
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == requisitionId, cancellationToken);
            
        if (requisition == null || (requisition.JobRequisitionStatus != JobRequisitionStatus.Open && requisition.JobRequisitionStatus != JobRequisitionStatus.OnHold))
        {
            return new CandidateImportResult(0, 0, 0, new[] { new CandidateImportError(0, "JobRequisition", "Geçerli bir Open veya OnHold JobRequisition bulunamadı.") });
        }

        // 2. Read from Excel
        var records = await _reader.ReadRecordsAsync(sourceStream, cancellationToken);
        
        // 3. Existing candidates
        var anonymousCodes = records.Select(r => r.AnonymousCode).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
        var existingCodes = await _dbContext.Candidates
            .AsNoTracking()
            .Where(c => anonymousCodes.Contains(c.CandidateCode))
            .Select(c => c.CandidateCode)
            .ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var validator = new CandidateImportValidator();
        var calculator = new CandidateFeatureCalculator();

        var errors = new List<CandidateImportError>();
        int inserted = 0;
        int skipped = 0;
        int failed = 0;

        // Process entities in memory
        var entitiesToAdd = new List<Person>();
        
        var compDict = new Dictionary<string, Competency>(StringComparer.OrdinalIgnoreCase);
        var projDict = new Dictionary<string, Project>(StringComparer.OrdinalIgnoreCase);
        var sectorDict = new Dictionary<string, Sector>(StringComparer.OrdinalIgnoreCase);
        var certDict = new Dictionary<string, Certificate>(StringComparer.OrdinalIgnoreCase);
        var langDict = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
        var wmDict = new Dictionary<string, WorkMode>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.AnonymousCode))
            {
                errors.Add(new CandidateImportError(record.RowNumber, "AnonymousCode", "Aday kodu boş olamaz."));
                failed++;
                continue;
            }

            if (existingSet.Contains(record.AnonymousCode))
            {
                skipped++;
                continue;
            }

            var recordErrors = validator.Validate(record);
            if (recordErrors.Any())
            {
                errors.AddRange(recordErrors);
                failed++;
                continue;
            }

            // --- Features ---
            var features = calculator.CalculateFeatures(
                record.TotalExperienceYears, 
                record.PreviousPositions, 
                record.PreviousCompanies, 
                record.PreviousDates);

            var personId = Guid.NewGuid();
            var candidateId = Guid.NewGuid();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            var person = new Person
            {
                Id = personId,
                AnonymousCode = record.AnonymousCode,
                Email = $"{record.AnonymousCode}@candidate.local", // Dummy email for constraint
                FirstName = "Aday",
                LastName = record.AnonymousCode,
                CreatedAtUtc = now,
                Candidate = new Candidate
                {
                    Id = candidateId,
                    PersonId = personId,
                    CandidateCode = record.AnonymousCode,
                    CandidateSource = CandidateSource.Excel,
                    ProfessionalTitle = record.ProfessionalTitle,
                    AvailabilityDays = record.AvailabilityDays,
                    WorkModePreferences = ParseWorkModesStr(record.WorkModePreference).Select(wmCode => {
                        if (!wmDict.TryGetValue(wmCode, out var wm)) {
                            wm = new WorkMode { Id = Guid.NewGuid(), Code = wmCode, Name = wmCode == "ON_SITE" ? "On-site" : (wmCode == "HYBRID" ? "Hybrid" : "Remote") };
                            wmDict[wmCode] = wm;
                        }
                        return new CandidateWorkModePreference { WorkMode = wm };
                    }).ToList(),
                    CareerFeatureSnapshots = new List<CandidateCareerFeatureSnapshot>
                    {
                        new CandidateCareerFeatureSnapshot
                        {
                            Id = Guid.NewGuid(),
                            CandidateId = candidateId,
                            FeatureSchemaVersion = "1.0",
                            TotalExperienceMonths = record.TotalExperienceYears.HasValue ? (int)(record.TotalExperienceYears.Value * 12) : 0,
                            BackendExperienceMonths = record.BackendExperienceYears.HasValue ? (int)(record.BackendExperienceYears.Value * 12) : 0,
                            CompanyChangeCount = features.CompanyChangeCount,
                            PreviousCompanyAverageStayMonths = features.PreviousCompanyAverageStayMonths,
                            ShortestPreviousJobMonths = features.ShortestPreviousJobMonths,
                            LongestPreviousJobMonths = features.LongestPreviousJobMonths,
                            LastPreviousCompanyStayMonths = features.LastPreviousCompanyStayMonths,
                            JobChangeRate = features.JobChangeRate,
                            CalculatedAtUtc = now
                        }
                    },
                    EvaluationCases = new List<CandidateEvaluationCase>
                    {
                        new CandidateEvaluationCase
                        {
                            Id = Guid.NewGuid(),
                            CandidateId = candidateId,
                            JobRequisitionId = requisitionId,
                            Status = CandidateEvaluationStatus.New,
                            ReceivedAtUtc = now,
                            CreatedAtUtc = now
                        }
                    }
                },
                EmploymentHistories = features.HistoryItems.Select(h => new EmploymentHistory
                {
                    Id = Guid.NewGuid(),
                    PersonId = personId,
                    PositionTitle = h.PositionName,
                    EmployerName = h.CompanyName,
                    StartDate = h.StartDate,
                    EndDate = h.EndDate
                }).ToList(),
                PersonCompetencies = ParseCompetencies(record.TechnicalSkills, record.FrameworksAndDbs).Select(c => {
                    if (!compDict.TryGetValue(c.Code, out var comp)) {
                        comp = new Competency { Id = Guid.NewGuid(), Code = c.Code, Name = c.Name, CompetencyCategory = c.Category };
                        compDict[c.Code] = comp;
                    }
                    return new PersonCompetency { Id = Guid.NewGuid(), PersonId = personId, Competency = comp };
                }).ToList(),
                PersonProjects = DelimitedEvidenceParser.Parse(record.Projects).Select(p => {
                    var code = SharedProfileConstants.Hash(p);
                    if (!projDict.TryGetValue(code, out var proj)) {
                        proj = new Project { Id = Guid.NewGuid(), Name = p };
                        projDict[code] = proj;
                    }
                    return new PersonProject { Id = Guid.NewGuid(), PersonId = personId, Project = proj };
                }).ToList(),
                PersonSectorExperiences = ParseSectors(record.SectorExperience).Select(s => {
                    if (!sectorDict.TryGetValue(s.Code, out var sec)) {
                        sec = new Sector { Id = Guid.NewGuid(), Code = s.Code, Name = s.Name };
                        sectorDict[s.Code] = sec;
                    }
                    return new PersonSectorExperience { Id = Guid.NewGuid(), PersonId = personId, ExperienceMonths = s.ExperienceMonths, Sector = sec };
                }).ToList(),
                PersonCertificates = ParseCertificates(record.Certificates).Select(c => {
                    var code = "CERT_" + SharedProfileConstants.Hash(c);
                    if (!certDict.TryGetValue(code, out var cert)) {
                        cert = new Certificate { Id = Guid.NewGuid(), Code = code, Name = c };
                        certDict[code] = cert;
                    }
                    return new PersonCertificate { Id = Guid.NewGuid(), PersonId = personId, Certificate = cert };
                }).ToList(),
                PersonLanguages = ParseLanguages(record.Languages).Select(l => {
                    if (!langDict.TryGetValue(l.Code, out var lang)) {
                        lang = new Language { Id = Guid.NewGuid(), Code = l.Code, Name = l.Name };
                        langDict[l.Code] = lang;
                    }
                    return new PersonLanguage { Id = Guid.NewGuid(), PersonId = personId, IsNative = l.IsNative, ProficiencyLevel = l.ProficiencyLevel, Language = lang };
                }).ToList()
            };

            // Education
            var degree = MapEducation(record.EducationLevel);
            if (degree.HasValue || !string.IsNullOrWhiteSpace(record.EducationField))
            {
                person.EducationRecords = new List<EducationRecord>
                {
                    new EducationRecord
                    {
                        Id = Guid.NewGuid(),
                        PersonId = personId,
                        DegreeLevel = degree ?? DegreeLevel.Other,
                        FieldOfStudy = record.EducationField?.Trim()
                    }
                };
            }

            entitiesToAdd.Add(person);
            inserted++;
        }

        if (entitiesToAdd.Any())
        {
            var efContext = (DbContext)_dbContext;
            using var transaction = await efContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // We add Persons. Because of EF Core navigations, it will insert Candidate, Features, History, Profiles, etc.
                await _dbContext.People.AddRangeAsync(entitiesToAdd, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw; // Or return a fatal error, but throwing is fine for single transaction fatal error.
            }
        }

        return new CandidateImportResult(inserted, skipped, failed, errors);
    }

    private static IReadOnlyList<(string Code, string Name, CompetencyCategory Category)> ParseCompetencies(string? skills, string? tools)
    {
        var tokens = DelimitedEvidenceParser.Parse(skills).Concat(DelimitedEvidenceParser.Parse(tools)).ToList();
        var occurrences = tokens
            .Where(SharedProfileConstants.Competencies.ContainsKey)
            .Select(token => SharedProfileConstants.Competencies[token])
            .Select(item => (SharedProfileConstants.Code(item.Name), item.Name, item.Category));

        var unresolved = tokens.Where(token => !SharedProfileConstants.Competencies.ContainsKey(token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token => (SharedProfileConstants.Code(token), token, CompetencyCategory.Tool));
            
        return occurrences.Concat(unresolved).GroupBy(x => x.Item2, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }

    private static IReadOnlyList<(string Code, string Name, int? ExperienceMonths)> ParseSectors(string? raw)
    {
        var result = new List<(string Code, string Name, int? ExperienceMonths)>();
        foreach (var token in DelimitedEvidenceParser.Parse(raw))
        {
            var m = Regex.Match(token, @"^(?<n>.*?)(?:\s*\((?<d>[^)]+)\))?$");
            var name = m.Groups["n"].Value.Trim();
            int? duration = null;
            if (m.Groups["d"].Success)
            {
                var parsed = DurationToMonthsParser.Parse(m.Groups["d"].Value);
                if (parsed.IsValid) duration = parsed.Months;
            }
            result.Add((SharedProfileConstants.Code(name), name, duration));
        }
        return result;
    }

    private static DegreeLevel? MapEducation(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (SharedProfileConstants.Degrees.TryGetValue(raw.Trim(), out var degree)) return degree;
        return null;
    }

    private static IReadOnlyList<string> ParseCertificates(string? raw) => 
        DelimitedEvidenceParser.Parse(raw).Where(x => !SharedProfileConstants.CertificateSentinels.Contains(x)).ToList();

    private static IReadOnlyList<(string Code, string Name, LanguageProficiencyLevel? ProficiencyLevel, bool IsNative)> ParseLanguages(string? raw)
    {
        var result = new List<(string, string, LanguageProficiencyLevel?, bool)>();
        foreach (var token in DelimitedEvidenceParser.Parse(raw))
        {
            var native = Regex.IsMatch(token, @"ana ?dil|anadil|native|mother tongue", RegexOptions.IgnoreCase);
            var levelMatch = Regex.Match(token, @"\b(A1|A2|B1|B2|C1|C2)\b", RegexOptions.IgnoreCase);
            var level = levelMatch.Success ? Enum.Parse<LanguageProficiencyLevel>(levelMatch.Value, true) : (LanguageProficiencyLevel?)null;
            var name = Regex.Replace(token, @"\([^)]*\)|\b(A1|A2|B1|B2|C1|C2)\b|ana ?dil|anadil|native|mother tongue", "", RegexOptions.IgnoreCase).Trim();
            if (name.Length == 0) continue;
            
            var canonical = SharedProfileConstants.Languages.TryGetValue(name, out var l) ? l : ("LANG_" + SharedProfileConstants.Hash(name), name);
            result.Add((canonical.Item1, canonical.Item2, level, native));
        }
        return result;
    }

    private static IReadOnlyList<string> ParseWorkModesStr(string? raw)
    {
        var text = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        if (text == null) return Array.Empty<string>();
        var list = new List<string>();
        if (text.Contains("Ofis", StringComparison.OrdinalIgnoreCase) || text.Contains("Yerinde", StringComparison.OrdinalIgnoreCase)) 
            list.Add("ON_SITE");
        if (text.Contains("Hibrit", StringComparison.OrdinalIgnoreCase)) 
            list.Add("HYBRID");
        if (text.Contains("Uzaktan", StringComparison.OrdinalIgnoreCase) || text.Contains("Remote", StringComparison.OrdinalIgnoreCase)) 
            list.Add("REMOTE");
        return list;
    }
}
