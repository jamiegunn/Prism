using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.Datasets.Application.Dtos;
using Prism.Features.Datasets.Domain;

namespace Prism.Features.Datasets.Application.GetDataset;

/// <summary>
/// Query to get a specific dataset by ID.
/// </summary>
/// <param name="Id">The dataset identifier.</param>
public sealed record GetDatasetQuery(Guid Id);

/// <summary>
/// Handles retrieving a single dataset with its splits.
/// </summary>
public sealed class GetDatasetHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetDatasetHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetDatasetHandler(AppDbContext db) => _db = db;

    /// <summary>
    /// Gets a dataset by its identifier.
    /// </summary>
    /// <param name="query">The query containing the dataset ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the dataset DTO.</returns>
    public async Task<Result<DatasetDto>> HandleAsync(GetDatasetQuery query, CancellationToken ct)
    {
        Dataset? dataset = await _db.Set<Dataset>()
            .AsNoTracking()
            .Include(d => d.Splits)
            .FirstOrDefaultAsync(d => d.Id == query.Id, ct);

        if (dataset is null)
        {
            return Error.NotFound($"Dataset {query.Id} not found.");
        }

        return DatasetDto.FromEntity(dataset);
    }
}
