namespace Prism.Common.Inference.Metrics;

/// <summary>
/// Estimates the cost of inference requests based on token usage and model pricing.
/// Contains a dictionary of known model pricing tiers for popular commercial models.
/// For local models (vLLM, Ollama, LM Studio), the cost is always zero.
/// </summary>
public static class CostCalculator
{
    /// <summary>
    /// Pricing entry with cost per input token and cost per output token.
    /// </summary>
    private sealed record ModelPricing(decimal InputCostPerToken, decimal OutputCostPerToken);

    private static readonly Dictionary<string, ModelPricing> KnownPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4"] = new(0.00003m, 0.00006m),
        ["gpt-4-turbo"] = new(0.00001m, 0.00003m),
        ["gpt-4o"] = new(0.000005m, 0.000015m),
        ["gpt-4o-mini"] = new(0.00000015m, 0.0000006m),
        ["gpt-3.5-turbo"] = new(0.0000005m, 0.0000015m),
        ["claude-3-opus"] = new(0.000015m, 0.000075m),
        ["claude-3-sonnet"] = new(0.000003m, 0.000015m),
        ["claude-3-haiku"] = new(0.00000025m, 0.00000125m),
    };

    /// <summary>
    /// Estimates the total cost of an inference request based on token counts and model name.
    /// Returns zero for local/self-hosted models not in the known pricing dictionary.
    /// </summary>
    /// <param name="modelName">The name of the model used for inference.</param>
    /// <param name="promptTokens">The number of tokens in the prompt.</param>
    /// <param name="completionTokens">The number of tokens in the completion.</param>
    /// <returns>The estimated cost in USD. Returns 0 for unknown or local models.</returns>
    public static decimal EstimateCost(string modelName, int promptTokens, int completionTokens)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            return 0m;
        }

        ModelPricing? pricing = FindPricing(modelName);
        if (pricing is null)
        {
            return 0m;
        }

        decimal inputCost = promptTokens * pricing.InputCostPerToken;
        decimal outputCost = completionTokens * pricing.OutputCostPerToken;

        return inputCost + outputCost;
    }

    /// <summary>
    /// Estimates the cost per token for a given model.
    /// Returns the average of input and output cost per token.
    /// </summary>
    /// <param name="modelName">The name of the model.</param>
    /// <returns>The average cost per token in USD, or 0 for unknown models.</returns>
    public static decimal GetAverageCostPerToken(string modelName)
    {
        ModelPricing? pricing = FindPricing(modelName);
        if (pricing is null)
        {
            return 0m;
        }

        return (pricing.InputCostPerToken + pricing.OutputCostPerToken) / 2m;
    }

    /// <summary>
    /// Checks whether a model has known pricing information.
    /// </summary>
    /// <param name="modelName">The name of the model.</param>
    /// <returns>True if the model has known pricing; otherwise, false.</returns>
    public static bool HasPricing(string modelName)
    {
        return FindPricing(modelName) is not null;
    }

    private static ModelPricing? FindPricing(string modelName)
    {
        if (KnownPricing.TryGetValue(modelName, out ModelPricing? pricing))
        {
            return pricing;
        }

        // Try partial matching for versioned model names (e.g., "gpt-4-0613" matches "gpt-4")
        foreach (KeyValuePair<string, ModelPricing> entry in KnownPricing)
        {
            if (modelName.StartsWith(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }
}
