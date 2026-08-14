using Prism.Common.Results;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Application;

/// <summary>
/// Decides which model name a request should carry.
/// </summary>
/// <remarks>
/// <para>
/// This kept being got wrong one screen at a time. Every feature that talks to a model builds its
/// own <c>ChatRequest</c>, and each one invented its own answer to "which model": some took the
/// caller's value and passed it through even when it was blank, some read the instance and
/// defaulted to <c>""</c>. Both spellings put an empty model on the wire, and an inference server
/// answers that with <c>{"error":"model is required"}</c> — surfaced as a 503, which reads as the
/// server being down rather than a field being empty. Replay, Playground streaming and the RAG
/// answer step each shipped that bug separately, and the remaining callers were one blank field
/// away from it.
/// </para>
/// <para>
/// Precedence stays with the caller, because it genuinely differs: the Playground honours an
/// explicit override first, while the Token Explorer wants the instance's own model whatever was
/// asked for. What does not differ, and is therefore settled here, is that the answer is never
/// blank and that failing to find one is explained in terms of the instance rather than left for
/// the inference server to complain about.
/// </para>
/// </remarks>
public static class ModelSelection
{
    /// <summary>
    /// Resolves the model to send, in the caller's order of preference.
    /// </summary>
    /// <param name="instance">The instance the request will be sent to.</param>
    /// <param name="preferred">
    /// Candidate names in priority order. Blank and null entries are skipped, so a caller can
    /// pass optional overrides directly without checking them first.
    /// </param>
    /// <returns>
    /// The first usable name, falling back to the instance's own model; a validation error when
    /// there is nothing to run.
    /// </returns>
    /// <example>
    /// An explicit override wins, then whatever the conversation recorded, then the instance:
    /// <code>
    /// Result&lt;string&gt; model = ModelSelection.Resolve(instance, command.Model, conversation.ModelId);
    /// </code>
    /// </example>
    public static Result<string> Resolve(InferenceInstance instance, params string?[] preferred)
    {
        foreach (string? candidate in preferred)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(instance.ModelId))
        {
            return instance.ModelId;
        }

        return Error.Validation(
            $"No model to run: none was given and instance '{instance.Name}' has none recorded. " +
            "Name a model, or check the instance's health so its model is known.");
    }

    /// <summary>
    /// Resolves a model where no instance is in scope, such as a stored job that carries its own.
    /// </summary>
    /// <param name="preferred">Candidate names in priority order.</param>
    /// <param name="describeSource">
    /// What should have supplied the name, named the way the user would recognise it — "the batch",
    /// "the agent workflow". It appears in the error, so it is the difference between a message
    /// someone can act on and one that only says something was empty.
    /// </param>
    /// <returns>The first usable name, or a validation error.</returns>
    public static Result<string> Resolve(string? describeSource, params string?[] preferred)
    {
        foreach (string? candidate in preferred)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                return candidate;
            }
        }

        return Error.Validation(
            $"No model to run: {describeSource ?? "the request"} does not name one.");
    }
}
