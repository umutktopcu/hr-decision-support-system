using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed record CreateEducationRecordRequest(
    Guid PersonId,
    string Institution,
    string? FieldOfStudy,
    DegreeLevel DegreeLevel,
    DateOnly? StartDate,
    DateOnly? GraduationDate);
