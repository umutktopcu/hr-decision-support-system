using System;
using System.Linq;
using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Domain.Extensions;

public static class CandidateExtensions
{
    public static CandidateCareerFeatureSnapshot? GetLatestFeatureSnapshot(this Candidate candidate)
    {
        if (candidate.CareerFeatureSnapshots == null || !candidate.CareerFeatureSnapshots.Any())
        {
            return null;
        }

        return candidate.CareerFeatureSnapshots
            .OrderByDescending(s => Version.TryParse(s.FeatureSchemaVersion, out var v) ? v : new Version(0, 0))
            .ThenByDescending(s => s.CalculatedAtUtc)
            .FirstOrDefault();
    }
}
