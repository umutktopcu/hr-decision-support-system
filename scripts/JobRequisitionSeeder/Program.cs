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

        // Mevcut Requisition kontrolü
        var existing = await dbContext.JobRequisitions
            .FirstOrDefaultAsync(r => r.RequisitionCode == "BD-2026-001");

        if (existing != null)
        {
            Console.WriteLine($"Zaten 'BD-2026-001' kodlu bir JobRequisition mevcut: {existing.Id} (Status: {existing.JobRequisitionStatus})");
            if (existing.JobRequisitionStatus != JobRequisitionStatus.Open)
            {
                Console.WriteLine("Status 'Open' değil, güncelleniyor...");
                var statusUpdate = await reqService.ChangeStatusAsync(existing.Id, new ChangeJobRequisitionStatusRequest(JobRequisitionStatus.Open, null));
                if (statusUpdate.IsFailure)
                {
                    Console.WriteLine("UYARI: Status 'Open' yapılamadı.");
                    Report(existing.Id, existing.DepartmentId, existing.PositionId, existing.RequisitionCode, existing.JobRequisitionStatus.ToString());
                    return;
                }
                Report(statusUpdate.Value.Id, statusUpdate.Value.DepartmentId, statusUpdate.Value.PositionId, statusUpdate.Value.RequisitionCode, statusUpdate.Value.JobRequisitionStatus.ToString());
            }
            else
            {
                Report(existing.Id, existing.DepartmentId, existing.PositionId, existing.RequisitionCode, existing.JobRequisitionStatus.ToString());
            }
            return;
        }

        // Yeni oluştur
        Console.WriteLine("Uygun Requisition bulunamadı, yenisi oluşturuluyor...");
        var request = new CreateJobRequisitionRequest(
            RequisitionCode: "BD-2026-001",
            Title: "Backend Developer",
            DepartmentId: itDept.Id,
            PositionId: backendPos.Id,
            Description: "Candidate dataset development requisition",
            OpeningsCount: 1,
            OpenedAt: DateOnly.FromDateTime(DateTime.Today)
        );

        var result = await reqService.CreateAsync(request);
        if (result.IsFailure)
        {
            Console.WriteLine("HATA: JobRequisition oluşturulamadı:");
            if (result.Errors != null) 
                foreach (var err in result.Errors) Console.WriteLine($"- {err.Code}: {err.Message}");
            return;
        }
        
        var created = result.Value;
        
        // Status Draft olarak oluşuyor, Open'a çekmemiz lazım
        Console.WriteLine("Draft requisition oluşturuldu. Status 'Open' olarak güncelleniyor...");
        var statusResult = await reqService.ChangeStatusAsync(created.Id, new ChangeJobRequisitionStatusRequest(JobRequisitionStatus.Open, null));
        
        if (statusResult.IsFailure)
        {
            Console.WriteLine("UYARI: Status 'Open' yapılamadı.");
            Report(created.Id, created.DepartmentId, created.PositionId, created.RequisitionCode, created.JobRequisitionStatus.ToString());
            return;
        }

        Console.WriteLine("JobRequisition başarıyla oluşturuldu ve Open yapıldı!");
        Report(statusResult.Value.Id, statusResult.Value.DepartmentId, statusResult.Value.PositionId, statusResult.Value.RequisitionCode, statusResult.Value.JobRequisitionStatus.ToString());
    }

    static void Report(Guid id, Guid deptId, Guid posId, string code, string status)
    {
        Console.WriteLine("\n--- RAPOR ---");
        Console.WriteLine($"Requisition Id : {id}");
        Console.WriteLine($"Department Id  : {deptId}");
        Console.WriteLine($"Position Id    : {posId}");
        Console.WriteLine($"RequisitionCode: {code}");
        Console.WriteLine($"Status         : {status}");
        Console.WriteLine("-----------------\n");
    }
}
