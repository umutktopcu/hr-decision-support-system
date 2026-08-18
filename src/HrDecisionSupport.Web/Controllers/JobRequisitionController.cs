using HrDecisionSupport.Application.MatchingExecution.History;
using Microsoft.AspNetCore.Mvc;
using System;

namespace HrDecisionSupport.Web.Controllers;

/// <summary>
/// MVC view controller for Job Requisition UI pages.
/// Routes: /JobRequisition, /JobRequisition/Create, /JobRequisition/Details/{id}, /JobRequisition/Edit/{id}
///
/// Do NOT confuse with the API controllers:
///   JobRequisitionsController  (API: /api/job-requisitions/...)
///   JobRequisitionOptionsController (API: /api/job-requisitions/options)
///   JobMatchingController (API: /api/job-requisitions/{jobId}/matching)
///
/// This controller only returns Razor Views. All data is loaded by
/// JavaScript fetch calls in the views that call the API controllers above.
/// </summary>
public class JobRequisitionController : Controller
{
    private readonly IJobMatchingHistoryService _historyService;

    public JobRequisitionController(IJobMatchingHistoryService historyService)
    {
        _historyService = historyService;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "İş İlanları";
        return View();
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Yeni İlan Oluştur";
        return View();
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        ViewData["Title"] = "İlan Detayı";
        ViewData["RequisitionId"] = id;
        var runs = await _historyService.GetRunsForJobAsync(id, cancellationToken);
        return View(runs);
    }

    [HttpGet("JobRequisition/{jobId:guid}/MatchingHistory/{runId:guid}")]
    public async Task<IActionResult> MatchingHistory(
        Guid jobId,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var run = await _historyService.GetRunDetailAsync(jobId, runId, cancellationToken);
        if (run is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Geçmiş Eşleştirme Sonucu";
        ViewData["RequisitionId"] = jobId;
        return View(run);
    }

    [HttpGet("JobRequisition/{jobId:guid}/MatchingHistory/Compare")]
    public async Task<IActionResult> CompareMatchingHistory(
        Guid jobId,
        Guid runAId,
        Guid runBId,
        CancellationToken cancellationToken)
    {
        if (runAId == runBId)
        {
            return BadRequest();
        }

        var comparison = await _historyService.GetComparisonAsync(
            jobId,
            runAId,
            runBId,
            cancellationToken);
        if (comparison is null)
        {
            return NotFound();
        }

        ViewData["Title"] = "Eşleştirme Karşılaştırması";
        ViewData["RequisitionId"] = jobId;
        return View("MatchingComparison", comparison);
    }

    public IActionResult Edit(Guid id)
    {
        ViewData["Title"] = "İlanı Düzenle";
        ViewData["RequisitionId"] = id;
        return View();
    }
}
