using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.UpdateDataset;

/// <summary>
/// Command to update dataset metadata.
/// </summary>
/// <param name="Id">The dataset identifier.</param>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description.</param>
/// <param name="Schema">The updated column schema.</param>
public sealed record UpdateDatasetCommand(Guid Id, string Name, string? Description, List<ColumnSchema>? Schema);

/// <summary>
/// Handles updating dataset metadata and schema.
/// </summary>
public sealed class UpdateDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public UpdateDatasetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Updates the dataset metadata.
    /// </summary>
    /// <param name="command">The update command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated dataset DTO.</returns>
    public async Task<Result<DatasetDto>> HandleAsync(UpdateDatasetCommand command, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .Include(d => d.Splits)
            .FirstOrDefaultAsync(d => d.Id == command.Id, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {command.Id} not found.");
        }

        dataset.Name = command.Name;
        dataset.Description = command.Description;
        if (command.Schema is not null)
        {
            dataset.Schema = command.Schema;
        }
        dataset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return DatasetDto.FromEntity(dataset);
    }
}
