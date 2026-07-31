namespace HrDecisionSupport.Domain.Entities;

public class Language
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<PersonLanguage> PersonLanguages { get; set; } = [];
}
