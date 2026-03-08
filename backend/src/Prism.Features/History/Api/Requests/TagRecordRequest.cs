namespace Prism.Features.History.Api.Requests;

/// <summary>
/// Request body for updating the tags on an inference record.
/// </summary>
/// <param name="Tags">The list of tags to set on the record (replaces existing tags).</param>
public sealed record TagRecordRequest(List<string> Tags);
