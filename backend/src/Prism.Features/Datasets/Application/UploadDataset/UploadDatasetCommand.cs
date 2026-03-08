namespace Prism.Features.Datasets.Application.UploadDataset;

/// <summary>
/// Command to upload and parse a dataset file.
/// </summary>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentStream">The file content stream.</param>
/// <param name="ContentLength">The file size in bytes.</param>
/// <param name="Name">The display name for the dataset.</param>
/// <param name="Description">An optional description.</param>
/// <param name="ProjectId">An optional project to associate with.</param>
public sealed record UploadDatasetCommand(
    string FileName,
    Stream ContentStream,
    long ContentLength,
    string Name,
    string? Description,
    Guid? ProjectId);
