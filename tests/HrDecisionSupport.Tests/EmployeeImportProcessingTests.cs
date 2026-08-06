using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class EmployeeImportProcessingTests
{
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
        var row = Source() with { TechnicalSkillsRaw="C Sharp; JS; Java", TechnologiesAndToolsRaw="Postgres; MSSQL; C#", SectorExperienceRaw="ERP (5.2 yıl); ERP; Telekomünikasyon (invalid)", CertificatesRaw="Yok; AWS; Azure", ForeignLanguagesRaw="Türkçe (Ana dil); İngilizce C1; İngilizce B2", WorkModeExperienceRaw="Ofis, hibrit ve uzaktan çalışma deneyimi", EducationLevelRaw="Lisans" };
        var result = new EmployeeImportRowNormalizer().Normalize(row);
        Assert.Equal(["C#", "JavaScript", "Java", "PostgreSQL", "SQL Server"], result.Competencies.Select(x=>x.Name));
        Assert.DoesNotContain(result.Competencies, x => x.Name == "Java" && x.Name == "JavaScript");
        Assert.Equal(62, result.SectorExperiences[0].ExperienceMonths);
        Assert.Equal(["AWS", "Azure"], result.Certificates);
        Assert.True(result.Languages.Single(x=>x.Code=="TR").IsNative);
        Assert.Equal(LanguageProficiencyLevel.C1,result.Languages.Single(x=>x.Code=="EN").ProficiencyLevel);
        Assert.Equal(["ON_SITE","HYBRID","REMOTE"],result.WorkModes.Select(x=>x.Code));
        Assert.Equal(DegreeLevel.Bachelor,result.EducationLevel);
        Assert.Contains(result.Diagnostics,x=>x.Code=="conflicting_language_level");
        Assert.Contains(result.Diagnostics,x=>x.Code=="invalid_sector_duration");
    }

    [Fact]
    public void Normalizer_UnknownEducationLanguageAndLabel_DoNotInventValues()
    {
        var result = new EmployeeImportRowNormalizer().Normalize(Source() with { EducationLevelRaw="Unknown", ForeignLanguagesRaw="Klingon Z9", StayLabel=9 });
        Assert.Null(result.EducationLevel); Assert.Null(result.StayLabel);
        Assert.Contains(result.Diagnostics,x=>x.Code=="unknown_education_level");
        Assert.Contains(result.Diagnostics,x=>x.Code=="language_level_missing");
        Assert.Contains(result.Diagnostics,x=>x.Code=="invalid_stay_label");
    }

    [Fact]
    public void Validator_EnforcesCriticalRulesAndAllowsProductionWithoutLabel()
    {
        var normalized=new EmployeeImportRowNormalizer().Normalize(Source() with { AnonymousEmployeeCode=null,HireDate=null,BackendExperienceYears=6,TotalExperienceYears=5,TerminationDate=new DateOnly(2019,1,1),ShortestPreviousJobMonths=10,LongestPreviousJobMonths=2,CompanyChangeCount=-1,StayLabel=null });
        var training=new EmployeeImportRowValidator().Validate(normalized,new(EmployeeDatasetSplit.Training,null));
        var production=new EmployeeImportRowValidator().Validate(new EmployeeImportRowNormalizer().Normalize(Source() with { StayLabel=null }),new(EmployeeDatasetSplit.Production,new DateOnly(2025,1,1)));
        Assert.Equal(EmployeeImportValidationStatus.Invalid,training.Status);
        Assert.Contains(training.Diagnostics,x=>x.Code=="stay_label_required");
        Assert.NotEqual(EmployeeImportValidationStatus.Invalid,production.Status);
    }

    [Fact]
    public async Task DryRun_OrchestratesRowsAndAggregatesWithoutPersistence()
    {
        var reader=new StubReader([Source(),Source(3) with { AnonymousEmployeeCode=null }]);
        var service=new EmployeeImportDryRunService(reader,new EmployeeImportRowNormalizer(),new EmployeeImportRowValidator());
        using var stream=new MemoryStream();
        var result=await service.DryRunAsync(stream,new(EmployeeDatasetSplit.Training,new DateOnly(2025,1,1)));
        Assert.True(result.IsSuccess); Assert.Equal(2,result.Value.TotalRows); Assert.Equal(1,result.Value.InvalidRows); Assert.Equal(1,result.Value.ValidRows); Assert.Equal(2,result.Value.CompetencyCount);
    }

    [Fact]
    public async Task DryRun_PropagatesCancellationAndHasNoDbContextDependency()
    {
        var service=new EmployeeImportDryRunService(new StubReader([Source()]),new EmployeeImportRowNormalizer(),new EmployeeImportRowValidator());
        using var stream=new MemoryStream(); using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.DryRunAsync(stream,new(EmployeeDatasetSplit.Training,null),cancel.Token));
        Assert.DoesNotContain(typeof(EmployeeImportDryRunService).GetConstructors().Single().GetParameters(),p=>p.ParameterType.Name.Contains("DbContext"));
    }

    private static EmployeeImportSourceRow Source(int row=2)=>new(row,"EMP-001","Backend Developer",5,4,"Developer","C#",null,"Project","ERP (1 yıl)","Lisans","Computer Science","AWS","İngilizce C1","Ofis",new DateOnly(2020,1,1),null,5,12,2,6,4,1,.2m,1,new Dictionary<string,string?>(),[]);
    private sealed class StubReader(IReadOnlyList<EmployeeImportSourceRow> rows):IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content,CancellationToken cancellationToken=default){cancellationToken.ThrowIfCancellationRequested();return Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees",[],rows,[])));} }
}
