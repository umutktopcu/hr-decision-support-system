namespace HrDecisionSupport.Tests.Web;

public sealed class PositionBenchmarkEditPageTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();
    private static readonly string EditView = File.ReadAllText(Path.Combine(
        RepositoryRoot,
        "src",
        "HrDecisionSupport.Web",
        "Views",
        "JobRequisition",
        "Edit.cshtml"));
    private static readonly string SharedScript = File.ReadAllText(Path.Combine(
        RepositoryRoot,
        "src",
        "HrDecisionSupport.Web",
        "wwwroot",
        "js",
        "job-requisition.js"));

    [Fact]
    public void EditView_InitializesBenchmarkOnlyAfterPersistedStateIsLoaded()
    {
        var persistedRequirements = EditView.IndexOf("(detail.requirements || []).forEach", StringComparison.Ordinal);
        var persistedLanguages = EditView.IndexOf("(detail.languageRequirements || []).forEach", StringComparison.Ordinal);
        var benchmarkInitialization = EditView.IndexOf("initPositionBenchmark({", StringComparison.Ordinal);

        Assert.True(persistedRequirements >= 0);
        Assert.True(persistedLanguages > persistedRequirements);
        Assert.True(benchmarkInitialization > persistedLanguages);
        Assert.Contains("mandatoryStore,", EditView);
        Assert.Contains("preferredStore,", EditView);
        Assert.Contains("languageStore,", EditView);
        Assert.Contains("experienceInputEl: document.getElementById('minimumRelevantExperienceMonths')", EditView);
        Assert.Contains("educationSelectEl: eduSel", EditView);
    }

    [Fact]
    public void EditView_HasExplicitAnalyzeActionWithoutAutomaticBenchmarkRequest()
    {
        Assert.Contains("id=\"analyzePositionBtn\"", EditView);
        Assert.Contains("Pozisyonu Analiz Et", EditView);
        Assert.Contains("id=\"positionBenchmarkContainer\" class=\"d-none\"", EditView);

        var beforeInitialization = Section(EditView, "(async function ()", "initPositionBenchmark({");
        Assert.DoesNotContain("/api/position-benchmarks/", beforeInitialization);
        Assert.Contains("analyzeButtonEl.addEventListener('click', analyzeBenchmark)", SharedScript);
    }

    [Fact]
    public void EditView_GatesAssistantApplyForActualTerminalStatuses()
    {
        Assert.Contains("detail.jobRequisitionStatus !== 4", EditView);
        Assert.Contains("detail.jobRequisitionStatus !== 5", EditView);
        Assert.Contains("canApplySuggestions: () => benchmarkApplyAllowed", EditView);
        Assert.Contains("if (!canApplySuggestions())", SharedScript);
        Assert.Contains("Düzenleme kilitli", SharedScript);
    }

    [Fact]
    public void EditView_PreservesPersistedRequirementDetailsInNormalPayload()
    {
        var payload = Section(EditView, "const payload = {", "try {");

        Assert.Contains("minimumExperienceMonths: r.minimumExperienceMonths", EditView);
        Assert.Contains("minimumProficiencyLevel: r.minimumProficiencyLevel", EditView);
        Assert.Contains("notes: r.notes", EditView);
        Assert.Contains("minimumExperienceMonths: s.minimumExperienceMonths ?? null", payload);
        Assert.Contains("minimumProficiencyLevel: s.minimumProficiencyLevel ?? null", payload);
        Assert.Contains("notes: s.notes ?? null", payload);
        Assert.DoesNotContain("benchmark", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suggestion", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedBenchmark_InvalidatesOnPositionChangeWithoutAutomaticAnalysis()
    {
        var invalidation = Section(
            SharedScript,
            "function invalidateBenchmark()",
            "containerEl.addEventListener('click', event =>");

        Assert.Contains("positionSelectEl.addEventListener('change', invalidateBenchmark)", SharedScript);
        Assert.Contains("containerEl.classList.add('d-none')", invalidation);
        Assert.DoesNotContain("fetch(", invalidation);
        Assert.DoesNotContain("preferredStore.splice", invalidation);
        Assert.DoesNotContain("languageStore.splice", invalidation);
    }

    [Fact]
    public void SharedBenchmark_UsesSingleDelegatedHandlerAndGuardsAllApplyActions()
    {
        var handler = Section(
            SharedScript,
            "containerEl.addEventListener('click', event =>",
            "async function analyzeBenchmark()");

        Assert.Equal(1, CountOccurrences(SharedScript, "containerEl.addEventListener('click', event =>"));
        Assert.Contains("data-benchmark-apply-skill", handler);
        Assert.Contains("data-benchmark-apply-experience", handler);
        Assert.Contains("data-benchmark-apply-education", handler);
        Assert.Contains("data-benchmark-apply-language", handler);
        Assert.Contains("if (!canApplySuggestions())", handler);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count += 1;
            index += value.Length;
        }

        return count;
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
