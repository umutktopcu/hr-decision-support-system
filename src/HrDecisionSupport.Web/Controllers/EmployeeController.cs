using HrDecisionSupport.Application.Employees;
using Microsoft.AspNetCore.Mvc;

namespace HrDecisionSupport.Web.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;
        private readonly IMlPredictionService _mlPredictionService;

        public EmployeeController(IEmployeeService employeeService, IMlPredictionService mlPredictionService)
        {
            _employeeService = employeeService;
            _mlPredictionService = mlPredictionService;
        }

        // "status" parametresi HTML'deki seçim kutusundan (select) gelecek
        public async Task<IActionResult> Index(string status = "Tümü")
        {
            var result = await _employeeService.ListAsync();

            if (!result.IsSuccess)
            {
                return View(new List<HrDecisionSupport.Application.Employees.Dtos.EmployeeListItemDto>());
            }

            var employees = result.Value.AsEnumerable();

            if (status == "Aktif")
            {
                employees = employees.Where(e => e.EmploymentStatus.ToString() == "Active");
            }
            else if (status == "Eski")
            {
                employees = employees.Where(e => e.EmploymentStatus.ToString() != "Active");
            }

            return View(employees.ToList());
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var result = await _employeeService.GetByIdAsync(id);
            if (!result.IsSuccess) return NotFound();

            double enKisaIs = result.Value.ShortestJobMonths ?? -1;
            double enUzunIs = result.Value.TotalExperienceMonths ?? -1;

            ViewBag.TestKisa = enKisaIs;
            ViewBag.TestUzun = enUzunIs;

            if (enKisaIs > 0 && enUzunIs > 0)
            {
                int tahminSonucu = await _mlPredictionService.PredictStayAsync(enKisaIs, enUzunIs);

                ViewBag.MlTahmin = tahminSonucu switch
                {
                    0 => "Kısa Süreli Kalıcı (Riskli)",
                    1 => "Normal Süreli Kalıcı",
                    2 => "Uzun Süreli Kalıcı (Güvenli)",
                    _ => "Tahmin Yapılamadı"
                };
            }
            else
            {
                ViewBag.MlTahmin = "Yetersiz Veri (Tahmin Yapılamadı)";
            }

            return View(result.Value);
        }
    }
}