using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Candidates.Dtos;

public sealed record CandidateListItemDto(
    Guid Id,
    string CandidateCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    CandidateSource CandidateSource,
    string? ExternalCandidateId,

    int? TotalExperienceYears = null,
    List<string>? Skills = null,
    double? MatchScore = null,
    string? ProfessionalTitle = null
);

public sealed record CandidateDetailsDto(
    Guid Id,
    Guid PersonId,
    string CandidateCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    CandidateSource CandidateSource,
    string? ExternalCandidateId,
    int EvaluationCaseCount,
    int? ShortestJobMonths = null,
    int? LongestJobMonths = null);


public sealed record CreateCandidateRequest(
    string CandidateCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    CandidateSource CandidateSource,
    string? ExternalCandidateId);

public sealed record UpdateCandidateRequest(
    string CandidateCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    CandidateSource CandidateSource,
    string? ExternalCandidateId);
