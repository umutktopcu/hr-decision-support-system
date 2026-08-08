using HrDecisionSupport.Application.Employees;
using Microsoft.AspNetCore.Mvc;

namespace HrDecisionSupport.Web.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly IEmployeeService _employeeService;
        private readonly IMlPredictionService _mlPredictionService; // ML Servisini ekledik

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

            // Gelen tüm listeyi LINQ ile filtreleyebilmek için bir değişkene alıyoruz
            var employees = result.Value.AsEnumerable();

            // Filtreleme Mantığı (EmploymentStatus Enum'una göre)
            if (status == "Aktif")
            {
                employees = employees.Where(e => e.EmploymentStatus.ToString() == "Active");
            }
            else if (status == "Eski")
            {
                // Active olmayanları (örneğin Terminated) filtrele
                employees = employees.Where(e => e.EmploymentStatus.ToString() != "Active");
            }

            // Filtrelenmiş listeyi View'a (HTML'e) gönder
            return View(employees.ToList());
        }

        public async Task<IActionResult> Details(Guid id)
        {
            var result = await _employeeService.GetByIdAsync(id);
            if (!result.IsSuccess) return NotFound();

            // Şimdilik test amaçlı örnek veriler gönderiyoruz (DTO güncellenince buraya DB'den gelen gerçek süreleri koyacağız)
            double ornekEnKisaIs = 14.0;
            double ornekEnUzunIs = 44.0;

            // Python'a soruyoruz!
            int tahminSonucu = await _mlPredictionService.PredictStayAsync(ornekEnKisaIs, ornekEnUzunIs);

            // Tahmini arayüze taşıyoruz
            ViewBag.MlTahmin = tahminSonucu switch
            {
                0 => "Kısa Süreli Kalıcı (Riskli)",
                1 => "Normal Süreli Kalıcı",
                2 => "Uzun Süreli Kalıcı (Güvenli)",
                _ => "Tahmin Yapılamadı"
            };

            return View(result.Value);
        }
    }
}