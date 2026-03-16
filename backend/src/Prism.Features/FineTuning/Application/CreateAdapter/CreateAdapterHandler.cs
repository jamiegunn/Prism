using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.FineTuning.Application.Dtos;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Application.CreateAdapter;

/// <summary>
/// Command to register a new LoRA adapter.
/// </summary>
public sealed record CreateAdapterCommand(
    string Name,
    string? Description,
    Guid InstanceId,
    string AdapterPath,
    string BaseModel);

/// <summary>
/// Handles registration of LoRA adapters.
/// </summary>
public sealed class CreateAdapterHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateAdapterHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateAdapterHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateAdapterHandler(AppDbContext db, ILogger<CreateAdapterHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new LoRA adapter registration.
    /// </summary>
    /// <param name="command">The create adapter command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The created adapter DTO.</returns>
    public async Task<Result<LoraAdapterDto>> HandleAsync(CreateAdapterCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            return Error.Validation("Adapter name is required.");

        if (string.IsNullOrWhiteSpace(command.AdapterPath))
            return Error.Validation("Adapter path is required.");

        if (string.IsNullOrWhiteSpace(command.BaseModel))
            return Error.Validation("Base model is required.");

        var adapter = new LoraAdapter
        {
            Name = command.Name,
            Description = command.Description,
            InstanceId = command.InstanceId,
            AdapterPath = command.AdapterPath,
            BaseModel = command.BaseModel
        };

        _db.Set<LoraAdapter>().Add(adapter);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Registered LoRA adapter {AdapterName} for model {BaseModel}", adapter.Name, adapter.BaseModel);

        return LoraAdapterDto.FromEntity(adapter);
    }
}
