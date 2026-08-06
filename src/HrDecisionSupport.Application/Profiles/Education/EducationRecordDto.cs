using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed record EducationRecordDto(
    Guid Id,
    Guid PersonId,
    string? Institution,
    string? FieldOfStudy,
    DegreeLevel DegreeLevel,
    DateOnly? StartDate,
    DateOnly? GraduationDate);
