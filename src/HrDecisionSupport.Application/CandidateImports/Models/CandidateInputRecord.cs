using System;

namespace HrDecisionSupport.Application.CandidateImports.Models;

public sealed record CandidateInputRecord(
    int RowNumber,
    string AnonymousCode,
    string? TargetPosition,
    string? ProfessionalTitle,
    decimal? TotalExperienceYears,
    decimal? BackendExperienceYears,
    string? PreviousPositions,
    string? PreviousCompanies,
    string? PreviousDates,
    string? TechnicalSkills,
    string? FrameworksAndDbs,
    string? Projects,
    string? EducationLevel,
    string? EducationField,
    string? Certificates,
    string? Languages,
    string? SectorExperience,
    string? WorkModePreference,
    int? AvailabilityDays
);
