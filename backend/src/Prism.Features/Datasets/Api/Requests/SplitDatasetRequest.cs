namespace Prism.Features.Datasets.Api.Requests;

/// <summary>
/// HTTP request body for splitting a dataset.
/// </summary>
/// <param name="TrainRatio">Training split proportion (0-1).</param>
/// <param name="TestRatio">Testing split proportion (0-1).</param>
/// <param name="ValRatio">Validation split proportion (0-1).</param>
/// <param name="Seed">Optional random seed for reproducibility.</param>
public sealed record SplitDatasetRequest(double TrainRatio, double TestRatio, double ValRatio, int? Seed);
