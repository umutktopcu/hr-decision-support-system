using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CandidateEvaluationCaseConfiguration
    : IEntityTypeConfiguration<CandidateEvaluationCase>
{
    public void Configure(EntityTypeBuilder<CandidateEvaluationCase> builder)
    {
        builder.ToTable("candidate_evaluation_cases");
        builder.HasKey(evaluationCase => evaluationCase.Id);

        builder.Property(evaluationCase => evaluationCase.Id).IsRequired();
        builder.Property(evaluationCase => evaluationCase.CandidateId).IsRequired();
        builder.Property(evaluationCase => evaluationCase.JobRequisitionId).IsRequired();
        builder.Property(evaluationCase => evaluationCase.ExternalReference)
            .HasMaxLength(200)
            .IsRequired(false);
        builder.Property(evaluationCase => evaluationCase.ReceivedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(evaluationCase => evaluationCase.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(evaluationCase => evaluationCase.Notes).HasMaxLength(2000).IsRequired(false);
        builder.Property(evaluationCase => evaluationCase.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(evaluationCase => evaluationCase.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasIndex(evaluationCase => new
        {
            evaluationCase.CandidateId,
            evaluationCase.JobRequisitionId
        }).IsUnique();

        builder.HasOne(evaluationCase => evaluationCase.Candidate)
            .WithMany(candidate => candidate.EvaluationCases)
            .HasForeignKey(evaluationCase => evaluationCase.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(evaluationCase => evaluationCase.JobRequisition)
            .WithMany(requisition => requisition.CandidateEvaluationCases)
            .HasForeignKey(evaluationCase => evaluationCase.JobRequisitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
