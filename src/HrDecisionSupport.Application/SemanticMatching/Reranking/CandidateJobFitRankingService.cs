using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Orchestration;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking;

public class CandidateJobFitRankingService : ICandidateJobFitRankingService
{
    private readonly ICandidateSemanticMatchingService _semanticMatchingService;
    private readonly ICrossEncoderProvider _crossEncoderProvider;

    public CandidateJobFitRankingService(
        ICandidateSemanticMatchingService semanticMatchingService,
        ICrossEncoderProvider crossEncoderProvider)
    {
        _semanticMatchingService = semanticMatchingService;
        _crossEncoderProvider = crossEncoderProvider;
    }

    public async Task<Result<CandidateJobFitRankingBatchResult>> RankApplicantsForJobAsync(
        Guid jobRequisitionId, 
        int retrievalTopN, 
        int finalTopN,
        CancellationToken cancellationToken = default)
    {
        // 1. Get Top N candidates from B3 Embedding Retrieval
        var semanticResult = await _semanticMatchingService.MatchApplicantsForJobAsync(jobRequisitionId, retrievalTopN, cancellationToken);
        if (semanticResult.IsFailure)
        {
            return Result<CandidateJobFitRankingBatchResult>.Failure(semanticResult.Error!);
        }

        var batch = semanticResult.Value;

        // 2. Early exit if no candidates retrieved
        if (batch.RetrievedCount == 0 || !batch.Results.Any())
        {
            return Result<CandidateJobFitRankingBatchResult>.Success(new CandidateJobFitRankingBatchResult(
                JobRequisitionId: batch.JobRequisitionId,
                TotalApplicants: batch.TotalApplicants,
                PreScreeningEligibleCount: batch.PreScreeningEligibleCount,
                PreScreeningRejectedCount: batch.PreScreeningRejectedCount,
                RetrievedCandidateCount: batch.RetrievedCount,
                CrossEncodedCandidateCount: 0,
                RequestedRetrievalTopN: batch.RequestedTopN,
                RequestedFinalTopN: finalTopN,
                FinalCandidateCount: 0,
                JobDocumentText: batch.JobDocumentText,
                Results: Array.Empty<CandidateJobFitRankingResult>()
            ));
        }

        // 3. Prepare inputs for CrossEncoder
        var inputs = batch.Results.Select(r => new CrossEncoderCandidateInput(
            CandidateId: r.CandidateId,
            CandidateDocumentText: r.CandidateDocumentText
        )).ToList();

        // 4. Call Reranker Provider
        // Note: The specific instruction (e.g. "Given a job description...") is handled internally by the provider/Python service
        // to keep this application service model-agnostic.
        var rerankResult = await _crossEncoderProvider.ScoreAsync(batch.JobDocumentText, inputs, cancellationToken);
        if (rerankResult.IsFailure)
        {
            return Result<CandidateJobFitRankingBatchResult>.Failure(rerankResult.Error!);
        }

        var scores = rerankResult.Value.ToDictionary(s => s.CandidateId);

        // 5. Check if we received all requested scores
        if (scores.Count != batch.Results.Count)
        {
            return Result<CandidateJobFitRankingBatchResult>.Failure(
                new Error("reranker_count_mismatch", $"Expected {batch.Results.Count} scores, got {scores.Count}", ErrorType.Failure));
        }

        // 6. Assign Tiers based on Unique Skill Coverage Combinations
        var distinctTiers = batch.Results
            .Select(r => new { r.MandatorySkillCoverage, r.PreferredSkillCoverage })
            .Distinct()
            .OrderByDescending(t => t.MandatorySkillCoverage)
            .ThenByDescending(t => t.PreferredSkillCoverage)
            .ToList();

        var tierMap = distinctTiers
            .Select((t, i) => new { Tier = i + 1, t.MandatorySkillCoverage, t.PreferredSkillCoverage })
            .ToDictionary(x => (x.MandatorySkillCoverage, x.PreferredSkillCoverage), x => x.Tier);

        // 7. Map to final results and preserve Embedding Rank
        var unsortedResults = new List<CandidateJobFitRankingResult>(batch.Results.Count);
        for (int i = 0; i < batch.Results.Count; i++)
        {
            var r = batch.Results[i];
            if (!scores.TryGetValue(r.CandidateId, out var scoreResult))
            {
                return Result<CandidateJobFitRankingBatchResult>.Failure(
                    new Error("reranker_missing_candidate", $"Score for candidate {r.CandidateId} is missing.", ErrorType.Failure));
            }

            int tier = tierMap[(r.MandatorySkillCoverage, r.PreferredSkillCoverage)];

            unsortedResults.Add(new CandidateJobFitRankingResult(
                CandidateId: r.CandidateId,
                FinalRank: 0, // Will be set after sorting
                SkillTier: tier,
                EmbeddingRank: i + 1,
                CosineSimilarityScore: r.CosineSimilarityScore,
                CrossEncoderRawScore: scoreResult.RawScore,
                JobFitScore: scoreResult.JobFitScore,
                CandidateDocumentText: r.CandidateDocumentText,
                MandatoryMatchedCount: r.MandatoryMatchedCount,
                MandatoryRequiredCount: r.MandatoryRequiredCount,
                MandatorySkillCoverage: r.MandatorySkillCoverage,
                PreferredMatchedCount: r.PreferredMatchedCount,
                PreferredRequiredCount: r.PreferredRequiredCount,
                PreferredSkillCoverage: r.PreferredSkillCoverage
            ));
        }

        // 8. Final Sort: SkillTier ASC, CrossEncoderRawScore DESC, CandidateId ASC
        var sortedResults = unsortedResults
            .OrderBy(r => r.SkillTier)
            .ThenByDescending(r => r.CrossEncoderRawScore)
            .ThenBy(r => r.CandidateId)
            .Take(finalTopN)
            .ToList();

        // 9. Assign Final Rank
        for (int i = 0; i < sortedResults.Count; i++)
        {
            sortedResults[i] = sortedResults[i] with { FinalRank = i + 1 };
        }

        // 10. Return final batch result
        var finalBatch = new CandidateJobFitRankingBatchResult(
            JobRequisitionId: batch.JobRequisitionId,
            TotalApplicants: batch.TotalApplicants,
            PreScreeningEligibleCount: batch.PreScreeningEligibleCount,
            PreScreeningRejectedCount: batch.PreScreeningRejectedCount,
            RetrievedCandidateCount: batch.RetrievedCount,
            CrossEncodedCandidateCount: scores.Count,
            RequestedRetrievalTopN: batch.RequestedTopN,
            RequestedFinalTopN: finalTopN,
            FinalCandidateCount: sortedResults.Count,
            JobDocumentText: batch.JobDocumentText,
            Results: sortedResults
        );

        return Result<CandidateJobFitRankingBatchResult>.Success(finalBatch);
    }
}
