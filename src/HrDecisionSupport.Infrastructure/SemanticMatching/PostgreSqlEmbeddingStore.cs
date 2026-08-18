using System.Data;
using System.Data.Common;
using System.Text;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Pgvector;

namespace HrDecisionSupport.Infrastructure.SemanticMatching;

public sealed class PostgreSqlEmbeddingStore(HrDecisionSupportDbContext dbContext) : IEmbeddingStore
{
    public async Task<IReadOnlyDictionary<Guid, StoredEmbedding>> GetCandidateEmbeddingsAsync(
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default)
    {
        if (candidateIds.Count == 0)
            return new Dictionary<Guid, StoredEmbedding>();

        var ids = candidateIds.Distinct().ToArray();
        var rows = await dbContext.CandidateEmbeddings
            .AsNoTracking()
            .Where(embedding => ids.Contains(embedding.CandidateId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            embedding => embedding.CandidateId,
            embedding => new StoredEmbedding(
                embedding.CandidateId,
                embedding.DocumentHash,
                embedding.ModelName,
                embedding.EmbeddingVector.ToArray(),
                embedding.UpdatedAtUtc));
    }

    public async Task<StoredEmbedding?> GetJobRequisitionEmbeddingAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default)
    {
        var embedding = await dbContext.JobRequisitionEmbeddings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.JobRequisitionId == jobRequisitionId,
                cancellationToken);

        return embedding is null
            ? null
            : new StoredEmbedding(
                embedding.JobRequisitionId,
                embedding.DocumentHash,
                embedding.ModelName,
                embedding.EmbeddingVector.ToArray(),
                embedding.UpdatedAtUtc);
    }

    public Task UpsertCandidateEmbeddingsAsync(
        IReadOnlyCollection<StoredEmbedding> embeddings,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "candidate_embeddings",
            "candidate_id",
            embeddings,
            cancellationToken);

    public Task UpsertJobRequisitionEmbeddingAsync(
        StoredEmbedding embedding,
        CancellationToken cancellationToken = default) =>
        UpsertAsync(
            "job_requisition_embeddings",
            "job_requisition_id",
            new[] { embedding },
            cancellationToken);

    private async Task UpsertAsync(
        string tableName,
        string idColumn,
        IReadOnlyCollection<StoredEmbedding> embeddings,
        CancellationToken cancellationToken)
    {
        if (embeddings.Count == 0)
            return;

        if (embeddings.Select(embedding => embedding.EntityId).Distinct().Count() != embeddings.Count)
            throw new ArgumentException("Embedding upsert contains duplicate entity IDs.", nameof(embeddings));

        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State == ConnectionState.Closed;
        if (shouldCloseConnection)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = BuildUpsertSql(command, tableName, idColumn, embeddings);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldCloseConnection)
                await connection.CloseAsync();
        }
    }

    private static string BuildUpsertSql(
        DbCommand command,
        string tableName,
        string idColumn,
        IReadOnlyCollection<StoredEmbedding> embeddings)
    {
        var sql = new StringBuilder()
            .Append("INSERT INTO ").Append(tableName)
            .Append(" (").Append(idColumn)
            .Append(", document_hash, model_name, embedding_vector, updated_at_utc) VALUES ");

        var index = 0;
        foreach (var embedding in embeddings)
        {
            if (index > 0)
                sql.Append(", ");

            sql.Append("(@id_").Append(index)
                .Append(", @hash_").Append(index)
                .Append(", @model_").Append(index)
                .Append(", @vector_").Append(index)
                .Append(", @updated_").Append(index).Append(')');

            AddParameter(command, $"id_{index}", embedding.EntityId);
            AddParameter(command, $"hash_{index}", embedding.DocumentHash);
            AddParameter(command, $"model_{index}", embedding.ModelName);
            AddParameter(command, $"vector_{index}", new Vector(embedding.Vector));
            AddParameter(command, $"updated_{index}", embedding.UpdatedAtUtc);
            index++;
        }

        sql.Append(" ON CONFLICT (").Append(idColumn).Append(") DO UPDATE SET ")
            .Append("document_hash = EXCLUDED.document_hash, ")
            .Append("model_name = EXCLUDED.model_name, ")
            .Append("embedding_vector = EXCLUDED.embedding_vector, ")
            .Append("updated_at_utc = EXCLUDED.updated_at_utc;");

        return sql.ToString();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = new NpgsqlParameter(name, value);
        command.Parameters.Add(parameter);
    }
}
