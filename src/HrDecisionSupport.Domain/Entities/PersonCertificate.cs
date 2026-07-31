namespace HrDecisionSupport.Domain.Entities;

public class PersonCertificate
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid CertificateId { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpirationDate { get; set; }
    public string? CredentialCode { get; set; }

    public Person Person { get; set; } = null!;
    public Certificate Certificate { get; set; } = null!;
}
