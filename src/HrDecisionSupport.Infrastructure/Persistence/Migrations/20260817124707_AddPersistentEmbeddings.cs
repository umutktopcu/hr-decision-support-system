using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "candidate_embeddings",
                columns: table => new
                {
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_vector = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_embeddings", x => x.candidate_id);
                    table.ForeignKey(
                        name: "FK_candidate_embeddings_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_requisition_embeddings",
                columns: table => new
                {
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    embedding_vector = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requisition_embeddings", x => x.job_requisition_id);
                    table.ForeignKey(
                        name: "FK_job_requisition_embeddings_job_requisitions_job_requisition~",
                        column: x => x.job_requisition_id,
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_embeddings");

            migrationBuilder.DropTable(
                name: "job_requisition_embeddings");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
