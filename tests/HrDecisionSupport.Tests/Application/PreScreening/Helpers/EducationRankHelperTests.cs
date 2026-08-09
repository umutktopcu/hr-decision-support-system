using System;
using HrDecisionSupport.Application.PreScreening.Helpers;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Xunit;

namespace HrDecisionSupport.Tests.Application.PreScreening.Helpers;

public class EducationRankHelperTests
{
    [Theory]
    [InlineData(DegreeLevel.HighSchool, 1)]
    [InlineData(DegreeLevel.Associate, 2)]
    [InlineData(DegreeLevel.Bachelor, 3)]
    [InlineData(DegreeLevel.Master, 4)]
    [InlineData(DegreeLevel.Doctorate, 5)]
    [InlineData(DegreeLevel.Other, 0)]
    public void GetEducationRank_ReturnsExpectedRank(DegreeLevel level, int expectedRank)
    {
        var rank = EducationRankHelper.GetEducationRank(level);
        Assert.Equal(expectedRank, rank);
    }

    [Fact]
    public void GetHighestEducationRank_NullCandidate_ReturnsZero()
    {
        var rank = EducationRankHelper.GetHighestEducationRank(null);
        Assert.Equal(0, rank);
    }

    [Fact]
    public void GetHighestEducationRank_NullPerson_ReturnsZero()
    {
        var candidate = new Candidate { Person = null };
        var rank = EducationRankHelper.GetHighestEducationRank(candidate);
        Assert.Equal(0, rank);
    }

    [Fact]
    public void GetHighestEducationRank_EmptyRecords_ReturnsZero()
    {
        var candidate = new Candidate { Person = new Person() };
        var rank = EducationRankHelper.GetHighestEducationRank(candidate);
        Assert.Equal(0, rank);
    }

    [Fact]
    public void GetHighestEducationRank_ValidRecords_ReturnsHighestRank()
    {
        var candidate = new Candidate
        {
            Person = new Person
            {
                EducationRecords = new List<EducationRecord>
                {
                    new EducationRecord { DegreeLevel = DegreeLevel.HighSchool },
                    new EducationRecord { DegreeLevel = DegreeLevel.Master },
                    new EducationRecord { DegreeLevel = DegreeLevel.Bachelor }
                }
            }
        };

        var rank = EducationRankHelper.GetHighestEducationRank(candidate);
        Assert.Equal(4, rank); // Master
    }
}
