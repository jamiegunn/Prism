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

        _db.Set<InferenceInstance>().Remove(instance);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Unregistered inference instance {InstanceName} ({InstanceId})",
            instance.Name, instance.Id);

        return Result.Success();
    }
}
