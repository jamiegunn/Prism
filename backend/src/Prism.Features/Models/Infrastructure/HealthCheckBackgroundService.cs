using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Features.Models.Application.CheckHealth;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Infrastructure;

/// <summary>
/// Background service that periodically checks the health of all registered inference instances.
/// Runs every 30 seconds and updates instance status in the database.
/// </summary>
public sealed class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthCheckBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthCheckBackgroundService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="logger">The logger instance.</param>
    public HealthCheckBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthCheckBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes the background health check loop, checking all instances every 30 seconds.
    /// </summary>
    /// <param name="stoppingToken">A token that signals when the service should stop.</param>
    /// <returns>A task representing the background operation.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Health check background service started");

        // Wait a bit before the first check to let the application finish starting
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllInstancesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check cycle");
            }

            await Task.Delay(Interval, stoppingToken);
        }

        _logger.LogInformation("Health check background service stopped");
    }

    private async Task CheckAllInstancesAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        CheckHealthHandler handler = scope.ServiceProvider.GetRequiredService<CheckHealthHandler>();

        List<Guid> instanceIds = await db.Set<InferenceInstance>()
            .AsNoTracking()
            .Select(i => i.Id)
            .ToListAsync(ct);

        if (instanceIds.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Running health checks for {InstanceCount} instances", instanceIds.Count);

        foreach (Guid instanceId in instanceIds)
        {
            try
            {
                await handler.HandleAsync(new CheckHealthCommand(instanceId), ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for instance {InstanceId}", instanceId);
            }
        }
    }
}
