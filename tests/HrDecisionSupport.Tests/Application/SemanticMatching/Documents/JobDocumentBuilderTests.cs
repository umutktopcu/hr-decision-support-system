using System;
using System.Collections.Generic;

using HrDecisionSupport.Application.SemanticMatching.Documents;
using HrDecisionSupport.Domain.Entities;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Documents;

public class JobDocumentBuilderTests
{
    private readonly JobDocumentBuilder _sut;

    public JobDocumentBuilderTests()
    {
        _sut = new JobDocumentBuilder();
    }

    [Fact]
    public void BuildJobDocument_JobDescriptionIncluded_ThresholdsNotRendered_RequirementSeparationCorrect()
    {
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "BD-TEST-001",
            Title = "Backend Developer",
            Description = "A great job.",
            MinimumRelevantExperienceMonths = 24,
            MandatorySkillCoverageThreshold = 0.50m, // Should not leak
            Position = new Position { Name = "Backend Developer" },
            Requirements = new List<JobRequisitionRequirement>
            {
                new JobRequisitionRequirement
                {
                    IsRequired = true,
                    Competency = new Competency { Name = "C#" }
                },
                new JobRequisitionRequirement
                {
                    IsRequired = false,
                    Competency = new Competency { Name = "ASP.NET Core" }
                }
            }
        };

        var result = _sut.BuildJobDocument(job);

        // Core fields
        Assert.Contains("JOB PROFILE", result);
        Assert.Contains("Title: Backend Developer", result);
        Assert.Contains("Position: Backend Developer", result);
        Assert.Contains("DESCRIPTION\r\nA great job.", result);
        Assert.Contains("Minimum Relevant Experience: 24 months", result);
        
        // Separation of mandatory and preferred
        Assert.Contains("MANDATORY SKILLS\r\n- C#", result);
        Assert.Contains("PREFERRED SKILLS\r\n- ASP.NET Core", result);

        // Exclusions
        Assert.DoesNotContain("0.50", result);
        Assert.DoesNotContain("BD-TEST-001", result);
    }

    [Fact]
    public void BuildJobDocument_DeterministicOutput()
    {
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            Title = "Backend Developer",
            Description = "A great job.",
            Requirements = new List<JobRequisitionRequirement>
            {
                new JobRequisitionRequirement
                {
                    IsRequired = true,
                    Competency = new Competency { Name = "Z_Language" }
                },
                new JobRequisitionRequirement
                {
                    IsRequired = true,
                    Competency = new Competency { Name = "A_Language" }
                }
            }
        };

        var result1 = _sut.BuildJobDocument(job);
        var result2 = _sut.BuildJobDocument(job);

        Assert.Equal(result1, result2);
        
        // Ordering check (A should come before Z)
        var aIdx = result1.IndexOf("A_Language");
        var zIdx = result1.IndexOf("Z_Language");
        Assert.True(aIdx < zIdx, "A should come before Z");
    }
}
