namespace Prism.Common.Inference;

/// <summary>
/// A provider that wraps another and adds behaviour to it.
/// </summary>
/// <remarks>
/// Exists so a decorator does not hide what it decorates. Recording is applied to every provider
/// the factory builds, and the wrapper implements <see cref="IInferenceProvider"/> only — so
/// <c>provider is IHotReloadableProvider</c> became false for every provider in the application,
/// and "swap the loaded model" answered "Ollama does not support hot-swapping models" for an
/// Ollama that does. The capability was not lost, only concealed.
/// </remarks>
public interface IProviderDecorator
{
    /// <summary>
    /// Gets the provider this one wraps.
    /// </summary>
    IInferenceProvider Inner { get; }
}

/// <summary>
/// Helpers for asking a provider what it can do without tripping over how it is wrapped.
/// </summary>
public static class InferenceProviderExtensions
{
    /// <summary>
    /// Finds the optional capability interface <typeparamref name="T"/> on a provider, looking
    /// through any decorators around it.
    /// </summary>
    /// <typeparam name="T">The capability interface being asked for.</typeparam>
    /// <param name="provider">The provider, possibly wrapped.</param>
    /// <returns>The provider as <typeparamref name="T"/>, or null when nothing in the chain is one.</returns>
    /// <remarks>
    /// Prefer this to a direct <c>is</c> test. A direct test asks "is the outermost object a
    /// <typeparamref name="T"/>", which is a question about wrapping rather than about the
    /// server, and the two answers stopped agreeing the moment recording was introduced.
    /// </remarks>
    public static T? As<T>(this IInferenceProvider provider)
        where T : class
    {
        IInferenceProvider current = provider;

        while (true)
        {
            if (current is T match)
            {
                return match;
            }

            if (current is IProviderDecorator decorator)
            {
                current = decorator.Inner;
                continue;
            }

            return null;
        }
    }
}
