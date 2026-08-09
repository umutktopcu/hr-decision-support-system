using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HrDecisionSupport.Web.Controllers;

[ApiController]
[Route("api/job-requisitions/options")]
public class JobRequisitionOptionsController : ControllerBase
{
    private readonly IHrDecisionSupportDbContext _dbContext;

    public JobRequisitionOptionsController(IHrDecisionSupportDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetOptions(CancellationToken cancellationToken)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync(cancellationToken);

        var positions = await _dbContext.Positions
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.Code })
            .ToListAsync(cancellationToken);

        var workModes = await _dbContext.WorkModes
            .AsNoTracking()
            .OrderBy(w => w.Name)
            .Select(w => new { w.Id, w.Name })
            .ToListAsync(cancellationToken);

        var competencies = await _dbContext.Competencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompetencyCategory)
            .ThenBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.CompetencyCategory })
            .ToListAsync(cancellationToken);

        var languages = await _dbContext.Languages
            .AsNoTracking()
            .OrderBy(l => l.Name)
            .Select(l => new { l.Id, l.Name, l.Code })
            .ToListAsync(cancellationToken);

        var degreeLevels = Enum.GetValues<DegreeLevel>()
            .Select(e => new { Value = (int)e, Name = e.ToString() });

        var proficiencyLevels = Enum.GetValues<CompetencyProficiencyLevel>()
            .Select(e => new { Value = (int)e, Name = e.ToString() });

        var languageProficiencyLevels = Enum.GetValues<LanguageProficiencyLevel>()
            .Select(e => new { Value = (int)e, Name = e.ToString() });

        return Ok(new
        {
            Departments = departments,
            Positions = positions,
            WorkModes = workModes,
            Competencies = competencies,
            Languages = languages,
            DegreeLevels = degreeLevels,
            CompetencyProficiencyLevels = proficiencyLevels,
            LanguageProficiencyLevels = languageProficiencyLevels
        });
    }
}
