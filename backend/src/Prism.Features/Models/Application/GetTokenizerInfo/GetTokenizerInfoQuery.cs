namespace Prism.Features.Models.Application.GetTokenizerInfo;

/// <summary>
/// Query to retrieve tokenizer information for a specific inference instance.
/// </summary>
/// <param name="InstanceId">The ID of the inference instance to query.</param>
public sealed record GetTokenizerInfoQuery(Guid InstanceId);
