using Microsoft.AspNetCore.Mvc;

namespace HrDecisionSupport.Web.Controllers
{
    public class CandidateController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Details(int id)
        {
            return View();
        }
    }
}