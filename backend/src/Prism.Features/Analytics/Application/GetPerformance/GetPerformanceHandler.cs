using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Analytics.Application.Dtos;

namespace Prism.Features.Analytics.Application.GetPerformance;

/// <summary>
/// Query for latency and throughput statistics over recorded usage.
/// </summary>
/// <param name="From">Inclusive start of the window. Defaults to 30 days ago.</param>
/// <param name="To">Inclusive end of the window. Defaults to now.</param>
/// <param name="Model">Optional model filter.</param>
public sealed record GetPerformanceQuery(DateTime? From, DateTime? To, string? Model);

/// <summary>
/// Computes latency percentiles and throughput averages from the usage log.
/// </summary>
/// <remarks>
/// Aggregation happens in PostgreSQL. The previous implementation loaded every matching row
/// into memory, sorted the whole set, then sorted each model's rows again — which is fine
/// against an empty table and untenable once the log holds real traffic.
/// </remarks>
public sealed class GetPerformanceHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPerformanceHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    public GetPerformanceHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Handles the get performance query.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A summary of latency and throughput.</returns>
    public async Task<Result<PerformanceSummaryDto>> HandleAsync(
        GetPerformanceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        DateTime from = query.From ?? DateTime.UtcNow.AddDays(-30);
        DateTime to = query.To ?? DateTime.UtcNow;

        // GROUPING SETS returns the per-model rows and the overall row from one scan. Running
        // two queries would also read the table twice and could disagree if rows land between.
        const string sql = """
            SELECT
                COALESCE("Model", '') AS model,
                GROUPING("Model") AS is_total,
                COUNT(*) AS request_count,
                AVG("LatencyMs")::float8 AS mean_latency,
                percentile_cont(0.50) WITHIN GROUP (ORDER BY "LatencyMs")::float8 AS p50,
                percentile_cont(0.95) WITHIN GROUP (ORDER BY "LatencyMs")::float8 AS p95,
                percentile_cont(0.99) WITHIN GROUP (ORDER BY "LatencyMs")::float8 AS p99,
                AVG("TtftMs")::float8 AS mean_ttft,
                AVG("TokensPerSecond")::float8 AS mean_throughput
            FROM analytics_usage_logs
            WHERE "CreatedAt" >= @from
              AND "CreatedAt" <= @to
              AND (@model::text IS NULL OR "Model" = @model::text)
            GROUP BY GROUPING SETS (("Model"), ())
            ORDER BY is_total DESC, request_count DESC;
            """;

        DbConnection connection = _db.Database.GetDbConnection();
        bool opened = false;

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            opened = true;
        }

        try
        {
            await using DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            AddParameter(command, "@from", from);
            AddParameter(command, "@to", to);
            AddParameter(command, "@model", (object?)query.Model ?? DBNull.Value);

            var byModel = new List<PerformanceByModelDto>();
            PerformanceSummaryDto? total = null;

            await using DbDataReader reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                bool isTotal = reader.GetInt32(1) == 1;
                int count = (int)reader.GetInt64(2);
                double mean = ReadDouble(reader, 3);
                double p50 = ReadDouble(reader, 4);
                double p95 = ReadDouble(reader, 5);
                double p99 = ReadDouble(reader, 6);
                double? ttft = reader.IsDBNull(7) ? null : reader.GetDouble(7);
                double? throughput = reader.IsDBNull(8) ? null : reader.GetDouble(8);

                if (isTotal)
                {
                    total = new PerformanceSummaryDto(mean, p50, p95, p99, ttft, throughput, []);
                }
                else
                {
                    byModel.Add(new PerformanceByModelDto(
                        reader.GetString(0), count, mean, p50, p95, ttft ?? 0, throughput ?? 0));
                }
            }

            if (total is null || byModel.Count == 0)
            {
                return new PerformanceSummaryDto(0, 0, 0, 0, null, null, []);
            }

            return total with { ByModel = byModel };
        }
        finally
        {
            if (opened)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static double ReadDouble(DbDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? 0 : reader.GetDouble(ordinal);

    private static void AddParameter(DbCommand command, string name, object value)
    {
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
