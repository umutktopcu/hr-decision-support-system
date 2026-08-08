using System;
using System.Collections.Generic;

using HrDecisionSupport.Application.SemanticMatching.Documents;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Documents;

public class CandidateDocumentBuilderTests
{
    private readonly CandidateDocumentBuilder _sut;

    public CandidateDocumentBuilderTests()
    {
        _sut = new CandidateDocumentBuilder();
    }

    private Candidate CreateCandidate()
    {
        return new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            CandidateCode = "C1",
            CandidateSource = CandidateSource.Other,
            ProfessionalTitle = "Backend Developer",
            AvailabilityDays = 15, // Should not leak
            Person = new Person
            {
                Id = Guid.NewGuid(),
                AnonymousCode = "A1",
                CreatedAtUtc = DateTime.UtcNow
            },
            WorkModePreferences = new List<CandidateWorkModePreference>
            {
                new CandidateWorkModePreference { WorkMode = new WorkMode { Name = "Uzaktan" } } // Should not leak
            },
            EvaluationCases = new List<CandidateEvaluationCase>
            {
                new CandidateEvaluationCase { Status = CandidateEvaluationStatus.Approved } // Should not leak
            }
        };
    }

    [Fact]
    public void BuildCandidateDocument_DeterministicOutput_And_CorrectSectionOrdering()
    {
        var candidate = CreateCandidate();
        
        // Professional Profile
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = DateTime.UtcNow,
            FeatureSchemaVersion = "1.0",
            TotalExperienceMonths = 40,
            BackendExperienceMonths = 20,
            JobChangeRate = 1.5m, // Should not leak
            CompanyChangeCount = 3 // Should not leak
        });

        // Employment History
        candidate.Person.EmploymentHistories.Add(new EmploymentHistory
        {
            PositionTitle = "Dev",
            EmployerName = "Company A",
            StartDate = new DateOnly(2023, 1, 1),
            EndDate = new DateOnly(2024, 1, 1),
            Description = "Did some work"
        });

        // Skills
        candidate.Person.PersonCompetencies.Add(new PersonCompetency
        {
            Competency = new Competency { Name = "C#" }
        });

        // Projects
        candidate.Person.PersonProjects.Add(new PersonProject
        {
            Project = new Project { Name = "Not rendered", Description = "My API Project" },
            Role = "Should not leak"
        });

        // Education
        candidate.Person.EducationRecords.Add(new EducationRecord
        {
            DegreeLevel = DegreeLevel.Bachelor,
            FieldOfStudy = "Computer Science",
            Institution = "MIT"
        });

        // Certificates
        candidate.Person.PersonCertificates.Add(new PersonCertificate
        {
            Certificate = new Certificate { Name = "AWS", Issuer = "Amazon" }
        });

        // Languages
        candidate.Person.PersonLanguages.Add(new PersonLanguage
        {
            Language = new Language { Name = "English" },
            ProficiencyLevel = LanguageProficiencyLevel.B2,
            IsNative = false
        });

        // Sectors
        candidate.Person.PersonSectorExperiences.Add(new PersonSectorExperience
        {
            Sector = new Sector { Name = "Finance" },
            ExperienceMonths = 12
        });

        var result1 = _sut.BuildCandidateDocument(candidate);
        var result2 = _sut.BuildCandidateDocument(candidate);

        // Same input gives exact same string
        Assert.Equal(result1, result2);

        // Verify correct ordering of sections
        var expectedOrder = new[]
        {
            "PROFESSIONAL PROFILE",
            "EMPLOYMENT HISTORY",
            "SKILLS",
            "PROJECTS",
            "EDUCATION",
            "CERTIFICATES",
            "LANGUAGES",
            "SECTOR EXPERIENCE"
        };

        var indices = new List<int>();
        int lastIdx = -1;
        foreach (var section in expectedOrder)
        {
            var idx = result1.IndexOf(section);
            Assert.True(idx > -1, $"Section {section} not found.");
            Assert.True(idx > lastIdx, $"Section {section} out of order.");
            lastIdx = idx;
        }
    }

    [Fact]
    public void BuildCandidateDocument_EmploymentLatestFirstOrdering()
    {
        var candidate = CreateCandidate();
        candidate.ProfessionalTitle = null; // Omit profile
        
        candidate.Person.EmploymentHistories.Add(new EmploymentHistory
        {
            PositionTitle = "Old Job",
            EmployerName = "Company A",
            StartDate = new DateOnly(2020, 1, 1),
            EndDate = new DateOnly(2021, 1, 1)
        });
        candidate.Person.EmploymentHistories.Add(new EmploymentHistory
        {
            PositionTitle = "New Job",
            EmployerName = "Company B",
            StartDate = new DateOnly(2022, 1, 1),
            EndDate = null
        });

        var result = _sut.BuildCandidateDocument(candidate);

        var newJobIdx = result.IndexOf("New Job");
        var oldJobIdx = result.IndexOf("Old Job");

        Assert.True(newJobIdx < oldJobIdx, "New job should appear before old job");
    }

    [Fact]
    public void BuildCandidateDocument_NullEmploymentDescriptionOmitted_PopulatedIncluded()
    {
        var candidate = CreateCandidate();
        candidate.Person.EmploymentHistories.Add(new EmploymentHistory
        {
            PositionTitle = "Job1",
            EmployerName = "A",
            StartDate = new DateOnly(2020, 1, 1),
            Description = "Populated description"
        });
        candidate.Person.EmploymentHistories.Add(new EmploymentHistory
        {
            PositionTitle = "Job2",
            EmployerName = "B",
            StartDate = new DateOnly(2021, 1, 1),
            Description = null
        });

        var result = _sut.BuildCandidateDocument(candidate);

        Assert.Contains("Populated description", result);
        Assert.DoesNotContain("Description: null", result);
        Assert.DoesNotContain("Description: \n", result);
        Assert.DoesNotMatch(@"Description:\s*$", result);
    }

    [Fact]
    public void BuildCandidateDocument_ProjectRules_NullOmitted_NameNotRendered_DuplicateRenderedOnce()
    {
        var candidate = CreateCandidate();
        
        // Null description
        candidate.Person.PersonProjects.Add(new PersonProject
        {
            Project = new Project { Name = "NameOnly", Description = null }
        });
        
        // Empty description
        candidate.Person.PersonProjects.Add(new PersonProject
        {
            Project = new Project { Name = "NameOnly2", Description = "  " }
        });

        // Valid description
        candidate.Person.PersonProjects.Add(new PersonProject
        {
            Project = new Project { Name = "Name1", Description = "Valid Project" }
        });

        // Duplicate valid description
        candidate.Person.PersonProjects.Add(new PersonProject
        {
            Project = new Project { Name = "Name2", Description = "Valid Project " }
        });

        var result = _sut.BuildCandidateDocument(candidate);

        Assert.DoesNotContain("NameOnly", result);
        Assert.DoesNotContain("NameOnly2", result);
        Assert.DoesNotContain("Name1", result);
        Assert.DoesNotContain("Name2", result);
        
        Assert.Contains("- Valid Project", result);
        
        // Should only appear once (plus heading)
        var count = System.Text.RegularExpressions.Regex.Matches(result, "Valid Project").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void BuildCandidateDocument_DuplicateCanonicalCompetencyRenderedOnce()
    {
        var candidate = CreateCandidate();
        var compId = Guid.NewGuid();
        
        candidate.Person.PersonCompetencies.Add(new PersonCompetency
        {
            CompetencyId = compId,
            Competency = new Competency { Id = compId, Name = "C#" }
        });
        candidate.Person.PersonCompetencies.Add(new PersonCompetency
        {
            CompetencyId = compId,
            Competency = new Competency { Id = compId, Name = "C#" }
        });

        var result = _sut.BuildCandidateDocument(candidate);

        var count = System.Text.RegularExpressions.Regex.Matches(result, "C#").Count;
        Assert.Equal(1, count);
    }

    [Fact]
    public void BuildCandidateDocument_EmptySectionsOmitted()
    {
        var candidate = CreateCandidate();
        candidate.ProfessionalTitle = null; // No profile
        // All collections are empty

        var result = _sut.BuildCandidateDocument(candidate);

        Assert.Empty(result);
        Assert.DoesNotContain("PROJECTS", result);
        Assert.DoesNotContain("CERTIFICATES", result);
        Assert.DoesNotContain("SKILLS", result);
        Assert.DoesNotContain("EDUCATION", result);
    }

    [Fact]
    public void BuildCandidateDocument_ExclusionsNotRendered()
    {
        var candidate = CreateCandidate();
        
        var result = _sut.BuildCandidateDocument(candidate);

        Assert.DoesNotContain("15", result); // Availability
        Assert.DoesNotContain("Uzaktan", result); // WorkMode
        Assert.DoesNotContain("Approved", result); // Status
    }

    [Fact]
    public void BuildCandidateDocument_EducationAndLanguageNumericCodeNotLeaked()
    {
        var candidate = CreateCandidate();
        candidate.Person.EducationRecords.Add(new EducationRecord
        {
            DegreeLevel = DegreeLevel.Master,
            FieldOfStudy = "Math"
        });
        candidate.Person.PersonLanguages.Add(new PersonLanguage
        {
            Language = new Language { Name = "German" },
            ProficiencyLevel = LanguageProficiencyLevel.C1
        });
        candidate.Person.PersonLanguages.Add(new PersonLanguage
        {
            Language = new Language { Name = "Turkish" },
            IsNative = true
        });

        var result = _sut.BuildCandidateDocument(candidate);

        Assert.DoesNotContain("4", result); // Numeric Master
        Assert.DoesNotContain("Master", result); // Should be localized mapping, assuming FormatDegreeLevel returns "Yüksek Lisans"
        Assert.Contains("Yüksek Lisans", result);

        Assert.DoesNotContain("5", result); // Numeric C1
        Assert.Contains("C1", result);

        Assert.Contains("- Turkish (Ana dil)", result);
    }

    [Fact]
    public void BuildCandidateDocument_SnapshotVersionRules()
    {
        var candidate = CreateCandidate();
        var time1 = DateTime.UtcNow.AddDays(-2);
        var time2 = DateTime.UtcNow.AddDays(-1);
        
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = time1,
            FeatureSchemaVersion = "10.0",
            TotalExperienceMonths = 100
        });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = time2,
            FeatureSchemaVersion = "2.0",
            TotalExperienceMonths = 20
        });

        var result = _sut.BuildCandidateDocument(candidate);
        Assert.Contains("100 months", result); // 10.0 wins over 2.0 even though older
        Assert.DoesNotContain("20 months", result);
    }

    [Fact]
    public void BuildCandidateDocument_SnapshotVersionRules_SameVersionNewerWins()
    {
        var candidate = CreateCandidate();
        var time1 = DateTime.UtcNow.AddDays(-2);
        var time2 = DateTime.UtcNow.AddDays(-1);
        
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = time1,
            FeatureSchemaVersion = "2.0",
            TotalExperienceMonths = 10
        });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = time2,
            FeatureSchemaVersion = "2.0",
            TotalExperienceMonths = 20
        });

        var result = _sut.BuildCandidateDocument(candidate);
        Assert.Contains("20 months", result);
        Assert.DoesNotContain("10 months", result);
    }

    [Fact]
    public void BuildCandidateDocument_EducationDeterministicSecondaryOrdering()
    {
        var candidate1 = CreateCandidate();
        candidate1.Person.EducationRecords.Add(new EducationRecord { DegreeLevel = DegreeLevel.Bachelor, FieldOfStudy = "B", Institution = "X" });
        candidate1.Person.EducationRecords.Add(new EducationRecord { DegreeLevel = DegreeLevel.Bachelor, FieldOfStudy = "A", Institution = "Y" });

        var candidate2 = CreateCandidate();
        candidate2.Person.EducationRecords.Add(new EducationRecord { DegreeLevel = DegreeLevel.Bachelor, FieldOfStudy = "A", Institution = "Y" });
        candidate2.Person.EducationRecords.Add(new EducationRecord { DegreeLevel = DegreeLevel.Bachelor, FieldOfStudy = "B", Institution = "X" });

        var result1 = _sut.BuildCandidateDocument(candidate1);
        var result2 = _sut.BuildCandidateDocument(candidate2);

        Assert.Equal(result1, result2);
        
        var expectedOrder = $"EDUCATION{Environment.NewLine}- Lisans — A — Y{Environment.NewLine}- Lisans — B — X";
        Assert.Contains(expectedOrder, result1);
    }

    [Fact]
    public void BuildCandidateDocument_UndefinedEnumsOmitted()
    {
        var candidate = CreateCandidate();
        
        // Undefined DegreeLevel => omitted (100 is undefined)
        candidate.Person.EducationRecords.Add(new EducationRecord { DegreeLevel = (DegreeLevel)100, FieldOfStudy = "Unknown" });
        
        // Undefined LanguageProficiencyLevel => language prints without proficiency (100 is undefined)
        candidate.Person.PersonLanguages.Add(new PersonLanguage { Language = new Language { Name = "Spanish" }, ProficiencyLevel = (LanguageProficiencyLevel)100 });

        var result = _sut.BuildCandidateDocument(candidate);

        Assert.DoesNotContain("100", result);
        Assert.DoesNotContain("Unknown Field", result); // Because whole education is omitted
        Assert.DoesNotContain("EDUCATION", result);
        Assert.Contains("- Spanish", result);
    }

    [Fact]
    public void BuildCandidateDocument_SnapshotVersionRules_MalformedVersionFallsBack()
    {
        var candidate = CreateCandidate();
        
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = DateTime.UtcNow,
            FeatureSchemaVersion = "garbage", // malformed
            TotalExperienceMonths = 5
        });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            CalculatedAtUtc = DateTime.UtcNow.AddMinutes(-1),
            FeatureSchemaVersion = "1.0",
            TotalExperienceMonths = 10
        });

        var result = _sut.BuildCandidateDocument(candidate);
        
        // 1.0 > garbage (0.0), so 1.0 wins
        Assert.Contains("10 months", result);
    }
}
