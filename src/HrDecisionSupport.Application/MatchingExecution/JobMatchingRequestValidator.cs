using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.MatchingExecution.Models;
using System.Collections.Generic;

namespace HrDecisionSupport.Application.MatchingExecution;

public sealed class JobMatchingRequestValidator : IValidator<JobMatchingRequest>
{
    public ValidationResult Validate(JobMatchingRequest instance)
    {
        var errors = new List<ValidationError>();

        if (instance.RetrievalTopN <= 0)
        {
            errors.Add(new ValidationError("retrieval_top_n_invalid", "RetrievalTopN must be greater than zero.", "RetrievalTopN"));
        }

        if (instance.FinalTopN <= 0)
        {
            errors.Add(new ValidationError("final_top_n_invalid", "FinalTopN must be greater than zero.", "FinalTopN"));
        }

        if (instance.FinalTopN > instance.RetrievalTopN)
        {
            errors.Add(new ValidationError("final_top_n_exceeds_retrieval", "FinalTopN cannot exceed RetrievalTopN.", "FinalTopN"));
        }

        return RequestValidation.ToResult(errors);
    }
}
