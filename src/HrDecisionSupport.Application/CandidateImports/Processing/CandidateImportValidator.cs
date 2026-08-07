using System.Collections.Generic;
using HrDecisionSupport.Application.CandidateImports.Models;

namespace HrDecisionSupport.Application.CandidateImports.Processing;

public sealed class CandidateImportValidator
{
    public IReadOnlyList<CandidateImportError> Validate(CandidateInputRecord record)
    {
        var errors = new List<CandidateImportError>();

        if (string.IsNullOrWhiteSpace(record.ProfessionalTitle))
        {
            errors.Add(new CandidateImportError(record.RowNumber, nameof(record.ProfessionalTitle), "Mesleki unvan (ProfessionalTitle) zorunludur."));
        }

        if (!record.AvailabilityDays.HasValue)
        {
            errors.Add(new CandidateImportError(record.RowNumber, nameof(record.AvailabilityDays), "İşe başlayabilme süresi (AvailabilityDays) zorunludur."));
        }
        else if (record.AvailabilityDays.Value < 0)
        {
            errors.Add(new CandidateImportError(record.RowNumber, nameof(record.AvailabilityDays), "İşe başlayabilme süresi negatif olamaz."));
        }

        if (record.TotalExperienceYears.HasValue && record.TotalExperienceYears.Value < 0)
        {
            errors.Add(new CandidateImportError(record.RowNumber, nameof(record.TotalExperienceYears), "Toplam deneyim yılı negatif olamaz."));
        }

        if (record.BackendExperienceYears.HasValue && record.BackendExperienceYears.Value < 0)
        {
            errors.Add(new CandidateImportError(record.RowNumber, nameof(record.BackendExperienceYears), "Backend deneyim yılı negatif olamaz."));
        }

        if (record.TotalExperienceYears.HasValue && record.BackendExperienceYears.HasValue)
        {
            if (record.BackendExperienceYears.Value > record.TotalExperienceYears.Value)
            {
                errors.Add(new CandidateImportError(record.RowNumber, nameof(record.BackendExperienceYears), "Backend deneyimi toplam deneyimden büyük olamaz."));
            }
        }

        // We can add basic checks for string lengths or date formats here, but keeping it simple as requested.
        return errors;
    }
}
