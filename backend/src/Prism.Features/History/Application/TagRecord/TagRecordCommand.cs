namespace Prism.Features.History.Application.TagRecord;

/// <summary>
/// Command to replace the tags on an existing inference record.
/// </summary>
/// <param name="Id">The unique identifier of the inference record to tag.</param>
/// <param name="Tags">The new list of tags to set on the record (replaces existing tags).</param>
public sealed record TagRecordCommand(Guid Id, List<string> Tags);
