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

    public IActionResult Details(Guid id)
    {
        ViewData["Title"] = "İlan Detayı";
        ViewData["RequisitionId"] = id;
        return View();
    }

    public IActionResult Edit(Guid id)
    {
        ViewData["Title"] = "İlanı Düzenle";
        ViewData["RequisitionId"] = id;
        return View();
    }
}
