using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed record UpdateEducationRecordRequest(
    string Institution,
    string? FieldOfStudy,
    DegreeLevel DegreeLevel,
    DateOnly? StartDate,
    DateOnly? GraduationDate);
