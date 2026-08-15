using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.UnregisterInstance;

/// <summary>
/// Handles removal of a registered inference provider instance.
/// </summary>
public sealed class UnregisterInstanceHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UnregisterInstanceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnregisterInstanceHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UnregisterInstanceHandler(AppDbContext db, ILogger<UnregisterInstanceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Removes the specified inference instance from the database.
    /// </summary>
    /// <param name="command">The command containing the instance ID to remove.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure (not found).</returns>
    public async Task<Result> HandleAsync(UnregisterInstanceCommand command, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == command.Id, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{command.Id}' was not found.");
        }

        bool wasDefault = instance.IsDefault;

        _db.Set<InferenceInstance>().Remove(instance);
        await _db.SaveChangesAsync(ct);

        // Deleting the default used to leave no default at all, and several features choose their
        // server by "the default first, then …" — with none, they fall through to a tiebreak that
        // was never meant to decide anything. Promoting a reachable one keeps that choice
        // meaningful; if nothing is left there is nothing to promote, which is honest.
        if (wasDefault)
        {
            InferenceInstance? successor = await _db.Set<InferenceInstance>()
                .OrderByDescending(i => i.Status == InstanceStatus.Online)
                .ThenBy(i => i.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (successor is not null)
            {
                successor.IsDefault = true;
                successor.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "{SuccessorName} is now the default, replacing the deleted {InstanceName}",
                    successor.Name, instance.Name);
            }
        }

        _logger.LogInformation("Unregistered inference instance {InstanceName} ({InstanceId})",
            instance.Name, instance.Id);

        return Result.Success();
    }
}
