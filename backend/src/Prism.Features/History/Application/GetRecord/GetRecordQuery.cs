namespace Prism.Features.History.Application.GetRecord;

/// <summary>
/// Query to retrieve a single inference record by its unique identifier.
/// </summary>
/// <param name="Id">The unique identifier of the inference record to retrieve.</param>
public sealed record GetRecordQuery(Guid Id);
