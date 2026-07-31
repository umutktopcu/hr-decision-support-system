using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace HrDecisionSupport.Tests;

public class CoreDomainModelTests
{
    [Fact]
    public void Model_CanBeCreated_AndContainsAllExpectedEntities()
    {
        var model = CreateModel();
        var expectedEntityTypes = new[]
        {
            typeof(Department),
            typeof(Position),
            typeof(Person),
            typeof(Employee),
            typeof(Candidate),
            typeof(EmployeeAssignment),
            typeof(EmploymentHistory),
            typeof(Competency),
            typeof(PersonCompetency),
            typeof(EducationRecord),
            typeof(Certificate),
            typeof(PersonCertificate),
            typeof(Language),
            typeof(PersonLanguage),
            typeof(Project),
            typeof(PersonProject),
            typeof(Sector),
            typeof(PersonSectorExperience),
            typeof(WorkMode),
            typeof(PersonWorkModeExperience),
            typeof(JobRequisition),
            typeof(JobRequisitionRequirement),
            typeof(CandidateEvaluationCase)
        };

        Assert.All(expectedEntityTypes, type => Assert.NotNull(model.FindEntityType(type)));
    }

    [Theory]
    [InlineData(typeof(Employee), nameof(Employee.PersonId))]
    [InlineData(typeof(Candidate), nameof(Candidate.PersonId))]
    public void PersonSpecialization_IsOptionalOneToOne(Type dependentType, string foreignKeyName)
    {
        var model = CreateModel();
        var dependent = GetEntity(model, dependentType);
        var foreignKey = Assert.Single(
            dependent.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(Person));

        Assert.True(foreignKey.IsUnique);
        Assert.True(foreignKey.IsRequired);
        Assert.Equal(foreignKeyName, Assert.Single(foreignKey.Properties).Name);
    }

    [Fact]
    public void PersonCompetency_HasUniquePersonAndCompetencyIndex()
    {
        var entity = GetEntity<PersonCompetency>(CreateModel());
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(PersonCompetency.PersonId),
                    nameof(PersonCompetency.CompetencyId)
                ]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EmployeeAssignment_HasRequiredEmployeeDepartmentAndPositionRelationships()
    {
        var entity = GetEntity<EmployeeAssignment>(CreateModel());

        AssertRequiredRelationship<Employee>(entity, nameof(EmployeeAssignment.EmployeeId));
        AssertRequiredRelationship<Department>(entity, nameof(EmployeeAssignment.DepartmentId));
        AssertRequiredRelationship<Position>(entity, nameof(EmployeeAssignment.PositionId));
    }

    [Fact]
    public void JobRequisition_HasRequiredDepartmentAndPositionRelationships()
    {
        var entity = GetEntity<JobRequisition>(CreateModel());

        AssertRequiredRelationship<Department>(entity, nameof(JobRequisition.DepartmentId));
        AssertRequiredRelationship<Position>(entity, nameof(JobRequisition.PositionId));
    }

    [Fact]
    public void CandidateEvaluationCase_HasRequiredCandidateAndRequisitionRelationships()
    {
        var entity = GetEntity<CandidateEvaluationCase>(CreateModel());

        AssertRequiredRelationship<Candidate>(
            entity,
            nameof(CandidateEvaluationCase.CandidateId));
        AssertRequiredRelationship<JobRequisition>(
            entity,
            nameof(CandidateEvaluationCase.JobRequisitionId));
    }

    [Fact]
    public void CandidateEvaluationCase_HasUniqueCandidateAndRequisitionIndex()
    {
        var entity = GetEntity<CandidateEvaluationCase>(CreateModel());
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual(
                [
                    nameof(CandidateEvaluationCase.CandidateId),
                    nameof(CandidateEvaluationCase.JobRequisitionId)
                ]));

        Assert.True(index.IsUnique);
    }

    [Fact]
    public void CandidateEvaluationCase_StatusUsesCandidateEvaluationStatusEnum()
    {
        var entity = GetEntity<CandidateEvaluationCase>(CreateModel());
        var status = entity.FindProperty(nameof(CandidateEvaluationCase.Status));

        Assert.NotNull(status);
        Assert.Equal(typeof(CandidateEvaluationStatus), status.ClrType);
        Assert.False(status.IsNullable);
        Assert.Equal(typeof(int), status.GetTypeMapping().Converter?.ProviderClrType);
        Assert.Null(entity.FindProperty(nameof(CandidateEvaluationStatus)));
    }

    [Theory]
    [InlineData(
        typeof(Employee),
        "ck_employees_termination_date_not_before_hire_date",
        "termination_date IS NULL OR termination_date >= hire_date")]
    [InlineData(
        typeof(EmployeeAssignment),
        "ck_employee_assignments_end_date_not_before_start_date",
        "end_date IS NULL OR end_date >= start_date")]
    [InlineData(
        typeof(EmploymentHistory),
        "ck_employment_histories_end_date_not_before_start_date",
        "end_date IS NULL OR end_date >= start_date")]
    [InlineData(
        typeof(EducationRecord),
        "ck_education_records_graduation_date_not_before_start_date",
        "start_date IS NULL OR graduation_date IS NULL OR graduation_date >= start_date")]
    [InlineData(
        typeof(PersonProject),
        "ck_person_projects_end_date_not_before_start_date",
        "start_date IS NULL OR end_date IS NULL OR end_date >= start_date")]
    [InlineData(
        typeof(PersonCertificate),
        "ck_person_certificates_expiration_date_not_before_issue_date",
        "issue_date IS NULL OR expiration_date IS NULL OR expiration_date >= issue_date")]
    [InlineData(
        typeof(JobRequisition),
        "ck_job_requisitions_closed_at_not_before_opened_at",
        "closed_at IS NULL OR closed_at >= opened_at")]
    [InlineData(
        typeof(PersonCompetency),
        "ck_person_competencies_experience_months_non_negative",
        "experience_months IS NULL OR experience_months >= 0")]
    [InlineData(
        typeof(PersonSectorExperience),
        "ck_person_sector_experiences_experience_months_non_negative",
        "experience_months IS NULL OR experience_months >= 0")]
    [InlineData(
        typeof(PersonWorkModeExperience),
        "ck_person_work_mode_experiences_experience_months_non_negative",
        "experience_months IS NULL OR experience_months >= 0")]
    [InlineData(
        typeof(JobRequisitionRequirement),
        "ck_job_requisition_requirements_min_exp_months_non_negative",
        "minimum_experience_months IS NULL OR minimum_experience_months >= 0")]
    public void Model_HasExpectedCheckConstraint(
        Type entityType,
        string constraintName,
        string expectedSql)
    {
        var entity = GetEntity(CreateModel(), entityType);
        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate => candidate.Name == constraintName);

        Assert.Equal(expectedSql, constraint.Sql);
    }

    [Theory]
    [InlineData(typeof(Person))]
    [InlineData(typeof(JobRequisition))]
    [InlineData(typeof(CandidateEvaluationCase))]
    public void AuditProperties_HaveRequiredCreatedAtAndNullableUpdatedAt(Type entityType)
    {
        var entity = GetEntity(CreateModel(), entityType);
        var createdAt = entity.FindProperty("CreatedAtUtc");
        var updatedAt = entity.FindProperty("UpdatedAtUtc");

        Assert.NotNull(createdAt);
        Assert.Equal(typeof(DateTime), createdAt.ClrType);
        Assert.False(createdAt.IsNullable);
        Assert.NotNull(updatedAt);
        Assert.Equal(typeof(DateTime?), updatedAt.ClrType);
        Assert.True(updatedAt.IsNullable);
    }

    [Fact]
    public void CertificateIssuer_IsNullable()
    {
        var issuer = GetEntity<Certificate>(CreateModel())
            .FindProperty(nameof(Certificate.Issuer));

        Assert.NotNull(issuer);
        Assert.True(issuer.IsNullable);
    }

    [Fact]
    public void JobRequisition_OpeningsCountMustBePositive()
    {
        var entity = GetEntity<JobRequisition>(CreateModel());
        var constraint = Assert.Single(
            entity.GetCheckConstraints(),
            candidate => candidate.Name == "ck_job_requisitions_openings_count");

        Assert.Equal("openings_count > 0", constraint.Sql);
    }

    [Fact]
    public void PersonCertificate_UsesIndependentKeyAndAllowsRepeatedCertificates()
    {
        var entity = GetEntity<PersonCertificate>(CreateModel());
        var primaryKey = entity.FindPrimaryKey();

        Assert.NotNull(primaryKey);
        Assert.Equal(nameof(PersonCertificate.Id), Assert.Single(primaryKey.Properties).Name);
        Assert.DoesNotContain(
            entity.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual(
                    [
                        nameof(PersonCertificate.PersonId),
                        nameof(PersonCertificate.CertificateId)
                    ]));
    }

    [Fact]
    public void TablesAndColumns_UseSnakeCaseNames()
    {
        var model = CreateModel();

        Assert.All(
            model.GetEntityTypes(),
            entity =>
            {
                var tableName = entity.GetTableName();
                Assert.NotNull(tableName);
                Assert.Matches("^[a-z0-9_]+$", tableName);

                var table = StoreObjectIdentifier.Table(tableName, entity.GetSchema());
                Assert.All(
                    entity.GetProperties(),
                    property =>
                    {
                        var columnName = property.GetColumnName(table);
                        Assert.NotNull(columnName);
                        Assert.Matches("^[a-z0-9_]+$", columnName);
                    });
            });
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql()
            .Options;
        using var context = new HrDecisionSupportDbContext(options);

        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType GetEntity<TEntity>(IModel model)
        where TEntity : class
    {
        return GetEntity(model, typeof(TEntity));
    }

    private static IEntityType GetEntity(IModel model, Type type)
    {
        return model.FindEntityType(type)
            ?? throw new InvalidOperationException($"{type.Name} is missing from the model.");
    }

    private static void AssertRequiredRelationship<TPrincipal>(
        IEntityType dependent,
        string foreignKeyName)
    {
        var foreignKey = Assert.Single(
            dependent.GetForeignKeys(),
            key => key.PrincipalEntityType.ClrType == typeof(TPrincipal));

        Assert.True(foreignKey.IsRequired);
        Assert.Equal(foreignKeyName, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }
}
