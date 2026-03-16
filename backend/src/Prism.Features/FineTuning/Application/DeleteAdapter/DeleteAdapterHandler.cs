using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Application.DeleteAdapter;

/// <summary>
/// Command to delete a LoRA adapter registration.
/// </summary>
public sealed record DeleteAdapterCommand(Guid Id);

/// <summary>
/// Handles deletion of LoRA adapter registrations.
/// </summary>
public sealed class DeleteAdapterHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteAdapterHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAdapterHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteAdapterHandler(AppDbContext db, ILogger<DeleteAdapterHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a LoRA adapter registration.
    /// </summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteAdapterCommand command, CancellationToken ct)
    {
        LoraAdapter? adapter = await _db.Set<LoraAdapter>()
            .FirstOrDefaultAsync(a => a.Id == command.Id, ct);

        if (adapter is null)
            return Error.NotFound($"LoRA adapter {command.Id} not found.");

        _db.Set<LoraAdapter>().Remove(adapter);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted LoRA adapter {AdapterId}", command.Id);

        return Result.Success();
    }
}
