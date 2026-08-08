using System.Linq;
using System.Text;
using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.SemanticMatching.Documents;

public class JobDocumentBuilder : ISemanticDocumentBuilder
{
    public string BuildCandidateDocument(Candidate candidate)
    {
        throw new System.NotImplementedException();
    }

    public string BuildJobDocument(JobRequisition job)
    {
        var sb = new StringBuilder();

        sb.AppendLine("JOB PROFILE");
        sb.AppendLine($"Title: {job.Title}");

        if (job.Position != null && SemanticFormattingHelper.ShouldInclude(job.Position.Name))
        {
            sb.AppendLine($"Position: {job.Position.Name}");
        }
        sb.AppendLine();

        if (SemanticFormattingHelper.ShouldInclude(job.Description))
        {
            sb.AppendLine("DESCRIPTION");
            sb.AppendLine(job.Description);
            sb.AppendLine();
        }

        if (job.MinimumRelevantExperienceMonths.HasValue)
        {
            sb.AppendLine("EXPERIENCE REQUIREMENTS");
            sb.AppendLine($"Minimum Relevant Experience: {job.MinimumRelevantExperienceMonths} months");
            sb.AppendLine();
        }

        if (job.MinimumEducationLevel.HasValue)
        {
            sb.AppendLine("EDUCATION REQUIREMENTS");
            sb.AppendLine($"Minimum Education Level: {job.MinimumEducationLevel.Value}");
            sb.AppendLine();
        }

        if (job.WorkMode != null)
        {
            sb.AppendLine("WORK MODE");
            sb.AppendLine($"Mode: {job.WorkMode.Name}");
            sb.AppendLine($"Hard Requirement: {(job.WorkModeHardFilterEnabled ? "Yes" : "No")}");
            sb.AppendLine();
        }

        if (job.LanguageRequirements != null && job.LanguageRequirements.Any())
        {
            sb.AppendLine("LANGUAGE REQUIREMENTS");
            sb.AppendLine();
            foreach (var langReq in job.LanguageRequirements.Where(x => x.Language != null).OrderBy(x => x.Language.Name))
            {
                sb.AppendLine($"- Language: {langReq.Language.Name}");
                sb.AppendLine($"  Minimum Proficiency: {langReq.MinimumProficiency}");
                sb.AppendLine($"  Hard Requirement: {(langReq.HardFilterEnabled ? "Yes" : "No")}");
                sb.AppendLine();
            }
        }

        if (job.Requirements != null && job.Requirements.Any())
        {
            var mandatory = job.Requirements
                .Where(x => x.IsRequired && x.Competency != null && SemanticFormattingHelper.ShouldInclude(x.Competency.Name))
                .OrderBy(x => x.Competency.Name)
                .ToList();

            if (mandatory.Any())
            {
                sb.AppendLine("MANDATORY SKILLS");
                foreach (var req in mandatory)
                {
                    sb.AppendLine($"- {req.Competency.Name}");
                }
                sb.AppendLine();
                sb.AppendLine($"Minimum Required Coverage: {job.MandatorySkillCoverageThreshold * 100:0}%");
                sb.AppendLine();
            }

            var preferred = job.Requirements
                .Where(x => !x.IsRequired && x.Competency != null && SemanticFormattingHelper.ShouldInclude(x.Competency.Name))
                .OrderBy(x => x.Competency.Name)
                .ToList();

            if (preferred.Any())
            {
                sb.AppendLine("PREFERRED SKILLS");
                foreach (var req in preferred)
                {
                    sb.AppendLine($"- {req.Competency.Name}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
