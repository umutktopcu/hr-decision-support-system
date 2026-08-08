using System.Linq;
using System.Text;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Extensions;

namespace HrDecisionSupport.Application.SemanticMatching.Documents;

public class CandidateDocumentBuilder : ISemanticDocumentBuilder
{
    public string BuildCandidateDocument(Candidate candidate)
    {
        var sb = new StringBuilder();

        BuildProfessionalProfile(sb, candidate);
        BuildEmploymentHistory(sb, candidate);
        BuildSkills(sb, candidate);
        BuildProjects(sb, candidate);
        BuildEducation(sb, candidate);
        BuildCertificates(sb, candidate);
        BuildLanguages(sb, candidate);
        BuildSectorExperience(sb, candidate);

        return sb.ToString().TrimEnd();
    }

    public string BuildJobDocument(JobRequisition job)
    {
        throw new System.NotImplementedException();
    }

    private void BuildProfessionalProfile(StringBuilder sb, Candidate candidate)
    {
        var snapshot = candidate.GetLatestFeatureSnapshot();
        
        bool hasTitle = SemanticFormattingHelper.ShouldInclude(candidate.ProfessionalTitle);
        bool hasSnapshot = snapshot != null;
        bool hasTotalExp = hasSnapshot && snapshot!.TotalExperienceMonths.HasValue;
        bool hasBackendExp = hasSnapshot && snapshot!.BackendExperienceMonths.HasValue;

        if (!hasTitle && !hasTotalExp && !hasBackendExp)
            return;

        sb.AppendLine("PROFESSIONAL PROFILE");
        if (hasTitle)
            sb.AppendLine($"Professional Title: {candidate.ProfessionalTitle}");
        if (hasTotalExp)
            sb.AppendLine($"Total Experience: {snapshot!.TotalExperienceMonths} months");
        if (hasBackendExp)
            sb.AppendLine($"Backend Experience: {snapshot!.BackendExperienceMonths} months");
        
        sb.AppendLine();
    }

    private void BuildEmploymentHistory(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.EmploymentHistories == null || !candidate.Person.EmploymentHistories.Any())
            return;

        var items = candidate.Person.EmploymentHistories
            .OrderByDescending(x => x.StartDate)
            .ToList();

        if (!items.Any()) return;

        sb.AppendLine("EMPLOYMENT HISTORY");
        foreach (var item in items)
        {
            var title = SemanticFormattingHelper.ShouldInclude(item.PositionTitle) ? item.PositionTitle : "Unknown Position";
            var company = SemanticFormattingHelper.ShouldInclude(item.EmployerName) ? item.EmployerName : "Unknown Company";
            var start = item.StartDate.ToString("MM.yyyy");
            var end = item.EndDate.HasValue ? item.EndDate.Value.ToString("MM.yyyy") : "Present";

            sb.AppendLine($"- {title} — {company} — {start}-{end}");
            if (SemanticFormattingHelper.ShouldInclude(item.Description))
            {
                sb.AppendLine($"  Description: {item.Description}");
            }
        }
        sb.AppendLine();
    }

    private void BuildSkills(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.PersonCompetencies == null || !candidate.Person.PersonCompetencies.Any())
            return;

        var items = candidate.Person.PersonCompetencies
            .Where(x => x.Competency != null && SemanticFormattingHelper.ShouldInclude(x.Competency.Name))
            .DistinctBy(x => x.CompetencyId)
            .OrderBy(x => x.Competency.Name)
            .ToList();

        if (!items.Any()) return;

        sb.AppendLine("SKILLS");
        foreach (var item in items)
        {
            sb.AppendLine($"- {item.Competency.Name}");
            // Future proofing for ExperienceMonths and ProficiencyLevel if they ever become populated.
            // But we don't output "null" if they are missing.
        }
        sb.AppendLine();
    }

    private void BuildProjects(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.PersonProjects == null || !candidate.Person.PersonProjects.Any())
            return;

        var validProjects = candidate.Person.PersonProjects
            .Where(x => x.Project != null && SemanticFormattingHelper.ShouldInclude(x.Project.Description))
            .Select(x => x.Project.Description!.Trim())
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (!validProjects.Any()) return;

        sb.AppendLine("PROJECTS");
        foreach (var desc in validProjects)
        {
            sb.AppendLine($"- {desc}");
        }
        sb.AppendLine();
    }

    private void BuildEducation(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.EducationRecords == null || !candidate.Person.EducationRecords.Any())
            return;

        var items = candidate.Person.EducationRecords
            .OrderByDescending(x => x.DegreeLevel)
            .ThenBy(x => x.FieldOfStudy ?? string.Empty)
            .ThenBy(x => x.Institution ?? string.Empty)
            .ToList();

        if (!items.Any()) return;

        bool hasEducation = false;
        var sectionSb = new StringBuilder();
        sectionSb.AppendLine("EDUCATION");
        
        foreach (var item in items)
        {
            var degree = SemanticFormattingHelper.FormatDegreeLevel(item.DegreeLevel);
            if (degree == null) continue; // Undefined enum value => OMIT entirely

            hasEducation = true;
            var field = SemanticFormattingHelper.ShouldInclude(item.FieldOfStudy) ? item.FieldOfStudy : "Unknown Field";
            
            if (SemanticFormattingHelper.ShouldInclude(item.Institution))
            {
                sectionSb.AppendLine($"- {degree} — {field} — {item.Institution}");
            }
            else
            {
                sectionSb.AppendLine($"- {degree} — {field}");
            }
        }
        
        if (hasEducation)
        {
            sb.Append(sectionSb.ToString());
            sb.AppendLine();
        }
    }

    private void BuildCertificates(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.PersonCertificates == null || !candidate.Person.PersonCertificates.Any())
            return;

        var items = candidate.Person.PersonCertificates
            .Where(x => x.Certificate != null && SemanticFormattingHelper.ShouldInclude(x.Certificate.Name))
            .OrderBy(x => x.Certificate.Name)
            .ToList();

        if (!items.Any()) return;

        sb.AppendLine("CERTIFICATES");
        foreach (var item in items)
        {
            if (SemanticFormattingHelper.ShouldInclude(item.Certificate.Issuer))
            {
                sb.AppendLine($"- {item.Certificate.Name} — {item.Certificate.Issuer}");
            }
            else
            {
                sb.AppendLine($"- {item.Certificate.Name}");
            }
        }
        sb.AppendLine();
    }

    private void BuildLanguages(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.PersonLanguages == null || !candidate.Person.PersonLanguages.Any())
            return;

        var items = candidate.Person.PersonLanguages
            .Where(x => x.Language != null && SemanticFormattingHelper.ShouldInclude(x.Language.Name))
            .OrderByDescending(x => x.IsNative)
            .ThenBy(x => x.Language.Name)
            .ToList();

        if (!items.Any()) return;

        sb.AppendLine("LANGUAGES");
        foreach (var item in items)
        {
            if (item.IsNative)
            {
                sb.AppendLine($"- {item.Language.Name} (Ana dil)");
            }
            else if (item.ProficiencyLevel.HasValue)
            {
                var prof = SemanticFormattingHelper.FormatLanguageProficiency(item.ProficiencyLevel.Value);
                if (prof != null)
                {
                    sb.AppendLine($"- {item.Language.Name} ({prof})");
                }
                else
                {
                    sb.AppendLine($"- {item.Language.Name}");
                }
            }
            else
            {
                sb.AppendLine($"- {item.Language.Name}");
            }
        }
        sb.AppendLine();
    }

    private void BuildSectorExperience(StringBuilder sb, Candidate candidate)
    {
        if (candidate.Person?.PersonSectorExperiences == null || !candidate.Person.PersonSectorExperiences.Any())
            return;

        var items = candidate.Person.PersonSectorExperiences
            .Where(x => x.Sector != null && SemanticFormattingHelper.ShouldInclude(x.Sector.Name))
            .OrderByDescending(x => x.ExperienceMonths ?? 0)
            .ThenBy(x => x.Sector.Name)
            .ToList();

        if (!items.Any()) return;

        sb.AppendLine("SECTOR EXPERIENCE");
        foreach (var item in items)
        {
            var name = item.Sector.Name;
            var exp = item.ExperienceMonths.HasValue ? $" — {item.ExperienceMonths} months" : "";
            
            sb.AppendLine($"- {name}{exp}");
            
            if (SemanticFormattingHelper.ShouldInclude(item.Notes))
            {
                sb.AppendLine($"  Notes: {item.Notes}");
            }
        }
        sb.AppendLine();
    }
}
