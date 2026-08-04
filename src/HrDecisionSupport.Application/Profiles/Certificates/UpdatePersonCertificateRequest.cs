namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed record UpdatePersonCertificateRequest(
    DateOnly? IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialCode);
