using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class EmployeeImportProcessingTests
{
    [Theory]
    [InlineData("C#", "C_SHARP")]
    [InlineData("C Sharp", "C_SHARP")]
    [InlineData("c#", "C_SHARP")]
    [InlineData("C", "C")]
    [InlineData("C++", "CPP")]
    [InlineData(".NET", "DOTNET")]
    [InlineData(".NET Core", "DOTNET_CORE")]
    [InlineData("ASP.NET Core", "ASP_NET_CORE")]
    [InlineData("Vue.js", "VUE_JS")]
    [InlineData("Node.js", "NODE_JS")]
    [InlineData("CI/CD", "CI_CD")]
    public void Normalizer_UsesExplicitCanonicalCompetencyCodes(string input, string expected)
    {
        var row = new EmployeeImportRowNormalizer().Normalize(Source() with { TechnicalSkillsRaw = input });
        var competency = Assert.Single(row.Competencies);
        Assert.Equal(expected, competency.Code);
        if (input is "C#" or "C Sharp" or "c#") { Assert.Equal("C#", competency.Name); Assert.Equal(CompetencyCategory.ProgrammingLanguage, competency.Category); }
    }

    [Theory]
    [MemberData(nameof(CsvCompetencies))]
    public void Normalizer_ResolvesCsvCompetenciesWithoutUnknownDiagnostics(string token, string code, string name)
    {
        var result = new EmployeeImportRowNormalizer().Normalize(Source() with { TechnicalSkillsRaw = token, TechnologiesAndToolsRaw = null });
        var competency = Assert.Single(result.Competencies);
        Assert.Equal(code, competency.Code); Assert.Equal(name, competency.Name);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "unknown_competency");
    }

    [Fact]
    public void Normalizer_CsvCompetenciesAreUniqueAndDuplicateTokensProduceOneCompetency()
    {
        var tokens = CsvCompetencies.Select(item => (string)item[0]).ToArray();
        var result = new EmployeeImportRowNormalizer().Normalize(Source() with { TechnicalSkillsRaw = string.Join("; ", tokens.Append(tokens[0])), TechnologiesAndToolsRaw = null });

        Assert.Equal(tokens.Length, result.Competencies.Count);
        Assert.Equal(result.Competencies.Count, result.Competencies.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(result.Competencies.Count, result.Competencies.Select(item => item.Code).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, result.Competencies.Count(item => item.Name == "AWS"));
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code == "unknown_competency");
    }

    public static IEnumerable<object[]> CsvCompetencies =>
    [
        ["AWS", "AWS", "AWS"], ["Linux", "LINUX", "Linux"], ["Olay güdümlü mimari", "OLAY_GUDUMLU_MIMARI", "Olay güdümlü mimari"], ["SOLID prensipleri", "SOLID_PRENSIPLERI", "SOLID prensipleri"], ["Postman", "POSTMAN", "Postman"], ["REST API geliştirme", "REST_API_GELISTIRME", "REST API geliştirme"], ["Veri tabanı tasarımı", "VERI_TABANI_TASARIMI", "Veri tabanı tasarımı"], ["GitLab CI", "GITLAB_CI", "GitLab CI"], ["Tasarım desenleri", "TASARIM_DESENLERI", "Tasarım desenleri"], ["Dağıtık sistemler", "DAGITIK_SISTEMLER", "Dağıtık sistemler"], ["Nginx", "NGINX", "Nginx"], ["Grafana", "GRAFANA", "Grafana"], ["Elasticsearch", "ELASTICSEARCH", "Elasticsearch"], ["API güvenliği", "API_GUVENLIGI", "API güvenliği"], ["Swagger", "SWAGGER", "Swagger"], ["Güvenli kodlama", "GUVENLI_KODLAMA", "Güvenli kodlama"], ["Önbellekleme stratejileri", "ONBELLEKLEME_STRATEJILERI", "Önbellekleme stratejileri"], ["Clean Code", "CLEAN_CODE", "Clean Code"], ["Loglama ve izleme", "LOGLAMA_VE_IZLEME", "Loglama ve izleme"], ["Prometheus", "PROMETHEUS", "Prometheus"], ["GitHub Actions", "GITHUB_ACTIONS", "GitHub Actions"], ["Azure", "AZURE", "Azure"], ["Performans optimizasyonu", "PERFORMANS_OPTIMIZASYONU", "Performans optimizasyonu"], ["Sistem tasarımı", "SISTEM_TASARIMI", "Sistem tasarımı"], ["Test otomasyonu", "TEST_OTOMASYONU", "Test otomasyonu"], ["Asenkron programlama", "ASENKRON_PROGRAMLAMA", "Asenkron programlama"], ["Ktor", "KTOR", "Ktor"], ["Express.js", "EXPRESS_JS", "Express.js"], ["FastAPI", "FASTAPI", "FastAPI"], ["LINQ", "LINQ", "LINQ"], ["Fiber", "FIBER", "Fiber"], ["Entity Framework", "ENTITY_FRAMEWORK", "Entity Framework"], [".NET Core", "DOTNET_CORE", ".NET Core"], ["SQLAlchemy", "SQLALCHEMY", "SQLAlchemy"], ["GORM", "GORM", "GORM"], ["Celery", "CELERY", "Celery"], ["Sequelize", "SEQUELIZE", "Sequelize"], ["Gin", "GIN", "Gin"], ["Symfony", "SYMFONY", "Symfony"], ["Laravel", "LARAVEL", "Laravel"], ["Maven", "MAVEN", "Maven"], ["Spring Security", "SPRING_SECURITY", "Spring Security"], ["Composer", "COMPOSER", "Composer"], ["Hibernate", "HIBERNATE", "Hibernate"], ["Gradle", "GRADLE", "Gradle"], ["NestJS", "NESTJS", "NestJS"], ["TypeORM", "TYPEORM", "TypeORM"], ["Prisma", "PRISMA", "Prisma"]
    ];
    [Theory]
    [InlineData("5.2 yıl", 62)]
    [InlineData("5,2 yıl", 62)]
    [InlineData("2 yıl 6 ay", 30)]
    [InlineData("18 ay", 18)]
    public void DurationParser_ParsesSupportedValues(string input, int expected) =>
        Assert.Equal(expected, DurationToMonthsParser.Parse(input).Months);

    [Theory]
    [InlineData("-1 yıl")]
    [InlineData("unknown")]
    public void DurationParser_RejectsNegativeAndInvalidValues(string input) =>
        Assert.False(DurationToMonthsParser.Parse(input).IsValid);

    [Fact]
    public void DelimitedParser_PreservesOrderDeduplicatesAndKeepsCommas()
    {
        var result = DelimitedEvidenceParser.Parse(" Git; PostgreSQL, Redis\nGit; Docker ");
        Assert.Equal(["Git", "PostgreSQL, Redis", "Docker"], result);
    }

    [Fact]
    public void Normalizer_NormalizesCompetenciesSectorsCertificatesLanguagesAndWorkModes()
    {
        var row = Source() with { TechnicalSkillsRaw = "C Sharp; JS; Java", TechnologiesAndToolsRaw = "Postgres; MSSQL; C#", SectorExperienceRaw = "ERP (5.2 yıl); ERP; Telekomünikasyon (invalid)", CertificatesRaw = "Yok; AWS; Azure", ForeignLanguagesRaw = "Türkçe (Ana dil); İngilizce C1; İngilizce B2", WorkModeExperienceRaw = "Ofis, hibrit ve uzaktan çalışma deneyimi", EducationLevelRaw = "Lisans" };
        var result = new EmployeeImportRowNormalizer().Normalize(row);
        Assert.Equal(["C#", "JavaScript", "Java", "PostgreSQL", "SQL Server"], result.Competencies.Select(x => x.Name));
        Assert.DoesNotContain(result.Competencies, x => x.Name == "Java" && x.Name == "JavaScript");
        Assert.Equal(62, result.SectorExperiences[0].ExperienceMonths);
        Assert.Equal(["AWS", "Azure"], result.Certificates);
        Assert.True(result.Languages.Single(x => x.Code == "TR").IsNative);
        Assert.Equal(LanguageProficiencyLevel.C1, result.Languages.Single(x => x.Code == "EN").ProficiencyLevel);
        Assert.Equal(["ON_SITE", "HYBRID", "REMOTE"], result.WorkModes.Select(x => x.Code));
        Assert.Equal(DegreeLevel.Bachelor, result.EducationLevel);
        Assert.Contains(result.Diagnostics, x => x.Code == "conflicting_language_level");
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_sector_duration");
    }

    [Fact]
    public void Normalizer_UnknownEducationLanguageAndLabel_DoNotInventValues()
    {
        var result = new EmployeeImportRowNormalizer().Normalize(Source() with { EducationLevelRaw = "Unknown", ForeignLanguagesRaw = "Klingon Z9", StayLabel = 9 });
        Assert.Null(result.EducationLevel); Assert.Null(result.StayLabel);
        Assert.Contains(result.Diagnostics, x => x.Code == "unknown_education_level");
        Assert.Contains(result.Diagnostics, x => x.Code == "language_level_missing");
        Assert.Contains(result.Diagnostics, x => x.Code == "invalid_stay_label");
    }

    [Fact]
    public void Validator_EnforcesCriticalRulesAndAllowsProductionWithoutLabel()
    {
        var normalized = new EmployeeImportRowNormalizer().Normalize(Source() with { AnonymousEmployeeCode = null, HireDate = null, BackendExperienceYears = 6, TotalExperienceYears = 5, TerminationDate = new DateOnly(2019, 1, 1), ShortestPreviousJobMonths = 10, LongestPreviousJobMonths = 2, CompanyChangeCount = -1, StayLabel = null });
        var training = new EmployeeImportRowValidator().Validate(normalized, new(EmployeeDatasetSplit.Training, null));
        var production = new EmployeeImportRowValidator().Validate(new EmployeeImportRowNormalizer().Normalize(Source() with { StayLabel = null }), new(EmployeeDatasetSplit.Production, new DateOnly(2025, 1, 1)));
        Assert.Equal(EmployeeImportValidationStatus.Invalid, training.Status);
        Assert.Contains(training.Diagnostics, x => x.Code == "stay_label_required");
        Assert.NotEqual(EmployeeImportValidationStatus.Invalid, production.Status);
    }

    [Fact]
    public void NormalizerAndValidator_PreserveFractionalAverageStayAndRejectOnlyNegativeValues()
    {
        var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator();
        var fractional = normalizer.Normalize(Source() with { PreviousCompanyAverageStayMonths = 12.5m, ShortestPreviousJobMonths = 2, LongestPreviousJobMonths = 6 });
        var zero = normalizer.Normalize(Source() with { PreviousCompanyAverageStayMonths = 0m });
        var negative = normalizer.Normalize(Source() with { PreviousCompanyAverageStayMonths = -0.5m });
        var missing = normalizer.Normalize(Source() with { PreviousCompanyAverageStayMonths = null });

        Assert.Equal(12.5m, fractional.PreviousCompanyAverageStayMonths); Assert.Equal(2, fractional.ShortestPreviousJobMonths); Assert.Equal(6, fractional.LongestPreviousJobMonths);
        Assert.Equal(EmployeeImportValidationStatus.Valid, validator.Validate(fractional, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1))).Status);
        Assert.Equal(EmployeeImportValidationStatus.Valid, validator.Validate(zero, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1))).Status);
        var invalid = validator.Validate(negative, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1)));
        Assert.Equal(EmployeeImportValidationStatus.Invalid, invalid.Status); Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "previous_company_average_stay_months_negative");
        Assert.Equal(EmployeeImportValidationStatus.Valid, validator.Validate(missing, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1))).Status);
    }

    [Fact]
    public async Task DryRun_FractionalAverageStayIsValidAndDoesNotProduceIntegerDiagnostics()
    {
        var reader = new StubReader([Source() with { PreviousCompanyAverageStayMonths = 29.7m }]);
        var result = await new EmployeeImportDryRunService(reader, new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()).DryRunAsync(Stream.Null, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1)));

        Assert.True(result.IsSuccess); Assert.Equal(1, result.Value.ValidRows); Assert.Equal(0, result.Value.InvalidRows); Assert.DoesNotContain(result.Value.Rows.Single().Diagnostics, diagnostic => diagnostic.Code == "invalid_integer_value" || diagnostic.Code == "invalid_decimal_value");
    }

    [Fact]
    public async Task DryRun_OrchestratesRowsAndAggregatesWithoutPersistence()
    {
        var reader = new StubReader([Source(), Source(3) with { AnonymousEmployeeCode = null }]);
        var service = new EmployeeImportDryRunService(reader, new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator());
        using var stream = new MemoryStream();
        var result = await service.DryRunAsync(stream, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1)));
        Assert.True(result.IsSuccess); Assert.Equal(2, result.Value.TotalRows); Assert.Equal(1, result.Value.InvalidRows); Assert.Equal(1, result.Value.ValidRows); Assert.Equal(2, result.Value.CompetencyCount);
    }

    [Fact]
    public async Task DryRun_PropagatesCancellationAndHasNoDbContextDependency()
    {
        var service = new EmployeeImportDryRunService(new StubReader([Source()]), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator());
        using var stream = new MemoryStream(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DryRunAsync(stream, new(EmployeeDatasetSplit.Training, null), cancel.Token));
        Assert.DoesNotContain(typeof(EmployeeImportDryRunService).GetConstructors().Single().GetParameters(), p => p.ParameterType.Name.Contains("DbContext"));
    }

    private static EmployeeImportSourceRow Source(int row = 2) => new(row, "EMP-001", "Backend Developer", 5, 4, "Developer", "C#", null, "Project", "ERP (1 yıl)", "Lisans", "Computer Science", "AWS", "İngilizce C1", "Ofis", new DateOnly(2020, 1, 1), null, 5, 12, 2, 6, 4, 1, .2m, 1, new Dictionary<string, string?>(), []);
    private sealed class StubReader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); } }
}
