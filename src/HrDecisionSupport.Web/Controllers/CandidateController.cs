using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Employees;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
// IMlPredictionService'in bulunduğu namespace'i buraya eklemelisin (EmployeeController'ın en üstünden kopyalayabilirsin)

namespace HrDecisionSupport.Web.Controllers
{
    public class CandidateController : Controller
    {
        private readonly ICandidateService _candidateService;
        private readonly IMlPredictionService _mlPredictionService; // ML Servisimizi ekledik

        // Constructor üzerinden her iki servisi de içeri alıyoruz
        public CandidateController(
            ICandidateService candidateService,
            IMlPredictionService mlPredictionService)
        {
            _candidateService = candidateService;
            _mlPredictionService = mlPredictionService;
        }

        public async Task<IActionResult> Index()
        {
            var result = await _candidateService.ListAsync();

            if (!result.IsSuccess)
            {
                return View();
            }

            return View(result.Value);
        }

        public async Task<IActionResult> Details(Guid id)
        {
            // 1. Adayın gerçek detay verilerini çekiyoruz
            var result = await _candidateService.GetByIdAsync(id);

            if (!result.IsSuccess)
            {
                return NotFound("Aday bulunamadı veya silinmiş olabilir.");
            }

            var candidate = result.Value;

            // 2. ML (Yapay Zeka) Modelimizi çalıştırıyoruz
            // 2. ML (Yapay Zeka) Modelimizi çalıştırıyoruz
            try
            {
                // Adayın gerçek sürelerini DTO'dan alıyoruz
                if (candidate.ShortestJobMonths > 0 && candidate.LongestJobMonths > 0)
                {
                    int tahminSonucu = await _mlPredictionService.PredictStayAsync(candidate.ShortestJobMonths.Value, candidate.LongestJobMonths.Value);

                    // Python'dan dönen 0, 1, 2 sonucunu metne çevirip arayüze taşıyoruz
                    ViewBag.MlPrediction = tahminSonucu switch
                    {
                        0 => "Kısa Süreli Kalıcı (Riskli)",
                        1 => "Normal Süreli Kalıcı",
                        2 => "Uzun Süreli Kalıcı (Güvenli)",
                        _ => "Tahmin Yapılamadı"
                    };
                }
                else
                {
                    ViewBag.MlPrediction = "Yetersiz Veri (Tahmin Yapılamadı)";
                }
            }
            catch
            {
                ViewBag.MlPrediction = "Tahmin Yapılamadı (Servis Kapalı)";
            }

            // 3. Veriyi (CandidateDetailsDto) View'a gönderiyoruz
            return View(candidate);
        }
    }
}