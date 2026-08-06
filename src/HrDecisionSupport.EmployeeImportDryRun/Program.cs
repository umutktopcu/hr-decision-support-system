using System.Diagnostics;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Infrastructure.EmployeeImports;

namespace HrDecisionSupport.EmployeeImportDryRun;

internal static class Program
{
    private const string FileVariable = "HRDS_EMPLOYEE_IMPORT_FILE";
    public static async Task<int> Main(string[] args)
    {
        if (args.Length > 0 && string.Equals(args[0], "import", StringComparison.OrdinalIgnoreCase))
            return await ImportAsync(args);
        if (args.Length > 0 && string.Equals(args[0], "backfill-termination-dates", StringComparison.OrdinalIgnoreCase))
            return await BackfillTerminationDatesAsync(args);
        if (args.Length > 0 && string.Equals(args[0], "backfill-competencies", StringComparison.OrdinalIgnoreCase))
            return await BackfillCompetenciesAsync(args);
        if (args.Length > 0 && string.Equals(args[0], "audit-competency-backfill", StringComparison.OrdinalIgnoreCase))
            return await AuditCompetencyBackfillAsync(args);
        if (args.Length > 0 && string.Equals(args[0], "analyze", StringComparison.OrdinalIgnoreCase))
            return await AnalyzeAsync(args);
        if (args.Length == 0 || !string.Equals(args[0], "dry-run", StringComparison.OrdinalIgnoreCase)) return Fail("Usage: dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- dry-run [--observation-date yyyy-MM-dd]\n       dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- analyze --file <workbook.xlsx> --observation-date yyyy-MM-dd\n       dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- import --observation-date yyyy-MM-dd [--confirm-import <row-count>]\n       dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- backfill-termination-dates --batch-id <guid> [--confirm-backfill <candidate-count>]\n       dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- backfill-competencies --batch-id <guid> [--confirm-competencies <count> --confirm-links <count>]");
        var filePath = Environment.GetEnvironmentVariable(FileVariable);
        if (string.IsNullOrWhiteSpace(filePath)) return Fail($"{FileVariable} is not set.");
        if (!File.Exists(filePath)) return Fail($"The file configured by {FileVariable} does not exist.");
        if (!TryObservationDate(args, out var observationDate, out var error)) return Fail(error!);
        using var cancellation = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try
        {
            var reader = new TimingReader(new ClosedXmlEmployeeSpreadsheetReader()); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator(); var service = new EmployeeImportDryRunService(reader, normalizer, validator);
            var memoryStart = GC.GetTotalMemory(false); var timer = Stopwatch.StartNew(); await using var stream = File.OpenRead(filePath);
            var result = await service.DryRunAsync(stream, new(HrDecisionSupport.Domain.Enums.EmployeeDatasetSplit.Training, observationDate), cancellation.Token); timer.Stop();
            if (result.IsFailure) return Fail(result.Error!.Message);
            var report = DryRunReportWriter.Build(result.Value, Path.GetFileName(filePath), timer.Elapsed, reader.Elapsed, memoryStart, GC.GetTotalMemory(false));
            var output = Environment.GetEnvironmentVariable("HRDS_EMPLOYEE_IMPORT_OUTPUT") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HrDecisionSupport", "employee-import-dry-run");
            await DryRunReportWriter.WriteAsync(report, result.Value, output, cancellation.Token);
            Console.WriteLine($"Dry-run completed: {report.TotalRows} rows; valid={report.ValidRows}; warnings={report.ValidWithWarningsRows}; invalid={report.InvalidRows}.");
            Console.WriteLine("Top diagnostics: " + string.Join("; ", report.Diagnostics.Take(10).Select(diagnostic => diagnostic.Code + "=" + diagnostic.Count)));
            Console.WriteLine($"Reports written to: {output}");
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Dry-run cancelled."); return 2; }
        catch (Exception exception) { return Fail("Dry-run failed: " + exception.Message); }
    }

    private static async Task<int> ImportAsync(string[] args)
    {
        var filePath = Environment.GetEnvironmentVariable(FileVariable);
        if (!TryObservationDate(args, out var observationDate, out var error)) return Fail(error!);
        var options = new ControlledImportOptions(filePath, observationDate, Option(args, "--confirm-import"), Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
        await using var execution = new PostgreSqlEmployeeImportExecution(Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        return await new ControlledEmployeeImportCommand(execution, new ConsoleImportConfirmation(), new ConsoleImportOutput()).ExecuteAsync(options, cancellation.Token);
    }

    private static async Task<int> BackfillTerminationDatesAsync(string[] args)
    {
        var batchText = Option(args, "--batch-id"); var batchId = Guid.TryParse(batchText, out var parsed) ? parsed : (Guid?)null;
        var options = new TerminationDateBackfillOptions(batchId, Option(args, "--confirm-backfill"), Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
        await using var execution = new PostgreSqlTerminationDateBackfillExecution(Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        using var cancellation = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        return await new TerminationDateBackfillCommand(execution, new ConsoleImportConfirmation(), new ConsoleImportOutput()).ExecuteAsync(options, cancellation.Token);
    }

    private static async Task<int> BackfillCompetenciesAsync(string[] args)
    {
        var batchText = Option(args, "--batch-id"); var batchId = Guid.TryParse(batchText, out var parsed) ? parsed : (Guid?)null;
        var options = new CompetencyBackfillOptions(batchId, Option(args, "--confirm-competencies"), Option(args, "--confirm-links"), Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production");
        await using var execution = new PostgreSqlCompetencyBackfillExecution(Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        using var cancellation = new CancellationTokenSource(); Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        return await new CompetencyBackfillCommand(execution, new ConsoleCompetencyBackfillConfirmation(), new ConsoleImportOutput()).ExecuteAsync(options, cancellation.Token);
    }

    private static async Task<int> AuditCompetencyBackfillAsync(string[] args)
    {
        var batchText = Option(args, "--batch-id"); var batchId = Guid.TryParse(batchText, out var parsed) ? parsed : (Guid?)null;
        var options = new CompetencyBackfillAuditOptions(batchId, Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"), Environment.GetEnvironmentVariable(FileVariable));
        return await new CompetencyBackfillAuditCommand(new PostgreSqlCompetencyBackfillAuditExecution(), new ConsoleImportOutput()).ExecuteAsync(options);
    }

    private static async Task<int> AnalyzeAsync(string[] args)
    {
        var filePath = Option(args, "--file");
        if (string.IsNullOrWhiteSpace(filePath)) return Fail("--file is required for analyze.");
        if (!File.Exists(filePath)) return Fail("The analysis workbook does not exist.");
        if (!TryObservationDate(args, out var observationDate, out var error)) return Fail(error!);
        if (observationDate is null) return Fail("--observation-date is required for analyze so the report records that this is a temporary analysis date.");

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
        try
        {
            var output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HrDecisionSupport", "employee-import-dry-run", "analysis");
            var report = await EmployeeImportWorkbookAnalysis.AnalyzeAsync(filePath, observationDate.Value, cancellation.Token);
            await EmployeeImportWorkbookAnalysis.WriteAsync(report, output, cancellation.Token);
            Console.WriteLine($"Analysis completed: {report.Workbook.FileName}; rows={report.Workbook.DataRowCount}; invalid-decimal={report.PreviousCompanyAverageStay.InvalidDecimalDiagnosticCount}; unknown-competencies={report.UnknownCompetencies.TotalOccurrenceCount}.");
            Console.WriteLine($"Average-stay classification: numeric-integer={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.NumericInteger)]}; numeric-fractional={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.NumericFractional)]}; comma-decimal-text={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.CommaDecimalText)]}; dot-decimal-text={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.DotDecimalText)]}; invalid-text={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.InvalidText)]}; empty={report.PreviousCompanyAverageStay.Classifications[nameof(AverageStayValueClass.Empty)]}.");
            Console.WriteLine($"Average-stay statistics: min={report.PreviousCompanyAverageStay.Min}; max={report.PreviousCompanyAverageStay.Max}; median={report.PreviousCompanyAverageStay.Median}; average={report.PreviousCompanyAverageStay.Average}; negative={report.PreviousCompanyAverageStay.NegativeCount}; zero={report.PreviousCompanyAverageStay.ZeroCount}; precision={string.Join("|", report.PreviousCompanyAverageStay.FractionalPrecisionDistribution.Select(pair => pair.Key + ":" + pair.Value))}.");
            Console.WriteLine($"Unknown-competency aggregate: distinct={report.UnknownCompetencies.DistinctTokenCount}; phrase-review={report.UnknownCompetencies.DescriptionOrPhraseReviewCount}; manual-review={report.UnknownCompetencies.ManualReviewCount}.");
            Console.WriteLine($"Analysis reports written to: {output}");
            return 0;
        }
        catch (OperationCanceledException) { Console.Error.WriteLine("Analysis cancelled."); return 2; }
        catch (Exception exception) { return Fail("Analysis failed: " + exception.Message); }
    }

    private static string? Option(IReadOnlyList<string> args, string name)
    {
        var index = Array.IndexOf(args.ToArray(), name);
        return index >= 0 && index + 1 < args.Count ? args[index + 1] : null;
    }
    private static bool TryObservationDate(string[] args, out DateOnly? date, out string? error)
    {
        date = null; error = null; var index = Array.IndexOf(args, "--observation-date"); if (index < 0) return true; if (index + 1 >= args.Length || !DateOnly.TryParse(args[index + 1], out var value)) { error = "--observation-date must be a valid date."; return false; } date = value; return true;
    }
    private static int Fail(string message) { Console.Error.WriteLine(message); return 1; }
    private sealed class TimingReader(IEmployeeSpreadsheetReader inner) : IEmployeeSpreadsheetReader
    {
        public TimeSpan Elapsed { get; private set; }
        public async Task<HrDecisionSupport.Application.Common.Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) { var timer = Stopwatch.StartNew(); var result = await inner.ReadAsync(content, cancellationToken); timer.Stop(); Elapsed += timer.Elapsed; return result; }
    }
}
