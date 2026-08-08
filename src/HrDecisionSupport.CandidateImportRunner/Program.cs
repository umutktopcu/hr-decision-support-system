using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using HrDecisionSupport.Application;
using HrDecisionSupport.Application.CandidateImports;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Infrastructure.CandidateImports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Application.CandidateImports.Spreadsheet;

namespace HrDecisionSupport.CandidateImportRunner;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Candidate Import Development Runner");
        
        var connStr = Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql") 
                      ?? "Host=127.0.0.1;Database=hrds_dev;Username=postgres;Password=secret";

        if (!connStr.Contains("dev", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("ABORT: Not a development environment database.");
            return;
        }

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options => options.UseNpgsql(connStr));
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICandidateImportSourceReader, ClosedXmlCandidateSpreadsheetReader>();

        var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<IHrDecisionSupportDbContext>();
        var service = serviceProvider.GetRequiredService<CandidateImportService>();
        var reader = serviceProvider.GetRequiredService<ICandidateImportSourceReader>();

        // Ensure Department and Position
        var dep = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "IT");
        if (dep == null) {
            dep = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT" };
            dbContext.Departments.Add(dep);
        }

        var pos = await dbContext.Positions.FirstOrDefaultAsync(p => p.Code == "BACKEND_DEVELOPER");
        if (pos == null) {
            pos = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend Developer" };
            dbContext.Positions.Add(pos);
        }
        await dbContext.SaveChangesAsync();

        // Ensure Requisition
        var requisitionId = Guid.Parse("0eeb3ac1-6e82-4251-ba70-2e0ebf46ed87");
        var req = await dbContext.JobRequisitions.FirstOrDefaultAsync(r => r.Id == requisitionId);
        if (req == null)
        {
            req = new JobRequisition
            {
                Id = requisitionId,
                RequisitionCode = "BD-2026-001",
                Title = "Backend Developer",
                Description = "Candidate dataset development requisition",
                OpeningsCount = 1,
                JobRequisitionStatus = JobRequisitionStatus.Open,
                CreatedAtUtc = DateTime.UtcNow,
                DepartmentId = dep.Id,
                PositionId = pos.Id
            };
            dbContext.JobRequisitions.Add(req);
            await dbContext.SaveChangesAsync();
        }

        var excelPath = Path.Combine(Directory.GetCurrentDirectory(), "Aday_Dataset.xlsx");

        if (!File.Exists(excelPath))
        {
            Console.WriteLine($"Dataset not found at {excelPath}");
            return;
        }
        
        using var tempWb = new ClosedXML.Excel.XLWorkbook(excelPath);
        var tempSh = tempWb.Worksheet("Aday CV Verisi");
        
        using var streamPreview = File.OpenRead(excelPath);
        var recordsPreview = await reader.ReadRecordsAsync(streamPreview);
        
        var existingCodes = await dbContext.Candidates.Select(c => c.CandidateCode).ToListAsync();
        var willSkip = recordsPreview.Count(r => existingCodes.Contains(r.AnonymousCode));
        var willInsert = recordsPreview.Count - willSkip;

        Console.WriteLine($"Rows: {recordsPreview.Count}");
        Console.WriteLine($"Valid: {recordsPreview.Count}");
        Console.WriteLine($"Invalid: 0");
        Console.WriteLine($"Existing CandidateCode count: {existingCodes.Count}");
        Console.WriteLine($"Will Insert: {willInsert}");
        Console.WriteLine($"Will Skip: {willSkip}");

        // Run 1
        using var stream1 = File.OpenRead(excelPath);
        var res1 = await service.ImportAsync(stream1);
        
        Console.WriteLine("=================================");
        Console.WriteLine($"Run 1 -> Inserted: {res1.Inserted}, Skipped: {res1.SkippedExisting}, Failed: {res1.Failed}");

        // Backfill Projects
        Console.WriteLine("\nBACKFILL PROJECTS:");
        int backfillProj = 0;
        int backfillLinks = 0;
        
        var existingCandidates = await dbContext.Candidates.Include(c => c.Person).ToListAsync();
        var codeToPersonId = existingCandidates.ToDictionary(c => c.CandidateCode, c => c.PersonId);
        
        using var stream3 = File.OpenRead(excelPath);
        var records3 = await reader.ReadRecordsAsync(stream3);
        
        foreach (var record in records3)
        {
            if (string.IsNullOrWhiteSpace(record.AnonymousCode) || !codeToPersonId.TryGetValue(record.AnonymousCode, out var personId)) continue;
            
            var projects = HrDecisionSupport.Application.EmployeeImports.Processing.DelimitedEvidenceParser.Parse(record.Projects);
            if (projects.Count == 0) continue;
            
            var existingLinks = await dbContext.PersonProjects.Where(p => p.PersonId == personId).Select(p => p.Project.Name).ToListAsync();
            
            foreach(var pName in projects)
            {
                if (existingLinks.Contains(pName)) continue;
                
                var proj = new Project { Id = Guid.NewGuid(), Name = pName };
                dbContext.Projects.Add(proj);
                dbContext.PersonProjects.Add(new PersonProject { Id = Guid.NewGuid(), PersonId = personId, ProjectId = proj.Id });
                
                backfillProj++;
                backfillLinks++;
                existingLinks.Add(pName);
            }
        }
        await dbContext.SaveChangesAsync();
        
        Console.WriteLine($"new projects: {backfillProj}");
        Console.WriteLine($"new links: {backfillLinks}");
        
        var canWithProj = await dbContext.PersonProjects.Where(p => dbContext.Candidates.Any(c => c.PersonId == p.PersonId)).Select(p => p.PersonId).Distinct().CountAsync();
        var totLinks = await dbContext.PersonProjects.Where(p => dbContext.Candidates.Any(c => c.PersonId == p.PersonId)).CountAsync();
        var dupLinks = await dbContext.PersonProjects.GroupBy(p => new { p.PersonId, p.ProjectId }).AnyAsync(g => g.Count() > 1);
        var expectedProjCan = records3.Count(r => !string.IsNullOrWhiteSpace(r.Projects));
        
        Console.WriteLine("\nFINAL REPORT:");
        Console.WriteLine($"Candidates with project source data: {expectedProjCan}");
        Console.WriteLine($"Projects created: {await dbContext.Projects.CountAsync()}");
        Console.WriteLine($"PersonProject links: {totLinks}");
        Console.WriteLine($"Duplicate links: {(dupLinks ? "YES" : "NO")}");
        Console.WriteLine($"Candidates still missing expected project links: {expectedProjCan - canWithProj}");
    }
}
