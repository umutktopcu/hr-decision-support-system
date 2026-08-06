using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrDecisionSupport.Tests;

/// <summary>Counts only commands issued while an import is being measured; fixture setup is deliberately excluded.</summary>
public sealed class PostgreSqlCommandCounter : DbCommandInterceptor
{
    private readonly Dictionary<string, int> _reader = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _nonQuery = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _scalar = new(StringComparer.Ordinal);
    private readonly List<string> _commands = [];
    public bool IsMeasuring { get; private set; }
    public int ReaderCount => _reader.Values.Sum();
    public int NonQueryCount => _nonQuery.Values.Sum();
    public int ScalarCount => _scalar.Values.Sum();
    public int ProfileLookupCount => ProfileLookupCategories.Sum(category => this[category]);
    public int this[string category] => _reader.GetValueOrDefault(category) + _nonQuery.GetValueOrDefault(category) + _scalar.GetValueOrDefault(category);
    public void Start() { _reader.Clear(); _nonQuery.Clear(); _scalar.Clear(); _commands.Clear(); IsMeasuring = true; }
    public void Stop() => IsMeasuring = false;
    public string Describe() => string.Join(", ", _commands.GroupBy(x => x).OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Count()));

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) { Count(_reader, command); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) { Count(_reader, command); return ValueTask.FromResult(result); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result) { Count(_nonQuery, command); return result; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) { Count(_nonQuery, command); return ValueTask.FromResult(result); }
    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result) { Count(_scalar, command); return result; }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default) { Count(_scalar, command); return ValueTask.FromResult(result); }

    private void Count(Dictionary<string, int> bucket, DbCommand command)
    {
        if (!IsMeasuring) return;
        var sql = command.CommandText;
        if (sql.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase) || sql.Contains("TRUNCATE TABLE", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("BEGIN", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase) || sql.StartsWith("ROLLBACK", StringComparison.OrdinalIgnoreCase)) return;
        var kind = ReferenceEquals(bucket, _reader) ? "reader" : ReferenceEquals(bucket, _nonQuery) ? "nonquery" : "scalar";
        var lower = sql.ToLowerInvariant();
        var category = lower.Contains("insert into") || lower.Contains("update ") || lower.Contains("delete from") ? "insert/update"
            : lower.Contains("\"employees\"") || lower.Contains("\"people\"") ? "employee/person lookup"
            : lower.Contains("\"competencies\"") ? "competency lookup"
            : ProfileLookupCategories.FirstOrDefault(table => lower.Contains("\"" + table + "\"")) ?? "other";
        bucket[category] = bucket.GetValueOrDefault(category) + 1;
        _commands.Add(kind + ":" + category);
    }

    private static readonly string[] ProfileLookupCategories = [
        "employee_assignments", "person_prior_position_evidences", "person_competencies", "person_projects",
        "person_sector_experiences", "education_records", "person_certificates", "person_languages", "person_work_mode_experiences"
    ];
}
