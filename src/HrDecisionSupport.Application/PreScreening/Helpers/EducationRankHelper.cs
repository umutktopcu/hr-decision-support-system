using System;
using System.Linq;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.PreScreening.Helpers;

public static class EducationRankHelper
{
    public static int GetEducationRank(DegreeLevel level)
    {
        return level switch
        {
            DegreeLevel.HighSchool => 1,
            DegreeLevel.Associate => 2,
            DegreeLevel.Bachelor => 3,
            DegreeLevel.Master => 4,
            DegreeLevel.Doctorate => 5,
            DegreeLevel.Other => 0, // Other is generally not comparable or below HighSchool
            _ => 0
        };
    }

    public static int GetHighestEducationRank(Candidate candidate)
    {
        if (candidate?.Person?.EducationRecords == null || !candidate.Person.EducationRecords.Any())
            return 0;

        return candidate.Person.EducationRecords.Max(e => GetEducationRank(e.DegreeLevel));
    }
}
