using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.PreScreening;
using HrDecisionSupport.Application.SemanticMatching.Documents;
using HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;
using HrDecisionSupport.Application.SemanticMatching.Retrieval;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.SemanticMatching.Orchestration;

public class CandidateSemanticMatchingService : ICandidateSemanticMatchingService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly ICandidatePreScreeningService _preScreeningService;
    private readonly ISemanticRetrievalService _retrievalService;
    private readonly CandidateDocumentBuilder _candidateDocumentBuilder;
    private readonly JobDocumentBuilder _jobDocumentBuilder;

    public CandidateSemanticMatchingService(
        IHrDecisionSupportDbContext dbContext,
        ICandidatePreScreeningService preScreeningService,
        ISemanticRetrievalService retrievalService,
        CandidateDocumentBuilder candidateDocumentBuilder,
        JobDocumentBuilder jobDocumentBuilder)
    {
        _dbContext = dbContext;
        _preScreeningService = preScreeningService;
        _retrievalService = retrievalService;
        _candidateDocumentBuilder = candidateDocumentBuilder;
        _jobDocumentBuilder = jobDocumentBuilder;
    }

    public async Task<Result<CandidateSemanticMatchingBatchResult>> MatchApplicantsForJobAsync(
        Guid jobRequisitionId, 
        int topN, 
        CancellationToken cancellationToken = default)
    {
        if (topN <= 0)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Failure(
                new Error("invalid_topn", "topN must be greater than zero.", ErrorType.Validation));
        }

        // 1. Run Candidate PreScreening to determine eligibility
        var preScreeningResult = await _preScreeningService.EvaluateApplicantsForJobAsync(jobRequisitionId, cancellationToken);
        if (preScreeningResult.IsFailure)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Failure(preScreeningResult.Error!);
        }

        var batch = preScreeningResult.Value;

        // 2. Load the Job Requisition Graph (only what JobDocumentBuilder needs)
        var job = await _dbContext.JobRequisitions
            .AsNoTracking()
            .Include(j => j.Position)
            .Include(j => j.Requirements)
                .ThenInclude(r => r.Competency)
            .SingleOrDefaultAsync(j => j.Id == jobRequisitionId, cancellationToken);

        if (job == null)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Failure(
                new Error("job_requisition_not_found", "Job Requisition not found.", ErrorType.NotFound));
        }

        var jobDocumentText = _jobDocumentBuilder.BuildJobDocument(job);

        // 3. Early exit if no candidates are eligible
        if (batch.EligibleCount == 0)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Success(new CandidateSemanticMatchingBatchResult(
                JobRequisitionId: jobRequisitionId,
                TotalApplicants: batch.TotalApplicants,
                PreScreeningEligibleCount: 0,
                PreScreeningRejectedCount: batch.RejectedCount,
                RequestedTopN: topN,
                RetrievedCount: 0,
                JobDocumentText: jobDocumentText,
                Results: Array.Empty<SemanticRetrievalResult>()
            ));
        }

        // 4. Extract eligible candidate IDs
        var eligibleCandidateIds = batch.Results
            .Where(r => r.EligibleForSemanticEvaluation)
            .Select(r => r.CandidateId)
            .Distinct()
            .ToList();

        // 5. Load Candidate Graphs required for Document Building (use AsSplitQuery to avoid cartesian explosion)
        var loadedCandidates = await _dbContext.Candidates
            .AsNoTracking()
            // Removed AsSplitQuery() because it might require Microsoft.EntityFrameworkCore.Relational which isn't referenced in Application layer.
            .Where(c => eligibleCandidateIds.Contains(c.Id))
            .Include(c => c.CareerFeatureSnapshots)
            .Include(c => c.Person)
                .ThenInclude(p => p.EmploymentHistories)
            .Include(c => c.Person)
                .ThenInclude(p => p.PersonCompetencies)
                    .ThenInclude(pc => pc.Competency)
            .Include(c => c.Person)
                .ThenInclude(p => p.PersonProjects)
                    .ThenInclude(pp => pp.Project)
            .Include(c => c.Person)
                .ThenInclude(p => p.EducationRecords)
            .Include(c => c.Person)
                .ThenInclude(p => p.PersonCertificates)
                    .ThenInclude(pc => pc.Certificate)
            .Include(c => c.Person)
                .ThenInclude(p => p.PersonLanguages)
                    .ThenInclude(pl => pl.Language)
            .Include(c => c.Person)
                .ThenInclude(p => p.PersonSectorExperiences)
                    .ThenInclude(ps => ps.Sector)
            .ToListAsync(cancellationToken);

        // 6. Candidate integrity check
        if (loadedCandidates.Count != eligibleCandidateIds.Count)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Failure(
                new Error("candidate_data_integrity_error", $"Expected to load {eligibleCandidateIds.Count} eligible candidates, but only {loadedCandidates.Count} were found.", ErrorType.Failure));
        }

        // 7. Build candidate documents
        var semanticCandidates = new List<SemanticCandidateDocument>(loadedCandidates.Count);
        foreach (var c in loadedCandidates)
        {
            var text = _candidateDocumentBuilder.BuildCandidateDocument(c);
            semanticCandidates.Add(new SemanticCandidateDocument(c.Id, text));
        }

        // 8. Run semantic retrieval
        var retrievalResult = await _retrievalService.RetrieveTopCandidatesAsync(
            jobDocumentText, 
            semanticCandidates, 
            topN, 
            cancellationToken);

        if (retrievalResult.IsFailure)
        {
            return Result<CandidateSemanticMatchingBatchResult>.Failure(retrievalResult.Error!);
        }

        // 9. Construct final result
        var finalResults = retrievalResult.Value;
        var batchResult = new CandidateSemanticMatchingBatchResult(
            JobRequisitionId: jobRequisitionId,
            TotalApplicants: batch.TotalApplicants,
            PreScreeningEligibleCount: batch.EligibleCount,
            PreScreeningRejectedCount: batch.RejectedCount,
            RequestedTopN: topN,
            RetrievedCount: finalResults.Count,
            JobDocumentText: jobDocumentText,
            Results: finalResults
        );

        return Result<CandidateSemanticMatchingBatchResult>.Success(batchResult);
    }
}
