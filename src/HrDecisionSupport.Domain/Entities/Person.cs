namespace HrDecisionSupport.Domain.Entities;

public class Person
{
    public Guid Id { get; set; }
    public string AnonymousCode { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Employee? Employee { get; set; }
    public Candidate? Candidate { get; set; }
    public ICollection<EmploymentHistory> EmploymentHistories { get; set; } = [];
    public ICollection<PersonCompetency> PersonCompetencies { get; set; } = [];
    public ICollection<EducationRecord> EducationRecords { get; set; } = [];
    public ICollection<PersonCertificate> PersonCertificates { get; set; } = [];
    public ICollection<PersonLanguage> PersonLanguages { get; set; } = [];
    public ICollection<PersonPriorPositionEvidence> PriorPositionEvidences { get; set; } = [];
    public ICollection<PersonProject> PersonProjects { get; set; } = [];
    public ICollection<PersonSectorExperience> PersonSectorExperiences { get; set; } = [];
    public ICollection<PersonWorkModeExperience> PersonWorkModeExperiences { get; set; } = [];
}
