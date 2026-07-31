namespace HrDecisionSupport.Domain.Entities;

public class Certificate
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Issuer { get; set; }

    public ICollection<PersonCertificate> PersonCertificates { get; set; } = [];
}
