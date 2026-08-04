namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed record CreatePersonCertificateRequest(
    Guid PersonId,
    Guid CertificateId,
    DateOnly? IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialCode);
