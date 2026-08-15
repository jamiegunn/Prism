using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.Models.Application.Dtos;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application.SetDefaultInstance;

/// <summary>
/// Command to make an instance the default one.
/// </summary>
/// <param name="InstanceId">The instance to promote.</param>
public sealed record SetDefaultInstanceCommand(Guid InstanceId);

/// <summary>
/// Makes one instance the default, and the others not.
/// </summary>
/// <remarks>
/// The default could only be set at registration: there was no endpoint to change it and no
/// control that offered to. Someone with two servers registered could not choose between them
/// without deleting one and adding it again, and deleting the default used to leave none at all.
/// That choice is not decorative — embedding resolution, batch inference and the evaluation
/// runner all pick their server by "the default first, then …".
/// </remarks>
public sealed class SetDefaultInstanceHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<SetDefaultInstanceHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetDefaultInstanceHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public SetDefaultInstanceHandler(AppDbContext db, ILogger<SetDefaultInstanceHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Promotes the named instance, demoting whichever held the flag before.
    /// </summary>
    /// <param name="command">The command naming the instance.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The promoted instance.</returns>
    public async Task<Result<InferenceInstanceDto>> HandleAsync(
        SetDefaultInstanceCommand command, CancellationToken ct)
    {
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance with ID '{command.InstanceId}' was not found.");
        }

        // Demote everything else first. Two defaults is a state nothing in the application knows
        // how to read: every consumer orders by the flag and takes the first, so which one wins
        // would come down to whatever the database felt like returning.
        await _db.Set<InferenceInstance>()
            .Where(i => i.IsDefault && i.Id != instance.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDefault, false), ct);

        instance.IsDefault = true;
        instance.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "{InstanceName} ({InstanceId}) is now the default instance", instance.Name, instance.Id);

        return InferenceInstanceDto.FromEntity(instance);
    }
}
