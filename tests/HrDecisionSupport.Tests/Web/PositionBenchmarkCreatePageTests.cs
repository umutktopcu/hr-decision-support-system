namespace HrDecisionSupport.Tests.Web;

public sealed class PositionBenchmarkCreatePageTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string CreateView = File.ReadAllText(Path.Combine(
        RepositoryRoot,
        "src",
        "HrDecisionSupport.Web",
        "Views",
        "JobRequisition",
        "Create.cshtml"));
    private static readonly string SharedScript = File.ReadAllText(Path.Combine(
        RepositoryRoot,
        "src",
        "HrDecisionSupport.Web",
        "wwwroot",
        "js",
        "job-requisition.js"));

    [Fact]
    public void CreateView_HasExplicitInitiallyDisabledAnalyzeAction()
    {
        Assert.Contains("id=\"analyzePositionBtn\"", CreateView);
        Assert.Contains("Pozisyonu Analiz Et", CreateView);
        Assert.Contains("class=\"btn btn-outline-primary\" disabled", CreateView);
        Assert.Contains("id=\"positionBenchmarkContainer\" class=\"d-none\"", CreateView);
        Assert.Contains("initPositionBenchmark({", CreateView);
    }

    [Fact]
    public void Client_RequestsOnlyOnExplicitClickAndPositionChangeInvalidates()
    {
        Assert.Contains("analyzeButtonEl.addEventListener('click', analyzeBenchmark)", SharedScript);
        Assert.Contains("positionSelectEl.addEventListener('change', invalidateBenchmark)", SharedScript);
        Assert.Contains("fetch(`/api/position-benchmarks/${encodeURIComponent(requestedPositionId)}`", SharedScript);

        var invalidation = Section(SharedScript, "function invalidateBenchmark()", "async function analyzeBenchmark()");
        Assert.DoesNotContain("fetch(", invalidation);
        Assert.Contains("containerEl.classList.add('d-none')", invalidation);
        Assert.Contains("analyzeButtonEl.disabled = !positionSelectEl.value", invalidation);
    }

    [Fact]
    public void Client_ProtectsAgainstDuplicateAndStaleRequests()
    {
        Assert.Contains("if (!requestedPositionId || analyzeButtonEl.disabled) return", SharedScript);
        Assert.Contains("abortController.abort()", SharedScript);
        Assert.Contains("version !== requestVersion || positionSelectEl.value !== requestedPositionId", SharedScript);
        Assert.Contains("signal: abortController.signal", SharedScript);
    }

    [Fact]
    public void Client_RendersAllSampleStatesCoverageAndInformationalSuggestions()
    {
        Assert.Contains("analiz edilebilecek aktif çalışan bulunamadı", SharedScript);
        Assert.Contains("Sınırlı veri", SharedScript);
        Assert.Contains("Yeterli örneklem", SharedScript);
        Assert.Contains("Skill profili bulunan", SharedScript);
        Assert.Contains("Eğitim profili bulunan", SharedScript);
        Assert.Contains("Dil profili bulunan", SharedScript);
        Assert.Contains("Verisi bulunan", SharedScript);
        Assert.Contains("Önerilen Gereksinimler", SharedScript);
        Assert.Contains("Yalnızca seçtiğiniz öneriler forma uygulanır", SharedScript);
    }

    [Fact]
    public void BenchmarkLoad_DoesNotApplySuggestionsAutomatically()
    {
        var analyze = Section(
            SharedScript,
            "async function analyzeBenchmark()",
            "positionSelectEl.addEventListener('change', invalidateBenchmark)");

        Assert.DoesNotContain("preferredStore.push", analyze);
        Assert.DoesNotContain("languageStore.push", analyze);
        Assert.DoesNotContain("experienceInputEl.value =", analyze);
        Assert.DoesNotContain("educationSelectEl.value =", analyze);
    }

    [Fact]
    public void SkillApply_UsesStableIdentityAndPreservesMandatoryRequirements()
    {
        var helper = Section(
            SharedScript,
            "function applyPreferredCompetencySuggestion(",
            "function languageProficiencyRank(value)");
        var apply = Section(
            SharedScript,
            "containerEl.addEventListener('click', event =>",
            "async function analyzeBenchmark()");

        Assert.Contains("data-benchmark-apply-skill", SharedScript);
        Assert.Contains("type=\"button\"", SharedScript);
        Assert.Contains("applyPreferredCompetencySuggestion(suggestion, mandatoryStore, preferredStore)", apply);
        Assert.Contains("mandatoryStore.some(item => item.competencyId === competencyId)", helper);
        Assert.Contains("preferredStore.some(item => item.competencyId === competencyId)", helper);
        Assert.Contains("preferredStore.push({", helper);
        Assert.Contains("isRequired: false", helper);
        Assert.Contains("Zaten zorunlu", SharedScript);
        Assert.Contains("Zaten eklendi", SharedScript);
    }

    [Fact]
    public void ExperienceApply_OnlyFillsOrStrengthensAfterClick()
    {
        var apply = Section(
            SharedScript,
            "const experienceButton = event.target.closest('[data-benchmark-apply-experience]')",
            "const educationButton = event.target.closest('[data-benchmark-apply-education]')");

        Assert.Contains("current === null || current < suggested", apply);
        Assert.Contains("experienceInputEl.value = suggested", apply);
        Assert.Contains("current > suggested", SharedScript);
        Assert.Contains("Mevcut değer daha sıkı", SharedScript);
        Assert.Contains("Kullanılıyor", SharedScript);
    }

    [Fact]
    public void EducationApply_UsesDomainRankAndTreatsOtherAsRankZero()
    {
        var rank = Section(
            SharedScript,
            "function educationLevelRank(value)",
            "function languageProficiencyRank(value)");
        var apply = Section(
            SharedScript,
            "const educationButton = event.target.closest('[data-benchmark-apply-education]')",
            "const languageButton = event.target.closest('[data-benchmark-apply-language]')");

        Assert.Contains("1:1, 2:2, 3:3, 4:4, 5:5, 99:0", rank);
        Assert.Contains("currentRank < suggestedRank", apply);
        Assert.Contains("suggested !== 99", apply);
        Assert.Contains("educationSelectEl.value = String(suggested)", apply);
    }

    [Fact]
    public void LanguageApply_PreventsDuplicatesUpgradesOnlyAndPreservesHardFlag()
    {
        var apply = Section(
            SharedScript,
            "const languageButton = event.target.closest('[data-benchmark-apply-language]')",
            "async function analyzeBenchmark()");

        Assert.Contains("languageStore.find(item => item.languageId === languageId)", apply);
        Assert.Contains("languageStore.push({", apply);
        Assert.Contains("hardFilterEnabled: false", apply);
        Assert.Contains("< languageProficiencyRank(suggestion.minimumProficiency)", apply);
        Assert.Contains("existing.minimumProficiency = suggestion.minimumProficiency", apply);
        Assert.DoesNotContain("existing.hardFilterEnabled =", apply);
    }

    [Fact]
    public void SuggestionStates_TrackCurrentFormAndPositionChangeWarnsWithoutCleanup()
    {
        var invalidation = Section(
            SharedScript,
            "function invalidateBenchmark()",
            "containerEl.addEventListener('click', event =>");

        Assert.Contains("requisition-form-state-changed", SharedScript);
        Assert.Contains("benchmarkSuggestionApplied", invalidation);
        Assert.Contains("Pozisyon değişti", invalidation);
        Assert.DoesNotContain("preferredStore.splice", invalidation);
        Assert.DoesNotContain("languageStore.splice", invalidation);
        Assert.DoesNotContain("fetch(", invalidation);
    }

    [Fact]
    public void CreatePayload_RemainsBenchmarkFree()
    {
        var payload = Section(CreateView, "const payload = {", "try {");
        Assert.DoesNotContain("benchmark", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suggestion", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("...preferredStore.map", payload);
        Assert.Contains("minimumRelevantExperienceMonths", payload);
        Assert.Contains("minimumEducationLevel", payload);
        Assert.Contains("languageRequirements: languageStore.map", payload);
    }

    [Fact]
    public void FailureMessage_IsRetryableAndDoesNotExposeExceptionText()
    {
        var benchmarkClient = Section(
            SharedScript,
            "function initPositionBenchmark(cfg)",
            "function renderPositionBenchmark(result)");

        Assert.Contains("Pozisyon benchmarkı alınamadı. Lütfen tekrar deneyin.", benchmarkClient);
        Assert.DoesNotContain("error.message", benchmarkClient);
    }

    private static string Section(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Start marker was not found: {startMarker}");
        Assert.True(end > start, $"End marker was not found after start marker: {endMarker}");
        return source[start..end];
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HrDecisionSupport.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located.");
    }
}
