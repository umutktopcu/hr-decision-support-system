using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.SemanticMatching.Documents;

public interface ISemanticDocumentBuilder
{
    string BuildCandidateDocument(Candidate candidate);
    string BuildJobDocument(JobRequisition job);
}
