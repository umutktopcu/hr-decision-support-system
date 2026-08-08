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
