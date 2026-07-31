using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class PersonLanguage
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid LanguageId { get; set; }
    public ProficiencyLevel ProficiencyLevel { get; set; }
    public bool IsNative { get; set; }

    public Person Person { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
