using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class EducationRecord
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string? Institution { get; set; }
    public string? FieldOfStudy { get; set; }
    public DegreeLevel DegreeLevel { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? GraduationDate { get; set; }

    public Person Person { get; set; } = null!;
}
