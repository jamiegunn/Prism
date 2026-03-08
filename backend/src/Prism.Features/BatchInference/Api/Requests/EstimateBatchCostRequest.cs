namespace Prism.Features.BatchInference.Api.Requests;

/// <summary>
/// HTTP request body for estimating batch job cost.
/// </summary>
public sealed record EstimateBatchCostRequest(
    Guid DatasetId,
    string? SplitLabel,
    string Model,
    int Concurrency);
