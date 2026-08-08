using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Infrastructure.Persistence;

public class HrDecisionSupportDbContext : DbContext, IHrDecisionSupportDbContext
{
    public HrDecisionSupportDbContext(DbContextOptions<HrDecisionSupportDbContext> options)
        : base(options)
    {
    }

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<EmployeeAssignment> EmployeeAssignments => Set<EmployeeAssignment>();
    public DbSet<EmploymentHistory> EmploymentHistories => Set<EmploymentHistory>();
    public DbSet<Competency> Competencies => Set<Competency>();
    public DbSet<PersonCompetency> PersonCompetencies => Set<PersonCompetency>();
    public DbSet<EducationRecord> EducationRecords => Set<EducationRecord>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    public DbSet<PersonCertificate> PersonCertificates => Set<PersonCertificate>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<PersonLanguage> PersonLanguages => Set<PersonLanguage>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PersonProject> PersonProjects => Set<PersonProject>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<PersonSectorExperience> PersonSectorExperiences => Set<PersonSectorExperience>();
    public DbSet<WorkMode> WorkModes => Set<WorkMode>();
    public DbSet<PersonWorkModeExperience> PersonWorkModeExperiences =>
        Set<PersonWorkModeExperience>();
    public DbSet<EmployeeImportBatch> EmployeeImportBatches => Set<EmployeeImportBatch>();
    public DbSet<EmployeeImportRow> EmployeeImportRows => Set<EmployeeImportRow>();
    public DbSet<PersonPriorPositionEvidence> PersonPriorPositionEvidences =>
        Set<PersonPriorPositionEvidence>();
    public DbSet<EmployeeCareerFeatureSnapshot> EmployeeCareerFeatureSnapshots =>
        Set<EmployeeCareerFeatureSnapshot>();
    public DbSet<EmployeeRetentionLabel> EmployeeRetentionLabels => Set<EmployeeRetentionLabel>();
    public DbSet<JobRequisition> JobRequisitions => Set<JobRequisition>();
    public DbSet<JobRequisitionRequirement> JobRequisitionRequirements =>
        Set<JobRequisitionRequirement>();
    public DbSet<CandidateEvaluationCase> CandidateEvaluationCases =>
        Set<CandidateEvaluationCase>();
    public DbSet<CandidateWorkModePreference> CandidateWorkModePreferences =>
        Set<CandidateWorkModePreference>();
    public DbSet<CandidateCareerFeatureSnapshot> CandidateCareerFeatureSnapshots =>
        Set<CandidateCareerFeatureSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HrDecisionSupportDbContext).Assembly);
    }
}
