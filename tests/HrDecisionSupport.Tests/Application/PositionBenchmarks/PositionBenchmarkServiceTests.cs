using HrDecisionSupport.Application.PositionBenchmarks;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests.Application.PositionBenchmarks;

public sealed class PositionBenchmarkServiceTests
{
    [Fact]
    public async Task GetBenchmark_UnknownOrInactivePosition_ReturnsCurrentPositionErrors()
    {
        await using var context = TestDatabase.CreateContext();
        var inactive = TestDatabase.Position(isActive: false);
        context.Positions.Add(inactive);
        await context.SaveChangesAsync();
        var service = new PositionBenchmarkService(context);

        var unknownResult = await service.GetBenchmarkAsync(Guid.NewGuid());
        var inactiveResult = await service.GetBenchmarkAsync(inactive.Id);

        Assert.True(unknownResult.IsFailure);
        Assert.Equal("position_not_found", unknownResult.Error!.Code);
        Assert.True(inactiveResult.IsFailure);
        Assert.Equal("position_inactive", inactiveResult.Error!.Code);
    }

    [Fact]
    public async Task GetBenchmark_NoEmployees_ReturnsUnavailableWithoutSuggestions()
    {
        await using var scenario = await Scenario.CreateAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.True(result.IsSuccess);
        var benchmark = result.Value;
        Assert.Equal(0, benchmark.TotalEmployees);
        Assert.Equal(PositionBenchmarkSampleSizeStatus.Unavailable, benchmark.SampleSizeStatus);
        Assert.Equal(0, benchmark.Benchmark.Skills.Coverage.KnownProfiles);
        Assert.Equal(0, benchmark.Benchmark.Skills.Coverage.MissingProfiles);
        Assert.Empty(benchmark.Suggestions.PreferredCompetencies);
        Assert.Null(benchmark.Suggestions.MinimumRelevantExperienceMonths);
        Assert.Null(benchmark.Suggestions.MinimumEducationLevel);
        Assert.Empty(benchmark.Suggestions.Languages);
    }

    [Fact]
    public async Task GetBenchmark_PopulationUsesOpenActiveDistinctAssignmentSemantics()
    {
        await using var scenario = await Scenario.CreateAsync();
        var included = scenario.AddEmployee(
            scenario.Position,
            startDate: new DateOnly(2099, 1, 1));
        scenario.Context.EmployeeAssignments.Add(new EmployeeAssignment
        {
            Id = Guid.NewGuid(),
            EmployeeId = included.Employee.Id,
            DepartmentId = scenario.Department.Id,
            PositionId = scenario.Position.Id,
            StartDate = null,
            EndDate = null
        });
        scenario.AddEmployee(scenario.Position, status: EmploymentStatus.OnLeave);
        scenario.AddEmployee(
            scenario.Position,
            status: EmploymentStatus.Terminated,
            terminationDate: new DateOnly(2026, 2, 1));
        scenario.AddEmployee(
            scenario.Position,
            status: EmploymentStatus.Active,
            terminationDate: new DateOnly(2026, 2, 1));
        scenario.AddEmployee(
            scenario.Position,
            endDate: new DateOnly(2026, 1, 31));
        scenario.AddEmployee(scenario.OtherPosition);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalEmployees);
        Assert.Equal(PositionBenchmarkSampleSizeStatus.LimitedData, result.Value.SampleSizeStatus);
    }

    [Fact]
    public async Task Skills_UseKnownProfilesAsDenominatorAndSortDeterministically()
    {
        await using var scenario = await Scenario.CreateAsync();
        var csharp = scenario.AddCompetency("C#", "C_SHARP");
        var alpha = scenario.AddCompetency("Alpha", "ALPHA");
        var beta = scenario.AddCompetency("Beta", "BETA");
        var inactive = scenario.AddCompetency("Retired", "RETIRED", isActive: false);
        var employees = Enumerable.Range(0, 10)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();

        foreach (var employee in employees.Take(6))
            scenario.AddCompetency(employee, csharp);
        scenario.AddCompetency(employees[6], alpha);
        scenario.AddCompetency(employees[7], beta);
        scenario.AddCompetency(employees[7], inactive);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var skills = result.Value.Benchmark.Skills;
        Assert.Equal(10, skills.Coverage.TotalEmployees);
        Assert.Equal(8, skills.Coverage.KnownProfiles);
        Assert.Equal(2, skills.Coverage.MissingProfiles);
        var csharpItem = Assert.Single(skills.Items, item => item.CompetencyId == csharp.Id);
        Assert.Equal(6, csharpItem.EmployeeCount);
        Assert.Equal(8, csharpItem.KnownProfileCount);
        Assert.Equal(0.75m, csharpItem.Percentage);
        Assert.DoesNotContain(skills.Items, item => item.CompetencyId == inactive.Id);
        Assert.Equal(new[] { "C#", "Alpha", "Beta" },
            skills.Items.Select(item => item.CompetencyName));
    }

    [Fact]
    public async Task Skills_StrictMajorityProducesPreferredSuggestionButExactHalfDoesNot()
    {
        await using var scenario = await Scenario.CreateAsync();
        var common = scenario.AddCompetency("Common", "COMMON");
        var half = scenario.AddCompetency("Half", "HALF");
        var filler = scenario.AddCompetency("Filler", "FILLER");
        var employees = Enumerable.Range(0, 4)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();
        foreach (var employee in employees.Take(3))
            scenario.AddCompetency(employee, common);
        foreach (var employee in employees.Take(2))
            scenario.AddCompetency(employee, half);
        scenario.AddCompetency(employees[3], filler);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var suggestion = Assert.Single(result.Value.Suggestions.PreferredCompetencies);
        Assert.Equal(common.Id, suggestion.CompetencyId);
        Assert.False(suggestion.IsRequired);
        Assert.DoesNotContain(result.Value.Suggestions.PreferredCompetencies,
            item => item.CompetencyId == half.Id);
    }

    [Fact]
    public async Task Skills_WithoutStrictMajority_SuggestsTwoMostCommonActiveCompetencies()
    {
        await using var scenario = await Scenario.CreateAsync();
        var alpha = scenario.AddCompetency("Alpha", "ALPHA");
        var beta = scenario.AddCompetency("Beta", "BETA");
        var lower = scenario.AddCompetency("Lower", "LOWER");
        var employees = Enumerable.Range(0, 4)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();

        scenario.AddCompetency(employees[0], alpha);
        scenario.AddCompetency(employees[1], alpha);
        scenario.AddCompetency(employees[2], beta);
        scenario.AddCompetency(employees[3], beta);
        scenario.AddCompetency(employees[0], lower);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.Equal(
            new[] { alpha.Id, beta.Id },
            result.Value.Suggestions.PreferredCompetencies.Select(item => item.CompetencyId));
        Assert.All(result.Value.Suggestions.PreferredCompetencies, item => Assert.False(item.IsRequired));
    }

    [Fact]
    public async Task Skills_FewerThanThreeKnownProfilesSuppressSuggestions()
    {
        await using var scenario = await Scenario.CreateAsync();
        var skill = scenario.AddCompetency("C#", "C_SHARP");
        var first = scenario.AddEmployee(scenario.Position);
        var second = scenario.AddEmployee(scenario.Position);
        scenario.AddCompetency(first, skill);
        scenario.AddCompetency(second, skill);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.Equal(2, result.Value.Benchmark.Skills.Coverage.KnownProfiles);
        Assert.Empty(result.Value.Suggestions.PreferredCompetencies);
    }

    [Fact]
    public async Task Skills_DuplicatePersonCompetencyRowsCountEmployeeOnce()
    {
        await using var scenario = await Scenario.CreateAsync();
        var skill = scenario.AddCompetency("C#", "C_SHARP");
        var employee = scenario.AddEmployee(scenario.Position);
        scenario.AddCompetency(employee, skill);
        scenario.AddCompetency(employee, skill);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var item = Assert.Single(result.Value.Benchmark.Skills.Items);
        Assert.Equal(1, item.EmployeeCount);
        Assert.Equal(1, item.KnownProfileCount);
        Assert.Equal(1m, item.Percentage);
    }

    [Fact]
    public async Task Experience_BackendUsesLatestKnownSnapshotsAndOverflowSafeEvenMedian()
    {
        await using var scenario = await Scenario.CreateAsync(backendPosition: true);
        var employees = Enumerable.Range(0, 5)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();
        scenario.AddSnapshot(employees[0], backendMonths: 100, totalMonths: 100, createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        scenario.AddSnapshot(employees[0], backendMonths: 0, totalMonths: 500, createdAt: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        scenario.AddSnapshot(employees[1], backendMonths: int.MaxValue - 1, totalMonths: null);
        scenario.AddSnapshot(employees[2], backendMonths: int.MaxValue, totalMonths: null);
        scenario.AddSnapshot(employees[3], backendMonths: int.MaxValue, totalMonths: null);
        scenario.AddSnapshot(employees[4], backendMonths: 60, totalMonths: 60, createdAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        scenario.AddSnapshot(employees[4], backendMonths: null, totalMonths: int.MaxValue, createdAt: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var experience = result.Value.Benchmark.Experience;
        Assert.True(experience.IsSupported);
        Assert.Equal(4, experience.Coverage.KnownProfiles);
        Assert.Equal(1, experience.Coverage.MissingProfiles);
        Assert.Equal(2147483646.5m, experience.MedianMonths);
        Assert.Equal(2147483646, result.Value.Suggestions.MinimumRelevantExperienceMonths);
    }

    [Fact]
    public async Task Experience_OddMedianAndFewerThanThreeSuppressionAreDeterministic()
    {
        await using var sufficient = await Scenario.CreateAsync(backendPosition: true);
        foreach (var months in new[] { 36, 12, 24 })
        {
            var employee = sufficient.AddEmployee(sufficient.Position);
            sufficient.AddSnapshot(employee, months, totalMonths: null);
        }
        await sufficient.Context.SaveChangesAsync();
        var sufficientResult = await sufficient.Service.GetBenchmarkAsync(sufficient.Position.Id);

        await using var limited = await Scenario.CreateAsync(backendPosition: true);
        foreach (var months in new[] { 12, 24 })
        {
            var employee = limited.AddEmployee(limited.Position);
            limited.AddSnapshot(employee, months, totalMonths: null);
        }
        await limited.Context.SaveChangesAsync();
        var limitedResult = await limited.Service.GetBenchmarkAsync(limited.Position.Id);

        Assert.Equal(24m, sufficientResult.Value.Benchmark.Experience.MedianMonths);
        Assert.Equal(24, sufficientResult.Value.Suggestions.MinimumRelevantExperienceMonths);
        Assert.Equal(18m, limitedResult.Value.Benchmark.Experience.MedianMonths);
        Assert.Null(limitedResult.Value.Suggestions.MinimumRelevantExperienceMonths);
    }

    [Fact]
    public async Task Experience_LatestSnapshotUsesDescendingIdAsTieBreak()
    {
        await using var scenario = await Scenario.CreateAsync(backendPosition: true);
        var employee = scenario.AddEmployee(scenario.Position);
        var createdAt = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        scenario.AddSnapshot(
            employee,
            backendMonths: 12,
            totalMonths: null,
            createdAt,
            Guid.Parse("00000000-0000-0000-0000-000000000001"));
        scenario.AddSnapshot(
            employee,
            backendMonths: 36,
            totalMonths: null,
            createdAt,
            Guid.Parse("00000000-0000-0000-0000-000000000002"));
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.Equal(36m, result.Value.Benchmark.Experience.MedianMonths);
    }

    [Fact]
    public async Task Experience_UnsupportedPositionDoesNotSubstituteTotalExperience()
    {
        await using var scenario = await Scenario.CreateAsync();
        var employee = scenario.AddEmployee(scenario.Position);
        scenario.AddSnapshot(employee, backendMonths: 48, totalMonths: 240);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.False(result.Value.Benchmark.Experience.IsSupported);
        Assert.Equal(0, result.Value.Benchmark.Experience.Coverage.KnownProfiles);
        Assert.Null(result.Value.Benchmark.Experience.MedianMonths);
        Assert.Null(result.Value.Suggestions.MinimumRelevantExperienceMonths);
    }

    [Fact]
    public async Task Education_UsesHighestRankDistributionAndStrictMajoritySuggestion()
    {
        await using var scenario = await Scenario.CreateAsync();
        var employees = Enumerable.Range(0, 5)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();
        scenario.AddEducation(employees[0], DegreeLevel.Other);
        scenario.AddEducation(employees[0], DegreeLevel.Bachelor);
        scenario.AddEducation(employees[1], DegreeLevel.Master);
        scenario.AddEducation(employees[2], DegreeLevel.Bachelor);
        scenario.AddEducation(employees[3], DegreeLevel.Other);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var education = result.Value.Benchmark.Education;
        Assert.Equal(4, education.Coverage.KnownProfiles);
        Assert.Equal(1, education.Coverage.MissingProfiles);
        Assert.Equal(2, Assert.Single(education.Distribution,
            item => item.DegreeLevel == DegreeLevel.Bachelor).EmployeeCount);
        Assert.Equal(1, Assert.Single(education.Distribution,
            item => item.DegreeLevel == DegreeLevel.Master).EmployeeCount);
        Assert.Equal(1, Assert.Single(education.Distribution,
            item => item.DegreeLevel == DegreeLevel.Other).EmployeeCount);
        Assert.Equal(DegreeLevel.Bachelor, result.Value.Suggestions.MinimumEducationLevel);
    }

    [Fact]
    public async Task Education_ExactlyHalfComparableAndOtherOnlyDoNotProduceSuggestion()
    {
        await using var exactHalf = await Scenario.CreateAsync();
        var employees = Enumerable.Range(0, 4)
            .Select(_ => exactHalf.AddEmployee(exactHalf.Position))
            .ToArray();
        exactHalf.AddEducation(employees[0], DegreeLevel.Doctorate);
        exactHalf.AddEducation(employees[1], DegreeLevel.Doctorate);
        exactHalf.AddEducation(employees[2], DegreeLevel.Other);
        exactHalf.AddEducation(employees[3], DegreeLevel.Other);
        await exactHalf.Context.SaveChangesAsync();
        var exactHalfResult = await exactHalf.Service.GetBenchmarkAsync(exactHalf.Position.Id);

        await using var limitedOther = await Scenario.CreateAsync();
        var first = limitedOther.AddEmployee(limitedOther.Position);
        var second = limitedOther.AddEmployee(limitedOther.Position);
        limitedOther.AddEducation(first, DegreeLevel.Other);
        limitedOther.AddEducation(second, DegreeLevel.Other);
        await limitedOther.Context.SaveChangesAsync();
        var limitedResult = await limitedOther.Service.GetBenchmarkAsync(limitedOther.Position.Id);

        Assert.Null(exactHalfResult.Value.Suggestions.MinimumEducationLevel);
        Assert.Null(limitedResult.Value.Suggestions.MinimumEducationLevel);
    }

    [Fact]
    public async Task Languages_UseKnownDenominatorExcludeNullProficiencyAndUseLowerEvenMedian()
    {
        await using var scenario = await Scenario.CreateAsync();
        var english = scenario.AddLanguage("English", "EN");
        var german = scenario.AddLanguage("German", "DE");
        var employees = Enumerable.Range(0, 6)
            .Select(_ => scenario.AddEmployee(scenario.Position))
            .ToArray();
        scenario.AddLanguage(employees[0], english, LanguageProficiencyLevel.B1);
        scenario.AddLanguage(employees[1], english, LanguageProficiencyLevel.B2);
        scenario.AddLanguage(employees[2], english, LanguageProficiencyLevel.C1);
        scenario.AddLanguage(employees[3], english, LanguageProficiencyLevel.C2);
        scenario.AddLanguage(employees[4], german, proficiency: null);
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        var languages = result.Value.Benchmark.Languages;
        Assert.Equal(5, languages.Coverage.KnownProfiles);
        Assert.Equal(1, languages.Coverage.MissingProfiles);
        var item = Assert.Single(languages.Items, value => value.LanguageId == english.Id);
        Assert.Equal(4, item.EmployeeCount);
        Assert.Equal(5, item.KnownProfileCount);
        Assert.Equal(0.8m, item.Percentage);
        Assert.Equal(4, item.KnownProficiencyCount);
        Assert.Equal(4.5m, item.MedianProficiencyRank);
        var suggestion = Assert.Single(result.Value.Suggestions.Languages);
        Assert.Equal(LanguageProficiencyLevel.B2, suggestion.MinimumProficiency);
        Assert.False(suggestion.HardFilterEnabled);
    }

    [Fact]
    public async Task Languages_NativeIsAboveC2AndSuggestionIsCappedAtC2()
    {
        await using var scenario = await Scenario.CreateAsync();
        var english = scenario.AddLanguage("English", "EN");
        foreach (var _ in Enumerable.Range(0, 3))
        {
            var employee = scenario.AddEmployee(scenario.Position);
            scenario.AddLanguage(employee, english, proficiency: null, isNative: true);
        }
        await scenario.Context.SaveChangesAsync();

        var result = await scenario.Service.GetBenchmarkAsync(scenario.Position.Id);

        Assert.Equal(PositionBenchmarkSampleSizeStatus.Sufficient, result.Value.SampleSizeStatus);
        var item = Assert.Single(result.Value.Benchmark.Languages.Items);
        Assert.Equal(3, item.NativeSpeakerCount);
        Assert.Equal(7m, item.MedianProficiencyRank);
        var suggestion = Assert.Single(result.Value.Suggestions.Languages);
        Assert.Equal(LanguageProficiencyLevel.C2, suggestion.MinimumProficiency);
        Assert.False(suggestion.HardFilterEnabled);
    }

    [Fact]
    public async Task Languages_RequireStrictMajorityAndThreeProfilesAndProficiencies()
    {
        await using var exactHalf = await Scenario.CreateAsync();
        var english = exactHalf.AddLanguage("English", "EN");
        var german = exactHalf.AddLanguage("German", "DE");
        var employees = Enumerable.Range(0, 4)
            .Select(_ => exactHalf.AddEmployee(exactHalf.Position))
            .ToArray();
        exactHalf.AddLanguage(employees[0], english, LanguageProficiencyLevel.B2);
        exactHalf.AddLanguage(employees[1], english, LanguageProficiencyLevel.B2);
        exactHalf.AddLanguage(employees[2], german, LanguageProficiencyLevel.B2);
        exactHalf.AddLanguage(employees[3], german, LanguageProficiencyLevel.B2);
        await exactHalf.Context.SaveChangesAsync();
        var exactHalfResult = await exactHalf.Service.GetBenchmarkAsync(exactHalf.Position.Id);

        await using var missingProficiency = await Scenario.CreateAsync();
        var language = missingProficiency.AddLanguage("English", "EN");
        foreach (var index in Enumerable.Range(0, 3))
        {
            var employee = missingProficiency.AddEmployee(missingProficiency.Position);
            missingProficiency.AddLanguage(
                employee,
                language,
                index < 2 ? LanguageProficiencyLevel.B2 : null);
        }
        await missingProficiency.Context.SaveChangesAsync();
        var missingProficiencyResult = await missingProficiency.Service
            .GetBenchmarkAsync(missingProficiency.Position.Id);

        Assert.Empty(exactHalfResult.Value.Suggestions.Languages);
        Assert.Empty(missingProficiencyResult.Value.Suggestions.Languages);
    }

    private sealed class Scenario : IAsyncDisposable
    {
        private int _employeeSequence;

        private Scenario(
            HrDecisionSupportDbContext context,
            Department department,
            Position position,
            Position otherPosition,
            EmployeeImportBatch batch)
        {
            Context = context;
            Department = department;
            Position = position;
            OtherPosition = otherPosition;
            Batch = batch;
            Service = new PositionBenchmarkService(context);
        }

        public HrDecisionSupportDbContext Context { get; }
        public Department Department { get; }
        public Position Position { get; }
        public Position OtherPosition { get; }
        public EmployeeImportBatch Batch { get; }
        public PositionBenchmarkService Service { get; }

        public static async Task<Scenario> CreateAsync(bool backendPosition = false)
        {
            var context = TestDatabase.CreateContext();
            var department = TestDatabase.Department();
            var position = TestDatabase.Position();
            if (backendPosition)
            {
                position.Code = "BACKEND_DEVELOPER";
                position.Name = "Backend Developer";
            }
            var otherPosition = TestDatabase.Position();
            var batch = new EmployeeImportBatch
            {
                Id = Guid.NewGuid(),
                FileName = "benchmark-test.xlsx",
                FileHash = Guid.NewGuid().ToString("N"),
                DatasetSplit = EmployeeDatasetSplit.Production,
                ImportedAtUtc = DateTime.UtcNow,
                Status = EmployeeImportBatchStatus.Completed,
                TotalRowCount = 0,
                SuccessfulRowCount = 0,
                FailedRowCount = 0,
                ObservationDate = new DateOnly(2026, 1, 1)
            };
            context.AddRange(department, position, otherPosition, batch);
            await context.SaveChangesAsync();
            return new Scenario(context, department, position, otherPosition, batch);
        }

        public Worker AddEmployee(
            Position position,
            EmploymentStatus status = EmploymentStatus.Active,
            DateOnly? terminationDate = null,
            DateOnly? startDate = null,
            DateOnly? endDate = null)
        {
            var sequence = ++_employeeSequence;
            var person = TestDatabase.Person($"PB-{sequence:D3}-{Guid.NewGuid():N}");
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = person.Id,
                EmployeeCode = $"PB-E-{sequence:D3}-{Guid.NewGuid():N}"[..30],
                HireDate = new DateOnly(2020, 1, 1),
                TerminationDate = terminationDate,
                EmploymentStatus = status
            };
            var assignment = new EmployeeAssignment
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                DepartmentId = Department.Id,
                PositionId = position.Id,
                StartDate = startDate ?? new DateOnly(2020, 1, 1),
                EndDate = endDate
            };
            Context.AddRange(person, employee, assignment);
            return new Worker(person, employee, assignment);
        }

        public Competency AddCompetency(string name, string code, bool isActive = true)
        {
            var competency = new Competency
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = name,
                CompetencyCategory = CompetencyCategory.Skill,
                IsActive = isActive
            };
            Context.Competencies.Add(competency);
            return competency;
        }

        public void AddCompetency(Worker worker, Competency competency) =>
            Context.PersonCompetencies.Add(new PersonCompetency
            {
                Id = Guid.NewGuid(),
                PersonId = worker.Person.Id,
                CompetencyId = competency.Id
            });

        public void AddSnapshot(
            Worker worker,
            int? backendMonths,
            int? totalMonths,
            DateTime? createdAt = null,
            Guid? id = null) =>
            Context.EmployeeCareerFeatureSnapshots.Add(new EmployeeCareerFeatureSnapshot
            {
                Id = id ?? Guid.NewGuid(),
                EmployeeId = worker.Employee.Id,
                ImportBatchId = Batch.Id,
                ObservedAt = new DateOnly(2026, 1, 1),
                TotalExperienceMonths = totalMonths,
                BackendExperienceMonths = backendMonths,
                FeatureSource = EmployeeCareerFeatureSource.ImportedAggregate,
                FeatureSchemaVersion = Guid.NewGuid().ToString("N")[..8],
                CreatedAtUtc = createdAt ?? DateTime.UtcNow
            });

        public void AddEducation(Worker worker, DegreeLevel level) =>
            Context.EducationRecords.Add(new EducationRecord
            {
                Id = Guid.NewGuid(),
                PersonId = worker.Person.Id,
                DegreeLevel = level
            });

        public Language AddLanguage(string name, string code)
        {
            var language = new Language { Id = Guid.NewGuid(), Name = name, Code = code };
            Context.Languages.Add(language);
            return language;
        }

        public void AddLanguage(
            Worker worker,
            Language language,
            LanguageProficiencyLevel? proficiency,
            bool isNative = false) =>
            Context.PersonLanguages.Add(new PersonLanguage
            {
                Id = Guid.NewGuid(),
                PersonId = worker.Person.Id,
                LanguageId = language.Id,
                ProficiencyLevel = proficiency,
                IsNative = isNative
            });

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed record Worker(
        Person Person,
        Employee Employee,
        EmployeeAssignment Assignment);
}
