using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class EmployeeExcelImportFoundationTests
{
    [Fact]
    public void ImportFoundationEntities_HaveGuidPrimaryKeysAndSnakeCaseTables()
    {
        var model = CreateModel();
        var entityTypes = new[]
        {
            typeof(EmployeeImportBatch),
            typeof(EmployeeImportRow),
            typeof(PersonPriorPositionEvidence),
            typeof(EmployeeCareerFeatureSnapshot),
            typeof(EmployeeRetentionLabel)
        };

        foreach (var entityType in entityTypes)
        {
            var entity = GetEntity(model, entityType);
            var primaryKey = entity.FindPrimaryKey();
            Assert.NotNull(primaryKey);
            Assert.Equal(nameof(EmployeeImportBatch.Id), Assert.Single(primaryKey!.Properties).Name);
            Assert.Equal(typeof(Guid), Assert.Single(primaryKey.Properties).ClrType);
            var tableName = entity.GetTableName();
            Assert.NotNull(tableName);
            Assert.Matches("^[a-z0-9_]+$", tableName!);
        }
    }

    [Fact]
    public void ImportFoundationRelationships_AreRequiredWhereSpecifiedAndRestrict()
    {
        var model = CreateModel();

        AssertRelationship<EmployeeImportRow, EmployeeImportBatch>(
            model, nameof(EmployeeImportRow.ImportBatchId), required: true);
        AssertRelationship<EmployeeImportRow, Employee>(
            model, nameof(EmployeeImportRow.EmployeeId), required: false);
        AssertRelationship<PersonPriorPositionEvidence, Person>(
            model, nameof(PersonPriorPositionEvidence.PersonId), required: true);
        AssertRelationship<PersonPriorPositionEvidence, EmployeeImportRow>(
            model, nameof(PersonPriorPositionEvidence.ImportRowId), required: true);
        AssertRelationship<EmployeeCareerFeatureSnapshot, Employee>(
            model, nameof(EmployeeCareerFeatureSnapshot.EmployeeId), required: true);
        AssertRelationship<EmployeeCareerFeatureSnapshot, EmployeeImportBatch>(
            model, nameof(EmployeeCareerFeatureSnapshot.ImportBatchId), required: true);
        AssertRelationship<EmployeeRetentionLabel, EmployeeCareerFeatureSnapshot>(
            model, nameof(EmployeeRetentionLabel.EmployeeCareerFeatureSnapshotId), required: true);

        var evidence = GetEntity<PersonPriorPositionEvidence>(model);
        Assert.DoesNotContain(
            evidence.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(EmploymentHistory));
    }

    [Fact]
    public void ImportFoundationEnums_UseIntegerConversions()
    {
        var model = CreateModel();

        AssertIntEnum<EmployeeImportBatch>(model, nameof(EmployeeImportBatch.DatasetSplit));
        AssertIntEnum<EmployeeImportBatch>(model, nameof(EmployeeImportBatch.Status));
        AssertIntEnum<EmployeeImportRow>(model, nameof(EmployeeImportRow.ImportStatus));
        AssertIntEnum<EmployeeCareerFeatureSnapshot>(model, nameof(EmployeeCareerFeatureSnapshot.FeatureSource));
        AssertIntEnum<EmployeeRetentionLabel>(model, nameof(EmployeeRetentionLabel.Label));
        AssertIntEnum<EmployeeRetentionLabel>(model, nameof(EmployeeRetentionLabel.LabelSource));
    }

    [Fact]
    public void ImportFoundationIndexes_HaveRequiredUniquenessAndPartialFilter()
    {
        var model = CreateModel();

        AssertUniqueIndex<EmployeeImportBatch>(model, nameof(EmployeeImportBatch.FileHash));
        AssertUniqueIndex<EmployeeImportRow>(
            model,
            nameof(EmployeeImportRow.ImportBatchId),
            nameof(EmployeeImportRow.SourceRowNumber));
        AssertUniqueIndex<PersonPriorPositionEvidence>(
            model,
            nameof(PersonPriorPositionEvidence.PersonId),
            nameof(PersonPriorPositionEvidence.SequenceNumber),
            nameof(PersonPriorPositionEvidence.Title));
        AssertUniqueIndex<EmployeeCareerFeatureSnapshot>(
            model,
            nameof(EmployeeCareerFeatureSnapshot.EmployeeId),
            nameof(EmployeeCareerFeatureSnapshot.ImportBatchId),
            nameof(EmployeeCareerFeatureSnapshot.FeatureSchemaVersion));
        AssertUniqueIndex<EmployeeRetentionLabel>(
            model,
            nameof(EmployeeRetentionLabel.EmployeeCareerFeatureSnapshotId));

        var row = GetEntity<EmployeeImportRow>(model);
        var partialIndex = Assert.Single(
            row.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(EmployeeImportRow.ImportBatchId), nameof(EmployeeImportRow.ExternalEmployeeCode)]));
        Assert.True(partialIndex.IsUnique);
        Assert.Equal("\"external_employee_code\" IS NOT NULL", partialIndex.GetFilter());
    }

    [Fact]
    public void ImportFoundationJsonColumnsAndConstraints_AreConfigured()
    {
        var model = CreateModel();
        var row = GetEntity<EmployeeImportRow>(model);
        Assert.Equal("jsonb", row.FindProperty(nameof(EmployeeImportRow.RawPayloadJson))!.GetColumnType());
        Assert.Equal("jsonb", row.FindProperty(nameof(EmployeeImportRow.ValidationErrorsJson))!.GetColumnType());

        AssertConstraint<EmployeeImportBatch>(model, "ck_employee_import_batches_row_counts_non_negative");
        AssertConstraint<EmployeeImportBatch>(model, "ck_emp_import_batch_completed_counts_lte_total");
        AssertConstraint<EmployeeImportBatch>(model, "ck_employee_import_batches_file_hash_lowercase_sha256");
        AssertConstraint<EmployeeImportRow>(model, "ck_employee_import_rows_source_row_number_positive");
        AssertConstraint<PersonPriorPositionEvidence>(
            model,
            "ck_person_prior_position_evidences_sequence_number_positive");
        AssertConstraint<EmployeeCareerFeatureSnapshot>(
            model,
            "ck_employee_career_feature_snapshots_months_non_negative");
        AssertConstraint<EmployeeCareerFeatureSnapshot>(
            model,
            "ck_emp_feature_backend_lte_total");
        AssertConstraint<EmployeeCareerFeatureSnapshot>(
            model,
            "ck_emp_feature_shortest_lte_longest");
        AssertConstraint<EmployeeRetentionLabel>(model, "ck_employee_retention_labels_label_valid");
    }

    [Fact]
    public void ImportFoundationNullability_MatchesProductDecisions()
    {
        var model = CreateModel();

        Assert.False(GetEntity<EmployeeAssignment>(model)
            .FindProperty(nameof(EmployeeAssignment.DepartmentId))!.IsNullable);
        Assert.True(GetEntity<EmployeeAssignment>(model)
            .FindProperty(nameof(EmployeeAssignment.StartDate))!.IsNullable);
        Assert.False(GetEntity<Employee>(model)
            .FindProperty(nameof(Employee.HireDate))!.IsNullable);
        Assert.True(GetEntity<EducationRecord>(model)
            .FindProperty(nameof(EducationRecord.Institution))!.IsNullable);
        Assert.True(GetEntity<PersonLanguage>(model)
            .FindProperty(nameof(PersonLanguage.ProficiencyLevel))!.IsNullable);
        Assert.Equal(typeof(LanguageProficiencyLevel?), GetEntity<PersonLanguage>(model)
            .FindProperty(nameof(PersonLanguage.ProficiencyLevel))!.ClrType);
        Assert.Equal(typeof(CompetencyProficiencyLevel?), GetEntity<PersonCompetency>(model)
            .FindProperty(nameof(PersonCompetency.ProficiencyLevel))!.ClrType);
        Assert.Equal(typeof(CompetencyProficiencyLevel?), GetEntity<JobRequisitionRequirement>(model)
            .FindProperty(nameof(JobRequisitionRequirement.MinimumProficiencyLevel))!.ClrType);
        Assert.False(GetEntity<PersonLanguage>(model)
            .FindProperty(nameof(PersonLanguage.IsNative))!.IsNullable);
        Assert.True(GetEntity<EmployeeImportBatch>(model)
            .FindProperty(nameof(EmployeeImportBatch.ObservationDate))!.IsNullable);
        Assert.True(GetEntity<EmployeeCareerFeatureSnapshot>(model)
            .FindProperty(nameof(EmployeeCareerFeatureSnapshot.ObservedAt))!.IsNullable);
    }

    [Fact]
    public void CompetencyProficiencyLevel_UsesOnlyTechnicalValues()
    {
        Assert.Equal(
            [
                CompetencyProficiencyLevel.Beginner,
                CompetencyProficiencyLevel.Elementary,
                CompetencyProficiencyLevel.Intermediate,
                CompetencyProficiencyLevel.Advanced,
                CompetencyProficiencyLevel.Expert
            ],
            Enum.GetValues<CompetencyProficiencyLevel>());
    }

    [Fact]
    public void LanguageProficiencyLevel_UsesOnlyCefrValues()
    {
        Assert.Equal(
            [
                LanguageProficiencyLevel.A1,
                LanguageProficiencyLevel.A2,
                LanguageProficiencyLevel.B1,
                LanguageProficiencyLevel.B2,
                LanguageProficiencyLevel.C1,
                LanguageProficiencyLevel.C2
            ],
            Enum.GetValues<LanguageProficiencyLevel>());
    }

    [Fact]
    public void NativeLanguage_CanHaveNoCefrLevelInTheDomainModel()
    {
        var language = new PersonLanguage
        {
            IsNative = true,
            ProficiencyLevel = null
        };

        Assert.True(language.IsNative);
        Assert.Null(language.ProficiencyLevel);
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(npgsqlOptions => npgsqlOptions.UseVector())
            .Options;
        using var context = new HrDecisionSupportDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType GetEntity<TEntity>(IModel model)
        where TEntity : class => GetEntity(model, typeof(TEntity));

    private static IEntityType GetEntity(IModel model, Type entityType) =>
        model.FindEntityType(entityType)
        ?? throw new InvalidOperationException($"{entityType.Name} is missing from the model.");

    private static void AssertRelationship<TDependent, TPrincipal>(
        IModel model,
        string foreignKeyProperty,
        bool required)
        where TDependent : class
        where TPrincipal : class
    {
        var dependent = GetEntity<TDependent>(model);
        var foreignKey = Assert.Single(
            dependent.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType.ClrType == typeof(TPrincipal));

        Assert.Equal(required, foreignKey.IsRequired);
        Assert.Equal(foreignKeyProperty, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static void AssertIntEnum<TEntity>(IModel model, string propertyName)
        where TEntity : class
    {
        var property = GetEntity<TEntity>(model).FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(typeof(int), property.GetTypeMapping().Converter?.ProviderClrType);
    }

    private static void AssertUniqueIndex<TEntity>(IModel model, params string[] propertyNames)
        where TEntity : class
    {
        var index = Assert.Single(
            GetEntity<TEntity>(model).GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        Assert.True(index.IsUnique);
    }

    private static void AssertConstraint<TEntity>(IModel model, string constraintName)
        where TEntity : class
    {
        Assert.Contains(
            GetEntity<TEntity>(model).GetCheckConstraints(),
            constraint => constraint.Name == constraintName);
    }
}
