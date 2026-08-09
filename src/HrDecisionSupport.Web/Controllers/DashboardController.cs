using HrDecisionSupport.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class DashboardController : Controller
{
    private readonly IHrDecisionSupportDbContext _dbContext;

    public DashboardController(IHrDecisionSupportDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        // Veritabanından gerçek sayıları çekiyoruz
        int totalEmployees = await _dbContext.Employees.CountAsync();
        int totalCandidates = await _dbContext.Candidates.CountAsync();

        // Bu sayıları ViewBag ile arayüze (View) gönderiyoruz
        ViewBag.TotalEmployees = totalEmployees;
        ViewBag.TotalCandidates = totalCandidates;

        return View();
    }
}