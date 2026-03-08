using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.DeleteDataset;

/// <summary>
/// Command to delete a dataset and all its records.
/// </summary>
/// <param name="Id">The dataset identifier.</param>
public sealed record DeleteDatasetCommand(Guid Id);

/// <summary>
/// Handles deleting a dataset.
/// </summary>
public sealed class DeleteDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public DeleteDatasetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Deletes the dataset and cascades to all records and splits.
    /// </summary>
    /// <param name="command">The delete command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteDatasetCommand command, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .FirstOrDefaultAsync(d => d.Id == command.Id, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {command.Id} not found.");
        }

        _db.Set<Dataset>().Remove(dataset);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
