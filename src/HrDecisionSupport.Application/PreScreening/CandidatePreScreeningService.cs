using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.PreScreening.Evaluators;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;
using HrDecisionSupport.Domain.Extensions;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.PreScreening;

public interface ICandidatePreScreeningService
{
    Task<Result<CandidatePreScreeningResult>> EvaluateAsync(
        Guid candidateEvaluationCaseId,
        CancellationToken cancellationToken = default);
        
    Task<Result<CandidatePreScreeningBatchResult>> EvaluateApplicantsForJobAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default);
}

public sealed class CandidatePreScreeningService : ICandidatePreScreeningService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly CandidatePreScreeningPolicy _policy;

    public CandidatePreScreeningService(
        IHrDecisionSupportDbContext dbContext,
        CandidatePreScreeningPolicy? policy = null)
    {
        _dbContext = dbContext;
        _policy = policy ?? new CandidatePreScreeningPolicy();
    }

    public async Task<Result<CandidatePreScreeningResult>> EvaluateAsync(
        Guid candidateEvaluationCaseId,
        CancellationToken cancellationToken = default)
    {
        var evalCase = await _dbContext.CandidateEvaluationCases
            .AsNoTracking()
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.Requirements)
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.Position)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.CareerFeatureSnapshots)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonCompetencies)
            .SingleOrDefaultAsync(caseItem => caseItem.Id == candidateEvaluationCaseId, cancellationToken);

        if (evalCase is null)
            return Result<CandidatePreScreeningResult>.Failure(new Error("evaluation_case_not_found", "Candidate Evaluation Case not found.", ErrorType.NotFound));

        var result = EvaluateCore(evalCase.JobRequisition, evalCase.Candidate);
        return Result<CandidatePreScreeningResult>.Success(result);
    }
    
    public async Task<Result<CandidatePreScreeningBatchResult>> EvaluateApplicantsForJobAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default)
    {
        var jobExists = await _dbContext.JobRequisitions
            .AnyAsync(j => j.Id == jobRequisitionId, cancellationToken);
            
        if (!jobExists)
            return Result<CandidatePreScreeningBatchResult>.Failure(new Error("job_requisition_not_found", "Job Requisition not found.", ErrorType.NotFound));

        var evalCases = await _dbContext.CandidateEvaluationCases
            .AsNoTracking()
            .Where(c => c.JobRequisitionId == jobRequisitionId)
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.Requirements)
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.Position)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.CareerFeatureSnapshots)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonCompetencies)
            .ToListAsync(cancellationToken);
            
        if (evalCases.Count == 0)
        {
            return Result<CandidatePreScreeningBatchResult>.Success(new CandidatePreScreeningBatchResult(
                JobRequisitionId: jobRequisitionId,
                TotalApplicants: 0,
                EligibleCount: 0,
                RejectedCount: 0,
                Results: []
            ));
        }
        
        var results = new List<CandidatePreScreeningResult>();
        var seenCandidateIds = new HashSet<Guid>();
        
        foreach (var evalCase in evalCases)
        {
            if (!seenCandidateIds.Add(evalCase.CandidateId))
                continue; // duplicate candidate for same job, evaluate only once
                
            results.Add(EvaluateCore(evalCase.JobRequisition, evalCase.Candidate));
        }
        
        var eligibleCount = results.Count(r => r.EligibleForSemanticEvaluation);
        
        var batchResult = new CandidatePreScreeningBatchResult(
            JobRequisitionId: jobRequisitionId,
            TotalApplicants: results.Count, // Only distinct candidates counted
            EligibleCount: eligibleCount,
            RejectedCount: results.Count - eligibleCount,
            Results: results
        );
        
        return Result<CandidatePreScreeningBatchResult>.Success(batchResult);
    }

    private CandidatePreScreeningResult EvaluateCore(Domain.Entities.JobRequisition job, Domain.Entities.Candidate candidate)
    {
        // 1. Skill Extraction
        var candidateCompetencyIds = candidate.Person.PersonCompetencies.Select(c => c.CompetencyId).ToList();
        
        var mandatoryJobCompetencyIds = job.Requirements
            .Where(r => r.IsRequired)
            .Select(r => r.CompetencyId)
            .ToList();
            
        var allJobCompetencyIds = job.Requirements
            .Select(r => r.CompetencyId)
            .ToList();

        // 2. Skill Evaluation
        var effectiveMandatoryThreshold = job.MandatorySkillCoverageThreshold ?? _policy.MandatorySkillCoverageThreshold;
        var effectiveOverallThreshold = job.OverallSkillCoverageThreshold ?? _policy.OverallSkillCoverageThreshold;

        var mandatoryResult = MandatorySkillFilter.Evaluate(
            candidateCompetencyIds, 
            mandatoryJobCompetencyIds, 
            effectiveMandatoryThreshold);
            
        var overallResult = OverallSkillFilter.Evaluate(
            candidateCompetencyIds, 
            allJobCompetencyIds, 
            effectiveOverallThreshold);

        // 3. Experience Evaluation
        var failureReasons = new List<PreScreeningFailureReason>();
        int? candidateRelevantExperienceMonths = null;

        var requiredExperience = job.MinimumRelevantExperienceMonths;
        bool hasExperienceRequirement = requiredExperience > 0;

        // V1 Mapping: Backend Experience mapping only for BACKEND_DEVELOPER
        if (job.Position.Code == "BACKEND_DEVELOPER")
        {
            var latestSnapshot = candidate.GetLatestFeatureSnapshot();

            if (latestSnapshot is not null)
            {
                candidateRelevantExperienceMonths = latestSnapshot.BackendExperienceMonths;
            }
            else if (hasExperienceRequirement)
            {
                failureReasons.Add(PreScreeningFailureReason.MissingRelevantExperienceData);
            }
        }
        else if (hasExperienceRequirement)
        {
            // Position requires experience, but we don't have a source for it in V1
            failureReasons.Add(PreScreeningFailureReason.UnsupportedRelevantExperienceSource);
        }

        var experienceResult = ExperienceGate.Evaluate(candidateRelevantExperienceMonths, requiredExperience);
        
        // If there were explicit missing data / unsupported source errors, the rule forces experience evaluation to fail.
        // E.g., if we had a requirement but no snapshot, experienceResult might say fail (null >= 36 -> false)
        // We just ensure the flag reflects it.
        bool experiencePassed = experienceResult.Passed && failureReasons.Count == 0;
        
        if (!experiencePassed && failureReasons.Count == 0)
        {
            // Normal failure due to insufficient months
            failureReasons.Add(PreScreeningFailureReason.RelevantExperienceBelowMinimum);
        }

        // 4. Final Eligibility
        if (!mandatoryResult.Passed)
            failureReasons.Add(PreScreeningFailureReason.MandatorySkillCoverageBelowThreshold);
            
        if (!overallResult.Passed)
            failureReasons.Add(PreScreeningFailureReason.OverallSkillCoverageBelowThreshold);

        var isEligible = mandatoryResult.Passed && overallResult.Passed && experiencePassed;

        return new CandidatePreScreeningResult(
            CandidateId: candidate.Id,
            JobRequisitionId: job.Id,
            MandatorySkillResult: mandatoryResult,
            OverallSkillResult: overallResult,
            ExperienceResult: experienceResult,
            EligibleForSemanticEvaluation: isEligible,
            FailureReasons: failureReasons,
            MandatorySkillThresholdUsed: effectiveMandatoryThreshold,
            OverallSkillThresholdUsed: effectiveOverallThreshold
        );
    }
}
