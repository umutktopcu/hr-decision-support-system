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
        Assert.Contains("Bilgilendirme amaçlıdır; forma otomatik uygulanmaz", SharedScript);
    }

    [Fact]
    public void BenchmarkClient_DoesNotModifyRequisitionFormState()
    {
        var benchmarkClient = Section(
            SharedScript,
            "// Position benchmark display (Create page only)",
            "function thresholdToDisplay(decimal)");

        Assert.DoesNotContain("mandatoryStore", benchmarkClient);
        Assert.DoesNotContain("preferredStore", benchmarkClient);
        Assert.DoesNotContain("languageStore", benchmarkClient);
        Assert.DoesNotContain("getElementById('minimumRelevantExperienceMonths')", benchmarkClient);
        Assert.DoesNotContain("getElementById('minimumEducationLevel')", benchmarkClient);
        Assert.DoesNotContain("getElementById('workModeId')", benchmarkClient);
    }

    [Fact]
    public void CreatePayload_RemainsBenchmarkFree()
    {
        var payload = Section(CreateView, "const payload = {", "try {");
        Assert.DoesNotContain("benchmark", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("suggestion", payload, StringComparison.OrdinalIgnoreCase);
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
