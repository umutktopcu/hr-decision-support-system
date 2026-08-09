using HrDecisionSupport.Application.CandidateEvaluations;
using HrDecisionSupport.Application.Requisitions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace HrDecisionSupport.Web.Controllers
{
    public class JobRequisitionController : Controller
    {
        private readonly IJobRequisitionService _requisitionService;

        

        // 1. İlanları Listeleme Sayfası
        public async Task<IActionResult> Index()
        {
            var result = await _requisitionService.ListAsync(); // Metot adına göre servis üzerinden çağırıyoruz
            if (!result.IsSuccess)
            {
                return View();
            }
            return View(result.Value);
        }

        // 2. Yeni İlan Oluşturma Sayfası (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // 3. Yeni İlan Kaydetme (POST)
        [HttpPost]
        public async Task<IActionResult> Create(CreateJobRequisitionRequest request)
        {
            var result = await _requisitionService.CreateAsync(request);
            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", "İlan oluşturulamadı.");
                return View(request);
            }
            return RedirectToAction(nameof(Index));
        }

        private readonly ICandidateEvaluationCaseService _evaluationCaseService; // Arkadaşının servisini ekledik

        public JobRequisitionController(
            IJobRequisitionService requisitionService,
            ICandidateEvaluationCaseService evaluationCaseService)
        {
            _requisitionService = requisitionService;
            _evaluationCaseService = evaluationCaseService;
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var requisitionResult = await _requisitionService.GetByIdAsync(id);
            if (!requisitionResult.IsSuccess)
            {
                return NotFound("İlan bulunamadı.");
            }

            // Arkadaşının servisini kullanarak bu ilana ait adayları çekiyoruz
            var evaluationCasesResult = await _evaluationCaseService.ListByRequisitionAsync(id);

            // Adayları view tarafına taşıyoruz
            ViewBag.EvaluationCases = evaluationCasesResult.IsSuccess ? evaluationCasesResult.Value : null;

            return View(requisitionResult.Value);
        }
    }
}