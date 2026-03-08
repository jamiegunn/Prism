namespace Prism.Features.Datasets.Api.Requests;

/// <summary>
/// HTTP request body for updating a dataset record.
/// </summary>
/// <param name="Data">The updated record data.</param>
public sealed record UpdateRecordRequest(Dictionary<string, object?> Data);
