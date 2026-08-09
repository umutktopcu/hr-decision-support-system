using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.MatchingExecution.Models;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.MatchingExecution;

public class JobMatchingExecutionService : IJobMatchingExecutionService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly ICandidateJobFitRankingService _rankingService;
    private readonly IValidator<JobMatchingRequest> _validator;

    public JobMatchingExecutionService(
        IHrDecisionSupportDbContext dbContext,
        ICandidateJobFitRankingService rankingService,
        IValidator<JobMatchingRequest> validator)
    {
        _dbContext = dbContext;
        _rankingService = rankingService;
        _validator = validator;
    }

    public async Task<Result<JobMatchingExecutionResult>> ExecuteMatchingAsync(
        Guid jobRequisitionId,
        JobMatchingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = _validator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<JobMatchingExecutionResult>.ValidationFailure(validation.Errors);
        }

        // 1. Target Job Requisition
        var jobRequisition = await _dbContext.JobRequisitions
            .AsNoTracking()
            .Select(j => new { j.Id, j.PositionId })
            .SingleOrDefaultAsync(j => j.Id == jobRequisitionId, cancellationToken);

        if (jobRequisition == null)
        {
            return Result<JobMatchingExecutionResult>.Failure(
                new Error("job_requisition_not_found", "Job Requisition not found.", ErrorType.NotFound));
        }

        // 2. Determine Candidate Pool (Target + Same-Position)
        var targetExistingCandidateIds = await _dbContext.CandidateEvaluationCases
            .Where(c => c.JobRequisitionId == jobRequisitionId)
            .Select(c => c.CandidateId)
            .ToListAsync(cancellationToken);

        var samePositionCandidateIds = await _dbContext.CandidateEvaluationCases
            .Where(c => c.JobRequisition.PositionId == jobRequisition.PositionId)
            .Select(c => c.CandidateId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var candidatePool = new HashSet<Guid>(targetExistingCandidateIds);
        candidatePool.UnionWith(samePositionCandidateIds);

        var poolCandidateIds = candidatePool.ToList();

        // 3. Evaluation Case Initialization (Idempotent)
        var existingTargetSet = new HashSet<Guid>(targetExistingCandidateIds);
        var missingCandidateIds = poolCandidateIds.Where(id => !existingTargetSet.Contains(id)).ToList();

        if (missingCandidateIds.Any())
        {
            var newCases = missingCandidateIds.Select(candidateId => new CandidateEvaluationCase
            {
                Id = Guid.NewGuid(),
                CandidateId = candidateId,
                JobRequisitionId = jobRequisitionId,
                ReceivedAtUtc = DateTime.UtcNow,
                Status = CandidateEvaluationStatus.New,
                CreatedAtUtc = DateTime.UtcNow
            }).ToList();

            await _dbContext.CandidateEvaluationCases.AddRangeAsync(newCases, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // 4. Execute the Semantic Pipeline
        var rankingResult = await _rankingService.RankApplicantsForJobAsync(
            jobRequisitionId,
            request.RetrievalTopN,
            request.FinalTopN,
            cancellationToken);

        if (rankingResult.IsFailure)
        {
            return Result<JobMatchingExecutionResult>.Failure(rankingResult.Error!);
        }

        var batch = rankingResult.Value;

        // 5. Build Hard Filter Breakdown and Statistics
        var breakdown = BuildBreakdown(batch.PreScreeningResults);

        var eligibleCount = batch.PreScreeningResults?.Count(x => x.EligibleForSemanticEvaluation) ?? 0;
        var rejectedCount = batch.PreScreeningResults?.Count(x => !x.EligibleForSemanticEvaluation) ?? 0;

        var preScreenedCandidateCount = batch.PreScreeningResults?.Count ?? 0;

        var stats = new JobMatchingStatistics(
            CandidatePoolCount: poolCandidateIds.Count,
            ExistingEvaluationCaseCount: targetExistingCandidateIds.Count,
            CreatedEvaluationCaseCount: missingCandidateIds.Count,
            PreScreenedCandidateCount: preScreenedCandidateCount,
            EligibleAfterHardFilters: eligibleCount,
            RejectedByHardFilters: rejectedCount,
            EmbeddingInputCount: eligibleCount,
            RequestedRetrievalTopN: request.RetrievalTopN,
            EmbeddingRetrievedCount: batch.RetrievedCandidateCount,
            CrossEncoderInputCount: batch.CrossEncodedCandidateCount,
            RequestedFinalTopN: request.FinalTopN,
            FinalReturnedCount: batch.FinalCandidateCount
        );

        // 6. Fetch Candidates Info
        var candidateIdsToFetch = batch.Results.Select(r => r.CandidateId).ToList();
        var candidatesInfo = new Dictionary<Guid, (string? Code, string DisplayName, int? Short, int? Long)>();

        if (candidateIdsToFetch.Any())
        {
            var dbCandidates = await _dbContext.Candidates
                .AsNoTracking()
                .Where(c => candidateIdsToFetch.Contains(c.Id))
                .Select(c => new
                {
                    c.Id,
                    c.CandidateCode,
                    c.Person.FirstName,
                    c.Person.LastName,
                    ShortestJobMonths = c.CareerFeatureSnapshots.OrderByDescending(s => s.CalculatedAtUtc).Select(s => s.ShortestPreviousJobMonths).FirstOrDefault(),
                    LongestJobMonths = c.CareerFeatureSnapshots.OrderByDescending(s => s.CalculatedAtUtc).Select(s => s.LongestPreviousJobMonths).FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            foreach (var c in dbCandidates)
            {
                var displayName = string.IsNullOrWhiteSpace(c.FirstName) && string.IsNullOrWhiteSpace(c.LastName)
                    ? "Unknown Candidate"
                    : $"{c.FirstName} {c.LastName}".Trim();
                candidatesInfo[c.Id] = (c.CandidateCode, displayName, c.ShortestJobMonths, c.LongestJobMonths);
            }
        }

        var candidateResults = batch.Results.Select(r =>
        {
            var info = candidatesInfo.GetValueOrDefault(r.CandidateId, (null, "Unknown Candidate", null, null));
            return new JobMatchingCandidateResult(
                CandidateId: r.CandidateId,
                CandidateCode: info.Code,
                DisplayName: info.DisplayName,
                SkillTier: r.SkillTier,
                MandatorySkillCoverage: r.MandatorySkillCoverage,
                PreferredSkillCoverage: r.PreferredSkillCoverage,
                EmbeddingScore: r.CosineSimilarityScore,
                CrossEncoderRawScore: r.CrossEncoderRawScore,
                JobFitScore: r.JobFitScore,
                ShortestJobMonths: info.Short,
                LongestJobMonths: info.Long
            );
        }).ToList();

        var finalResult = new JobMatchingExecutionResult(
            JobRequisitionId: jobRequisitionId,
            Statistics: stats,
            HardFilterBreakdown: breakdown,
            Candidates: candidateResults
        );

        return Result<JobMatchingExecutionResult>.Success(finalResult);
    }

    private static HardFilterBreakdown? BuildBreakdown(IReadOnlyList<CandidatePreScreeningResult>? results)
    {
        if (results == null || !results.Any())
            return null;

        return new HardFilterBreakdown(
            MandatorySkill: new GateStatistics(
                results.Count(r => r.MandatorySkillResult.Passed),
                results.Count(r => !r.MandatorySkillResult.Passed),
                0 // Mandatory skill is always applicable if there are pre-screening results
            ),
            Experience: new GateStatistics(
                results.Count(r => r.ExperienceResult.Passed && r.ExperienceResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => !r.ExperienceResult.Passed && r.ExperienceResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => r.ExperienceResult.Status == EvaluationStatus.NotApplicable)
            ),
            Education: new GateStatistics(
                results.Count(r => r.EducationResult.Passed && r.EducationResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => !r.EducationResult.Passed && r.EducationResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => r.EducationResult.Status == EvaluationStatus.NotApplicable)
            ),
            WorkMode: new GateStatistics(
                results.Count(r => r.WorkModeResult.Passed && r.WorkModeResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => !r.WorkModeResult.Passed && r.WorkModeResult.Status != EvaluationStatus.NotApplicable),
                results.Count(r => r.WorkModeResult.Status == EvaluationStatus.NotApplicable)
            ),
            Language: new GateStatistics(
                results.Count(r => r.LanguageResults.All(l => l.Passed) && r.LanguageResults.Any(l => l.Status != EvaluationStatus.NotApplicable)),
                results.Count(r => r.LanguageResults.Any(l => !l.Passed)),
                results.Count(r => !r.LanguageResults.Any() || r.LanguageResults.All(l => l.Status == EvaluationStatus.NotApplicable))
            )
        );
    }
}
