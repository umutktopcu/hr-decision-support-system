using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using HrDecisionSupport.Application;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Domain.Enums;

namespace JobRequisitionSeeder;

class Program
{
    static async Task Main(string[] args)
    {
        var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "HrDecisionSupport.Web"));

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.SetBasePath(basePath);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                config.AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddApplication();
                services.AddInfrastructure(options =>
                {
                    var connStr = context.Configuration.GetConnectionString("PostgreSql") ?? context.Configuration.GetConnectionString("DefaultConnection");
                    options.UseNpgsql(connStr);
                });
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IHrDecisionSupportDbContext>();
        var reqService = scope.ServiceProvider.GetRequiredService<IJobRequisitionService>();

        Console.WriteLine("JobRequisition Seed Aracı Başlatıldı...");

        // Department ve Position lookup
        var itDept = await dbContext.Departments.FirstOrDefaultAsync(d => d.Code == "IT");
        var backendPos = await dbContext.Positions.FirstOrDefaultAsync(p => p.Code == "BACKEND_DEVELOPER");

        if (itDept == null || backendPos == null)
        {
            Console.WriteLine("HATA: IT department veya BACKEND_DEVELOPER position bulunamadı. Lütfen catalog verilerinin db'de olduğundan emin olun.");
            return;
        }

        var csharp = await dbContext.Competencies.FirstOrDefaultAsync(c => c.Code == "C_SHARP");
        var dotnetcore = await dbContext.Competencies.FirstOrDefaultAsync(c => c.Code == "DOTNET_CORE");
        var aspnetcore = await dbContext.Competencies.FirstOrDefaultAsync(c => c.Code == "ASP_NET_CORE");

        if (csharp == null || dotnetcore == null || aspnetcore == null)
        {
            Console.WriteLine("HATA: Gerekli Competency'ler bulunamadı (C_SHARP, DOTNET_CORE, ASP_NET_CORE).");
            return;
        }

        var dummyCode = "BD-TEST-001";
        var existingDummy = await dbContext.JobRequisitions
            .Include(r => r.Requirements)
            .FirstOrDefaultAsync(r => r.RequisitionCode == dummyCode);

        Guid dummyId;

        if (existingDummy != null)
        {
            Console.WriteLine($"Zaten '{dummyCode}' kodlu bir JobRequisition mevcut: {existingDummy.Id}");
            dummyId = existingDummy.Id;
        }
        else
        {
            Console.WriteLine("Uygun dummy Requisition bulunamadı, yenisi oluşturuluyor...");
            var request = new CreateJobRequisitionRequest(
                RequisitionCode: dummyCode,
                Title: "Backend Developer (Dummy)",
                DepartmentId: itDept.Id,
                PositionId: backendPos.Id,
                Description: "Dummy pre-screening requisition",
                OpeningsCount: 1,
                MinimumRelevantExperienceMonths: 24,
                OpenedAt: DateOnly.FromDateTime(DateTime.Today),
                ClosedAt: null,
                MandatorySkillCoverageThreshold: 0.50m,
                OverallSkillCoverageThreshold: 0.50m
            );

            var result = await reqService.CreateAsync(request);
            if (result.IsFailure)
            {
                Console.WriteLine("HATA: Dummy JobRequisition oluşturulamadı:");
                if (result.Errors != null)
                    foreach (var err in result.Errors) Console.WriteLine($"- {err.Code}: {err.Message}");
                return;
            }
            dummyId = result.Value.Id;

            await reqService.ChangeStatusAsync(dummyId, new ChangeJobRequisitionStatusRequest(JobRequisitionStatus.Open, null));
            Console.WriteLine("Dummy JobRequisition başarıyla oluşturuldu ve Open yapıldı.");
        }

        // Add Requirements idempotently
        await EnsureRequirement(dbContext, dummyId, csharp.Id, true);
        await EnsureRequirement(dbContext, dummyId, dotnetcore.Id, true);
        await EnsureRequirement(dbContext, dummyId, aspnetcore.Id, false);

        Console.WriteLine("Dummy JobRequisition seeding tamamlandı.");
    }

    static async Task EnsureRequirement(IHrDecisionSupportDbContext dbContext, Guid reqId, Guid compId, bool mandatory)
    {
        var existingReq = await dbContext.JobRequisitionRequirements
            .FirstOrDefaultAsync(r => r.JobRequisitionId == reqId && r.CompetencyId == compId);

        if (existingReq == null)
        {
            dbContext.JobRequisitionRequirements.Add(new HrDecisionSupport.Domain.Entities.JobRequisitionRequirement
            {
                Id = Guid.NewGuid(),
                JobRequisitionId = reqId,
                CompetencyId = compId,
                IsRequired = mandatory
            });
            await dbContext.SaveChangesAsync();
            Console.WriteLine($"Added requirement: {compId} (Mandatory: {mandatory})");
        }
    }
}
