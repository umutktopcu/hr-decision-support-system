using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.CandidateImports.Processing;

public sealed class CandidateFeatureCalculator
{
    public CandidateFeatures CalculateFeatures(
        decimal? totalExperienceYears,
        string? previousPositions,
        string? previousCompanies,
        string? previousDates)
    {
        var positions = DelimitedEvidenceParser.Parse(previousPositions);
        var companies = DelimitedEvidenceParser.Parse(previousCompanies);
        var dates = DelimitedEvidenceParser.Parse(previousDates);

        var historyItems = new List<EmploymentHistoryItem>();

        // We match by index up to the minimum count of the three arrays
        int count = Math.Min(positions.Count, Math.Min(companies.Count, dates.Count));

        for (int i = 0; i < count; i++)
        {
            var parsedDates = ParseDates(dates[i]);
            if (parsedDates != null)
            {
                var durationMonths = CalculateDurationMonths(parsedDates.Value.Start, parsedDates.Value.End);
                historyItems.Add(new EmploymentHistoryItem(
                    positions[i],
                    companies[i],
                    parsedDates.Value.Start,
                    parsedDates.Value.End,
                    durationMonths
                ));
            }
        }

        decimal? averageStay = null;
        int? shortestStay = null;
        int? longestStay = null;
        int? lastStay = null;
        int companyChangeCount = companies.Count; // As per requirement: CompanyChangeCount = PreviousCompanies.Count
        decimal? jobChangeRate = null;

        if (historyItems.Any())
        {
            averageStay = Math.Round((decimal)historyItems.Average(x => x.DurationMonths), 1);
            shortestStay = historyItems.Min(x => x.DurationMonths);
            longestStay = historyItems.Max(x => x.DurationMonths);

            // Last stay: chronological latest (EndDate DESC, StartDate DESC)
            var latestItem = historyItems
                .OrderByDescending(x => x.EndDate)
                .ThenByDescending(x => x.StartDate)
                .First();
            
            lastStay = latestItem.DurationMonths;
        }

        if (totalExperienceYears.HasValue && totalExperienceYears.Value > 0)
        {
            jobChangeRate = Math.Round(companyChangeCount / totalExperienceYears.Value, 2);
        }
        else if (totalExperienceYears.HasValue && totalExperienceYears.Value == 0)
        {
            jobChangeRate = 0;
        }

        return new CandidateFeatures(
            historyItems,
            averageStay,
            shortestStay,
            longestStay,
            lastStay,
            companyChangeCount,
            jobChangeRate
        );
    }

    private (DateOnly Start, DateOnly End)? ParseDates(string dateString)
    {
        // Expected format MM.YYYY-MM.YYYY
        var match = Regex.Match(dateString.Trim(), @"^(\d{2})\.(\d{4})\s*-\s*(\d{2})\.(\d{4})$");
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int startMonth) &&
                int.TryParse(match.Groups[2].Value, out int startYear) &&
                int.TryParse(match.Groups[3].Value, out int endMonth) &&
                int.TryParse(match.Groups[4].Value, out int endYear))
            {
                try
                {
                    var start = new DateOnly(startYear, startMonth, 1);
                    var end = new DateOnly(endYear, endMonth, 1);
                    return (start, end);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // Invalid month/year
                    return null;
                }
            }
        }
        return null;
    }

    private int CalculateDurationMonths(DateOnly start, DateOnly end)
    {
        return (end.Year - start.Year) * 12 + (end.Month - start.Month);
    }
}

public sealed record EmploymentHistoryItem(
    string PositionName,
    string CompanyName,
    DateOnly StartDate,
    DateOnly EndDate,
    int DurationMonths
);

public sealed record CandidateFeatures(
    IReadOnlyList<EmploymentHistoryItem> HistoryItems,
    decimal? PreviousCompanyAverageStayMonths,
    int? ShortestPreviousJobMonths,
    int? LongestPreviousJobMonths,
    int? LastPreviousCompanyStayMonths,
    int CompanyChangeCount,
    decimal? JobChangeRate
);
