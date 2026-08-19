using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports;
using HrDecisionSupport.Application.PreScreening.Helpers;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.PositionBenchmarks;

public sealed class PositionBenchmarkService : IPositionBenchmarkService
{
    private const int MinimumKnownProfilesForSuggestion = 3;
    private const int NativeLanguageRank = 7;
    private readonly IHrDecisionSupportDbContext _dbContext;

    public PositionBenchmarkService(IHrDecisionSupportDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<PositionBenchmarkResult>> GetBenchmarkAsync(
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var position = await _dbContext.Positions
            .AsNoTracking()
            .Where(item => item.Id == positionId)
            .Select(item => new PositionRecord(item.Id, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        if (position is null)
            return Result<PositionBenchmarkResult>.Failure(UseCaseErrors.PositionNotFound);
        if (!position.IsActive)
            return Result<PositionBenchmarkResult>.Failure(UseCaseErrors.PositionInactive);

        var supportsRelevantExperience = string.Equals(
            position.Code,
            OrganizationCatalogDefinitions.BackendDeveloperPositionCode,
            StringComparison.Ordinal);

        var population = await _dbContext.EmployeeAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.PositionId == positionId
                && assignment.EndDate == null
                && assignment.Employee.EmploymentStatus == EmploymentStatus.Active
                && assignment.Employee.TerminationDate == null)
            .Select(assignment => new PopulationIdentity(
                assignment.EmployeeId,
                assignment.Employee.PersonId))
            .Distinct()
            .OrderBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        var latestExperienceByEmployee = new Dictionary<Guid, int?>();
        if (supportsRelevantExperience)
        {
            var employeeIds = population.Select(item => item.EmployeeId).ToArray();
            var snapshots = await _dbContext.EmployeeCareerFeatureSnapshots
                .AsNoTracking()
                .Where(snapshot => employeeIds.Contains(snapshot.EmployeeId))
                .Select(snapshot => new SnapshotRecord(
                    snapshot.Id,
                    snapshot.EmployeeId,
                    snapshot.BackendExperienceMonths,
                    snapshot.CreatedAtUtc))
                .ToListAsync(cancellationToken);

            latestExperienceByEmployee = snapshots
                .GroupBy(snapshot => snapshot.EmployeeId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
                        .ThenByDescending(snapshot => snapshot.Id)
                        .First()
                        .BackendExperienceMonths);
        }

        var employees = population
            .Select(item => new PopulationEmployee(
                item.EmployeeId,
                item.PersonId,
                latestExperienceByEmployee.GetValueOrDefault(item.EmployeeId)))
            .ToArray();

        var totalEmployees = employees.Length;
        var personIds = employees.Select(item => item.PersonId).Distinct().ToArray();

        var competencyRecords = await _dbContext.PersonCompetencies
            .AsNoTracking()
            .Where(item => personIds.Contains(item.PersonId))
            .Select(item => new CompetencyRecord(
                item.PersonId,
                item.CompetencyId,
                item.Competency.Code,
                item.Competency.Name,
                item.Competency.CompetencyCategory,
                item.Competency.IsActive))
            .ToListAsync(cancellationToken);

        var educationRecords = await _dbContext.EducationRecords
            .AsNoTracking()
            .Where(item => personIds.Contains(item.PersonId))
            .Select(item => new EducationRecord(item.PersonId, item.DegreeLevel))
            .ToListAsync(cancellationToken);

        var languageRecords = await _dbContext.PersonLanguages
            .AsNoTracking()
            .Where(item => personIds.Contains(item.PersonId))
            .Select(item => new LanguageRecord(
                item.PersonId,
                item.LanguageId,
                item.Language.Code,
                item.Language.Name,
                item.ProficiencyLevel,
                item.IsNative))
            .ToListAsync(cancellationToken);

        var skills = BuildSkills(totalEmployees, competencyRecords);
        var experience = BuildExperience(totalEmployees, supportsRelevantExperience, employees);
        var education = BuildEducation(totalEmployees, educationRecords);
        var employeeLanguages = BuildEmployeeLanguages(languageRecords);
        var languages = BuildLanguages(totalEmployees, languageRecords, employeeLanguages);

        var suggestions = new PositionRequirementSuggestions(
            BuildSkillSuggestions(skills),
            BuildExperienceSuggestion(experience),
            BuildEducationSuggestion(education),
            BuildLanguageSuggestions(languages, employeeLanguages));

        return Result<PositionBenchmarkResult>.Success(new PositionBenchmarkResult(
            position.Id,
            position.Code,
            position.Name,
            totalEmployees,
            GetSampleSizeStatus(totalEmployees),
            new PositionWorkforceBenchmark(skills, experience, education, languages),
            suggestions));
    }

    private static PositionSkillBenchmark BuildSkills(
        int totalEmployees,
        IReadOnlyCollection<CompetencyRecord> records)
    {
        var knownProfiles = records.Select(item => item.PersonId).Distinct().Count();
        var items = records
            .Where(item => item.IsActive)
            .GroupBy(item => new
            {
                item.PersonId,
                item.CompetencyId,
                item.CompetencyCode,
                item.CompetencyName,
                item.CompetencyCategory
            })
            .Select(group => group.Key)
            .GroupBy(item => new
            {
                item.CompetencyId,
                item.CompetencyCode,
                item.CompetencyName,
                item.CompetencyCategory
            })
            .Select(group => new PositionSkillFrequency(
                group.Key.CompetencyId,
                group.Key.CompetencyCode,
                group.Key.CompetencyName,
                group.Key.CompetencyCategory,
                group.Count(),
                knownProfiles,
                Percentage(group.Count(), knownProfiles)))
            .OrderByDescending(item => item.EmployeeCount)
            .ThenBy(item => item.CompetencyName, StringComparer.Ordinal)
            .ThenBy(item => item.CompetencyId)
            .ToArray();

        return new PositionSkillBenchmark(
            new BenchmarkProfileCoverage(totalEmployees, knownProfiles),
            items);
    }

    private static PositionExperienceBenchmark BuildExperience(
        int totalEmployees,
        bool isSupported,
        IReadOnlyCollection<PopulationEmployee> employees)
    {
        if (!isSupported)
        {
            return new PositionExperienceBenchmark(
                false,
                new BenchmarkProfileCoverage(totalEmployees, 0),
                null);
        }

        var values = employees
            .Where(item => item.RelevantExperienceMonths.HasValue)
            .Select(item => item.RelevantExperienceMonths!.Value)
            .Order()
            .ToArray();

        return new PositionExperienceBenchmark(
            true,
            new BenchmarkProfileCoverage(totalEmployees, values.Length),
            Median(values));
    }

    private static PositionEducationBenchmark BuildEducation(
        int totalEmployees,
        IReadOnlyCollection<EducationRecord> records)
    {
        var highestByPerson = records
            .GroupBy(item => item.PersonId)
            .Select(group => group
                .OrderByDescending(item => EducationRankHelper.GetEducationRank(item.DegreeLevel))
                .ThenBy(item => item.DegreeLevel)
                .First().DegreeLevel)
            .ToArray();
        var knownProfiles = highestByPerson.Length;

        var distribution = highestByPerson
            .GroupBy(level => level)
            .Select(group =>
            {
                var rank = EducationRankHelper.GetEducationRank(group.Key);
                var atOrAbove = highestByPerson.Count(level =>
                    EducationRankHelper.GetEducationRank(level) >= rank);
                return new PositionEducationDistribution(
                    group.Key,
                    group.Count(),
                    knownProfiles,
                    Percentage(group.Count(), knownProfiles),
                    atOrAbove,
                    Percentage(atOrAbove, knownProfiles));
            })
            .OrderByDescending(item => EducationRankHelper.GetEducationRank(item.DegreeLevel))
            .ThenBy(item => item.DegreeLevel)
            .ToArray();

        return new PositionEducationBenchmark(
            new BenchmarkProfileCoverage(totalEmployees, knownProfiles),
            distribution);
    }

    private static PositionLanguageBenchmark BuildLanguages(
        int totalEmployees,
        IReadOnlyCollection<LanguageRecord> records,
        IReadOnlyCollection<EmployeeLanguageRecord> perEmployeeLanguage)
    {
        var knownProfiles = records.Select(item => item.PersonId).Distinct().Count();
        var items = perEmployeeLanguage
            .GroupBy(item => new
            {
                item.LanguageId,
                item.LanguageCode,
                item.LanguageName
            })
            .Select(group =>
            {
                var knownRanks = group
                    .Where(item => item.ProficiencyRank > 0)
                    .Select(item => item.ProficiencyRank)
                    .Order()
                    .ToArray();
                return new PositionLanguageFrequency(
                    group.Key.LanguageId,
                    group.Key.LanguageCode,
                    group.Key.LanguageName,
                    group.Count(),
                    knownProfiles,
                    Percentage(group.Count(), knownProfiles),
                    knownRanks.Length,
                    group.Count(item => item.IsNative),
                    Median(knownRanks));
            })
            .OrderByDescending(item => item.EmployeeCount)
            .ThenBy(item => item.LanguageName, StringComparer.Ordinal)
            .ThenBy(item => item.LanguageId)
            .ToArray();

        return new PositionLanguageBenchmark(
            new BenchmarkProfileCoverage(totalEmployees, knownProfiles),
            items);
    }

    private static IReadOnlyList<EmployeeLanguageRecord> BuildEmployeeLanguages(
        IReadOnlyCollection<LanguageRecord> records) =>
        records
            .GroupBy(item => new
            {
                item.PersonId,
                item.LanguageId,
                item.LanguageCode,
                item.LanguageName
            })
            .Select(group => new EmployeeLanguageRecord(
                group.Key.PersonId,
                group.Key.LanguageId,
                group.Key.LanguageCode,
                group.Key.LanguageName,
                group.Where(IsKnownProficiency)
                    .Select(GetLanguageRank)
                    .DefaultIfEmpty(0)
                    .Max(),
                group.Any(item => item.IsNative)))
            .ToArray();

    private static IReadOnlyList<PositionCompetencySuggestion> BuildSkillSuggestions(
        PositionSkillBenchmark benchmark)
    {
        if (benchmark.Coverage.KnownProfiles < MinimumKnownProfilesForSuggestion)
            return [];

        return benchmark.Items
            .Where(item => IsStrictMajority(item.EmployeeCount, item.KnownProfileCount))
            .Select(item => new PositionCompetencySuggestion(
                item.CompetencyId,
                item.CompetencyCode,
                item.CompetencyName,
                IsRequired: false))
            .ToArray();
    }

    private static int? BuildExperienceSuggestion(PositionExperienceBenchmark benchmark)
    {
        if (!benchmark.IsSupported
            || benchmark.Coverage.KnownProfiles < MinimumKnownProfilesForSuggestion
            || !benchmark.MedianMonths.HasValue)
        {
            return null;
        }

        return decimal.ToInt32(decimal.Floor(benchmark.MedianMonths.Value));
    }

    private static DegreeLevel? BuildEducationSuggestion(PositionEducationBenchmark benchmark)
    {
        if (benchmark.Coverage.KnownProfiles < MinimumKnownProfilesForSuggestion)
            return null;

        var orderedLevels = new[]
        {
            DegreeLevel.Doctorate,
            DegreeLevel.Master,
            DegreeLevel.Bachelor,
            DegreeLevel.Associate,
            DegreeLevel.HighSchool
        };

        foreach (var level in orderedLevels)
        {
            var requiredRank = EducationRankHelper.GetEducationRank(level);
            var atOrAbove = benchmark.Distribution
                .Where(item => EducationRankHelper.GetEducationRank(item.DegreeLevel) >= requiredRank)
                .Sum(item => item.EmployeeCount);
            if (IsStrictMajority(atOrAbove, benchmark.Coverage.KnownProfiles))
                return level;
        }

        return null;
    }

    private static IReadOnlyList<PositionLanguageSuggestion> BuildLanguageSuggestions(
        PositionLanguageBenchmark benchmark,
        IReadOnlyCollection<EmployeeLanguageRecord> employeeLanguages)
    {
        if (benchmark.Coverage.KnownProfiles < MinimumKnownProfilesForSuggestion)
            return [];

        return benchmark.Items
            .Where(item =>
                IsStrictMajority(item.EmployeeCount, item.KnownProfileCount)
                && item.KnownProficiencyCount >= MinimumKnownProfilesForSuggestion)
            .Select(item => new PositionLanguageSuggestion(
                item.LanguageId,
                item.LanguageCode,
                item.LanguageName,
                GetSuggestedLanguageProficiency(item.LanguageId),
                HardFilterEnabled: false))
            .ToArray();

        LanguageProficiencyLevel GetSuggestedLanguageProficiency(Guid languageId)
        {
            var ranks = employeeLanguages
                .Where(item => item.LanguageId == languageId && item.ProficiencyRank > 0)
                .Select(item => item.ProficiencyRank)
                .Order()
                .ToArray();
            var lowerMedianRank = ranks[(ranks.Length - 1) / 2];
            var cappedRank = Math.Min(lowerMedianRank, LanguageRankHelper.GetProficiencyRank(
                LanguageProficiencyLevel.C2));
            return GetLanguageProficiency(cappedRank);
        }
    }

    private static PositionBenchmarkSampleSizeStatus GetSampleSizeStatus(int employeeCount) =>
        employeeCount switch
        {
            0 => PositionBenchmarkSampleSizeStatus.Unavailable,
            <= 2 => PositionBenchmarkSampleSizeStatus.LimitedData,
            _ => PositionBenchmarkSampleSizeStatus.Sufficient
        };

    private static bool IsStrictMajority(int count, int denominator) =>
        (long)count * 2 > denominator;

    private static decimal Percentage(int count, int denominator) =>
        denominator == 0 ? 0m : (decimal)count / denominator;

    private static decimal? Median(IReadOnlyList<int> orderedValues)
    {
        if (orderedValues.Count == 0)
            return null;

        var middle = orderedValues.Count / 2;
        if (orderedValues.Count % 2 != 0)
            return orderedValues[middle];

        return ((long)orderedValues[middle - 1] + orderedValues[middle]) / 2m;
    }

    private static bool IsKnownProficiency(LanguageRecord item) =>
        item.IsNative || item.ProficiencyLevel.HasValue;

    private static int GetLanguageRank(LanguageRecord item) =>
        item.IsNative
            ? NativeLanguageRank
            : LanguageRankHelper.GetProficiencyRank(item.ProficiencyLevel!.Value);

    private static LanguageProficiencyLevel GetLanguageProficiency(int rank) => rank switch
    {
        1 => LanguageProficiencyLevel.A1,
        2 => LanguageProficiencyLevel.A2,
        3 => LanguageProficiencyLevel.B1,
        4 => LanguageProficiencyLevel.B2,
        5 => LanguageProficiencyLevel.C1,
        6 => LanguageProficiencyLevel.C2,
        _ => throw new ArgumentOutOfRangeException(nameof(rank), rank, "Unsupported language rank.")
    };

    private sealed record PositionRecord(Guid Id, string Code, string Name, bool IsActive);

    private sealed record PopulationEmployee(
        Guid EmployeeId,
        Guid PersonId,
        int? RelevantExperienceMonths);

    private sealed record PopulationIdentity(Guid EmployeeId, Guid PersonId);

    private sealed record SnapshotRecord(
        Guid Id,
        Guid EmployeeId,
        int? BackendExperienceMonths,
        DateTime CreatedAtUtc);

    private sealed record CompetencyRecord(
        Guid PersonId,
        Guid CompetencyId,
        string CompetencyCode,
        string CompetencyName,
        CompetencyCategory CompetencyCategory,
        bool IsActive);

    private sealed record EducationRecord(Guid PersonId, DegreeLevel DegreeLevel);

    private sealed record LanguageRecord(
        Guid PersonId,
        Guid LanguageId,
        string LanguageCode,
        string LanguageName,
        LanguageProficiencyLevel? ProficiencyLevel,
        bool IsNative);

    private sealed record EmployeeLanguageRecord(
        Guid PersonId,
        Guid LanguageId,
        string LanguageCode,
        string LanguageName,
        int ProficiencyRank,
        bool IsNative);
}
