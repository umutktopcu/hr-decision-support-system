namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed record PersonCertificateDto(
    Guid Id,
    Guid PersonId,
    Guid CertificateId,
    string CertificateCode,
    string CertificateName,
    string? Issuer,
    DateOnly? IssueDate,
    DateOnly? ExpirationDate,
    string? CredentialCode);
