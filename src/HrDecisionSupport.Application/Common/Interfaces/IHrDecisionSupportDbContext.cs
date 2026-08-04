using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Common.Interfaces;

public interface IHrDecisionSupportDbContext
{
    DbSet<Person> People { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Candidate> Candidates { get; }
    DbSet<EmployeeAssignment> EmployeeAssignments { get; }
    DbSet<Department> Departments { get; }
    DbSet<Position> Positions { get; }
    DbSet<JobRequisition> JobRequisitions { get; }
    DbSet<CandidateEvaluationCase> CandidateEvaluationCases { get; }
    DbSet<Competency> Competencies { get; }
    DbSet<PersonCompetency> PersonCompetencies { get; }
    DbSet<EducationRecord> EducationRecords { get; }
    DbSet<Certificate> Certificates { get; }
    DbSet<PersonCertificate> PersonCertificates { get; }
    DbSet<Language> Languages { get; }
    DbSet<PersonLanguage> PersonLanguages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
