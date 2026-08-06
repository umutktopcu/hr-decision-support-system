using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed record DurationParseResult(int? Months, bool IsValid);

public static class DurationToMonthsParser
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public static DurationParseResult Parse(decimal? years) => years is null ? new(null, true) : ToMonths(years.Value);
    public static DurationParseResult Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new(null, true);
        var text = value.Trim();
        var match = Regex.Match(text, @"^(?<y>[-+]?\d+(?:[\.,]\d+)?)\s*yıl(?:\s+(?<m>[-+]?\d+)\s*ay)?$|^(?<only>[-+]?\d+(?:[\.,]\d+)?)\s*ay$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (match.Groups["only"].Success && TryDecimal(match.Groups["only"].Value, out var months)) return ToIntegerMonths(months);
            var extra = 0;
            if (TryDecimal(match.Groups["y"].Value, out var years)
                && (!match.Groups["m"].Success || int.TryParse(match.Groups["m"].Value, out extra)))
                return AddMonths(years, extra);
        }
        if (TryDecimal(text, out var numeric)) return ToMonths(numeric);
        return new(null, false);
    }
    private static DurationParseResult AddMonths(decimal years, int extra) => years < 0 || extra < 0 ? new(null, false) : new((int)Math.Round(years * 12, MidpointRounding.AwayFromZero) + extra, true);
    private static DurationParseResult ToMonths(decimal years) => years < 0 ? new(null, false) : new((int)Math.Round(years * 12, MidpointRounding.AwayFromZero), true);
    private static DurationParseResult ToIntegerMonths(decimal months) => months < 0 || decimal.Truncate(months) != months ? new(null, false) : new((int)months, true);
    private static bool TryDecimal(string value, out decimal parsed) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed) || decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Tr, out parsed);
}

public static class DelimitedEvidenceParser
{
    public static IReadOnlyList<string> Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var part in value.Split([';', '\r', '\n']))
        {
            var normalized = Regex.Replace(part.Normalize(NormalizationForm.FormC).Trim(), "\\s+", " ");
            if (normalized.Length != 0 && seen.Add(normalized)) result.Add(normalized);
        }
        return result;
    }
}
