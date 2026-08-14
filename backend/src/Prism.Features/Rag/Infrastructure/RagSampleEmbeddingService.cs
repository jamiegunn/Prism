using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Infrastructure;

/// <summary>
/// Retries embedding the sample RAG collection shortly after startup.
/// </summary>
/// <remarks>
/// <para>
/// Seeders run before any inference server has been health-checked, and on a first launch the
/// server frequently is not up yet — so the sample was seeded, correctly reported itself as not
/// embedded, and then stayed that way until someone restarted the application. A new user has no
/// reason to know that a restart is what stands between them and semantic search, so the whole
/// feature reads as broken on exactly the install where first impressions are formed.
/// </para>
/// <para>
/// A handful of bounded attempts, not a permanent loop: if no embedding server appears in the
/// first few minutes there is nothing useful left to retry, the document already carries an
/// explanation, and the next start will try again.
/// </para>
/// </remarks>
public sealed class RagSampleEmbeddingService : BackgroundService
{
    private static readonly TimeSpan[] Attempts =
    [
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromMinutes(2),
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RagSampleEmbeddingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagSampleEmbeddingService"/> class.
    /// </summary>
    /// <param name="scopeFactory">Creates the scope the work runs in.</param>
    /// <param name="logger">The logger instance.</param>
    public RagSampleEmbeddingService(
        IServiceScopeFactory scopeFactory, ILogger<RagSampleEmbeddingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (TimeSpan delay in Attempts)
        {
            // The first wait is what puts this after the health check service's opening pass, so
            // the embedding endpoint is chosen from servers already known to answer.
            await Task.Delay(delay, stoppingToken);

            try
            {
                if (await TryEmbedAsync(stoppingToken))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not embed the sample RAG collection; will retry");
            }
        }
    }

    /// <summary>
    /// Runs one attempt.
    /// </summary>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns><see langword="true"/> when there is nothing further to do.</returns>
    private async Task<bool> TryEmbedAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();

        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        RagSampleEmbedder embedder = scope.ServiceProvider.GetRequiredService<RagSampleEmbedder>();

        await embedder.EmbedIfNeededAsync(db, ct);

        return !await db.Set<RagChunk>()
            .AsNoTracking()
            .AnyAsync(
                c => c.Embedding == null
                     && db.Set<RagDocument>()
                         .Where(d => d.Filename == RagSampleEmbedder.SampleFilename)
                         .Select(d => d.Id)
                         .Contains(c.DocumentId),
                ct);
    }
}
