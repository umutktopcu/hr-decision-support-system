using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.PreScreening.Evaluators;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;
using HrDecisionSupport.Application.PreScreening.Helpers;
using HrDecisionSupport.Domain.Extensions;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Collections.Generic;

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
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.LanguageRequirements)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.CareerFeatureSnapshots)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.WorkModePreferences)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonCompetencies)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.EducationRecords)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonLanguages)
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
            .Include(c => c.JobRequisition)
                .ThenInclude(jr => jr.LanguageRequirements)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.CareerFeatureSnapshots)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.WorkModePreferences)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonCompetencies)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.EducationRecords)
            .Include(c => c.Candidate)
                .ThenInclude(c => c.Person)
                    .ThenInclude(p => p.PersonLanguages)
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
        var failureReasons = new List<PreScreeningFailureReason>();

        // 1. Skill Evaluation
        var candidateCompetencyIds = candidate.Person?.PersonCompetencies.Select(c => c.CompetencyId).ToList() ?? [];

        var mandatoryJobCompetencyIds = job.Requirements
            .Where(r => r.IsRequired)
            .Select(r => r.CompetencyId)
            .ToList();

        var preferredJobCompetencyIds = job.Requirements
            .Where(r => !r.IsRequired)
            .Select(r => r.CompetencyId)
            .ToList();

        var effectiveMandatoryThreshold = job.MandatorySkillCoverageThreshold ?? _policy.MandatorySkillCoverageThreshold;

        var mandatoryResult = MandatorySkillFilter.Evaluate(
            candidateCompetencyIds,
            mandatoryJobCompetencyIds,
            effectiveMandatoryThreshold);

        var preferredResult = MandatorySkillFilter.Evaluate(
            candidateCompetencyIds,
            preferredJobCompetencyIds,
            0); // No threshold for preferred skills

        if (!mandatoryResult.Passed)
            failureReasons.Add(PreScreeningFailureReason.MandatorySkillCoverageBelowThreshold);

        // 2. Experience Evaluation
        int? candidateRelevantExperienceMonths = null;
        var requiredExperience = job.MinimumRelevantExperienceMonths;
        bool hasExperienceRequirement = requiredExperience > 0;

        if (job.Position?.Code == "BACKEND_DEVELOPER")
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
            failureReasons.Add(PreScreeningFailureReason.UnsupportedRelevantExperienceSource);
        }

        var experienceResult = ExperienceGate.Evaluate(candidateRelevantExperienceMonths, requiredExperience);
        bool experiencePassed = experienceResult.Passed && failureReasons.Count(f => f == PreScreeningFailureReason.MissingRelevantExperienceData || f == PreScreeningFailureReason.UnsupportedRelevantExperienceSource) == 0;

        if (!experiencePassed && failureReasons.Count(f => f == PreScreeningFailureReason.MissingRelevantExperienceData || f == PreScreeningFailureReason.UnsupportedRelevantExperienceSource) == 0)
        {
            if (hasExperienceRequirement)
                failureReasons.Add(PreScreeningFailureReason.RelevantExperienceBelowMinimum);
        }

        // 3. Education Evaluation
        EducationEvaluationResult educationResult;
        if (job.MinimumEducationLevel.HasValue)
        {
            int requiredRank = EducationRankHelper.GetEducationRank(job.MinimumEducationLevel.Value);
            int candidateRank = EducationRankHelper.GetHighestEducationRank(candidate);

            bool eduPassed = candidateRank >= requiredRank;

            DegreeLevel? highest = null;
            if (candidate.Person?.EducationRecords != null && candidate.Person.EducationRecords.Any())
            {
                var maxRank = candidate.Person.EducationRecords.Max(e => EducationRankHelper.GetEducationRank(e.DegreeLevel));
                highest = candidate.Person.EducationRecords.First(e => EducationRankHelper.GetEducationRank(e.DegreeLevel) == maxRank).DegreeLevel;
            }

            educationResult = new EducationEvaluationResult(
                job.MinimumEducationLevel,
                highest,
                eduPassed ? EvaluationStatus.Pass : EvaluationStatus.Fail,
                eduPassed);

            if (!eduPassed)
                failureReasons.Add(PreScreeningFailureReason.EducationLevelBelowMinimum);
        }
        else
        {
            educationResult = new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true);
        }

        // 4. WorkMode Evaluation
        WorkModeEvaluationResult workModeResult;
        if (job.WorkModeId.HasValue && job.WorkModeHardFilterEnabled)
        {
            bool candidateSupports = candidate.WorkModePreferences.Any(p => p.WorkModeId == job.WorkModeId.Value);
            workModeResult = new WorkModeEvaluationResult(
                job.WorkModeId.Value,
                true,
                candidateSupports,
                candidateSupports ? EvaluationStatus.Pass : EvaluationStatus.Fail,
                candidateSupports);

            if (!candidateSupports)
                failureReasons.Add(PreScreeningFailureReason.WorkModeMismatch);
        }
        else
        {
            bool candidateSupports = job.WorkModeId.HasValue && candidate.WorkModePreferences.Any(p => p.WorkModeId == job.WorkModeId.Value);
            workModeResult = new WorkModeEvaluationResult(
                job.WorkModeId,
                false,
                candidateSupports,
                EvaluationStatus.NotApplicable,
                true);
        }

        // 5. Language Evaluation
        var languageResults = new List<LanguageEvaluationResult>();
        bool languagePassed = true;

        if (job.LanguageRequirements != null)
        {
            foreach (var req in job.LanguageRequirements)
            {
                var pLang = candidate.Person?.PersonLanguages?.FirstOrDefault(pl => pl.LanguageId == req.LanguageId);

                int candidateRank = pLang != null ? LanguageRankHelper.GetCandidateProficiencyRank(pLang) : 0;
                int requiredRank = LanguageRankHelper.GetProficiencyRank(req.MinimumProficiency);

                bool meetsProficiency = candidateRank >= requiredRank;

                var langStatus = meetsProficiency ? EvaluationStatus.Pass : EvaluationStatus.Fail;
                if (!req.HardFilterEnabled)
                    langStatus = EvaluationStatus.NotApplicable;

                var result = new LanguageEvaluationResult(
                    req.LanguageId,
                    req.MinimumProficiency,
                    pLang?.ProficiencyLevel,
                    req.HardFilterEnabled,
                    langStatus,
                    !req.HardFilterEnabled || meetsProficiency);

                languageResults.Add(result);

                if (req.HardFilterEnabled && !meetsProficiency)
                {
                    languagePassed = false;
                }
            }
        }

        if (!languagePassed)
        {
            failureReasons.Add(PreScreeningFailureReason.LanguageRequirementNotMet);
        }

        // Final Eligibility
        bool isEligible = mandatoryResult.Passed && experiencePassed && educationResult.Passed && workModeResult.Passed && languagePassed;

        return new CandidatePreScreeningResult(
            candidate.Id,
            job.Id,
            mandatoryResult,
            preferredResult,
            experienceResult,
            educationResult,
            workModeResult,
            languageResults,
            isEligible,
            failureReasons,
            effectiveMandatoryThreshold
        );
    }
}
