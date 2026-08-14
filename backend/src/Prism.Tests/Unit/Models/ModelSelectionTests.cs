using System.Text.RegularExpressions;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Common.Inference;
using Prism.Features.Models.Domain;

namespace Prism.Tests.Unit.Models;

/// <summary>
/// Covers how the model name for a request is chosen, and that every caller chooses it this way.
/// </summary>
/// <remarks>
/// The same defect appeared in Replay, in Playground streaming and in the RAG answer step, and
/// was fixed three separate times: a blank model reached the inference server, which replied
/// <c>{"error":"model is required"}</c>, which Prism surfaced as a 503. Five more callers were
/// spelling it <c>instance.ModelId ?? ""</c> and were one unhealthy instance away from the same
/// thing. The last test here is the one that keeps it fixed — it reads the source and fails when
/// a new caller reintroduces the pattern.
/// </remarks>
public sealed partial class ModelSelectionTests
{
    /// <summary>
    /// The first non-blank preference wins.
    /// </summary>
    [Fact]
    public void The_First_Named_Model_Is_Chosen()
    {
        Result<string> model = ModelSelection.Resolve(Instance("instance-model"), "asked-for", "second");

        Assert.True(model.IsSuccess);
        Assert.Equal("asked-for", model.Value);
    }

    /// <summary>
    /// Blank preferences are skipped rather than treated as a choice.
    /// </summary>
    /// <remarks>
    /// An empty string arriving from a JSON body is the normal shape of "not specified", and
    /// treating it as an answer is exactly what put an empty model on the wire.
    /// </remarks>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_Blank_Preference_Falls_Through_To_The_Instance(string? blank)
    {
        Result<string> model = ModelSelection.Resolve(Instance("instance-model"), blank);

        Assert.True(model.IsSuccess);
        Assert.Equal("instance-model", model.Value);
    }

    /// <summary>
    /// With nothing anywhere, the failure names the instance instead of leaving it to the server.
    /// </summary>
    [Fact]
    public void With_No_Model_Anywhere_The_Error_Names_The_Instance()
    {
        Result<string> model = ModelSelection.Resolve(Instance(null), null, "");

        Assert.True(model.IsFailure);
        Assert.Equal(ErrorType.Validation, model.Error.Type);
        Assert.Contains("lonely-instance", model.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The instance-free overload says what should have carried the name.
    /// </summary>
    [Fact]
    public void Without_An_Instance_The_Error_Names_The_Source()
    {
        Result<string> model = ModelSelection.Resolve("the batch", [null]);

        Assert.True(model.IsFailure);
        Assert.Contains("the batch", model.Error.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// No feature builds a request whose model can be blank.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A source check rather than a behavioural one, because the failure it prevents is a new
    /// call site written the old way — something no test of existing behaviour can see. The
    /// pattern it bans, <c>Model = something ?? ""</c>, is the one that shipped an empty model
    /// under the appearance of a safe default.
    /// </para>
    /// <para>
    /// If this fails on code you just wrote, resolve the model with
    /// <see cref="ModelSelection.Resolve(InferenceInstance, string?[])"/> and return its error,
    /// rather than defaulting to an empty string.
    /// </para>
    /// </remarks>
    [Fact]
    public void No_Feature_Defaults_A_Request_Model_To_Empty()
    {
        List<string> offenders = [];

        foreach (string file in Directory.EnumerateFiles(FeaturesRoot(), "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                if (BlankModelDefault().IsMatch(lines[i]))
                {
                    offenders.Add($"{Path.GetFileName(file)}:{i + 1} {lines[i].Trim()}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These assign a request model that can be empty. Use ModelSelection.Resolve and " +
            "return its error instead:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Matches <c>Model = anything ?? ""</c>, the shape that ships a blank model.</summary>
    [GeneratedRegex(@"Model\s*=\s*[^;]*\?\?\s*""""")]
    private static partial Regex BlankModelDefault();

    private static InferenceInstance Instance(string? modelId)
        => new()
        {
            Name = modelId is null ? "lonely-instance" : "test-instance",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.Ollama,
            ModelId = modelId,
        };

    /// <summary>
    /// Locates the feature sources relative to this assembly.
    /// </summary>
    /// <returns>The absolute path to the features project.</returns>
    private static string FeaturesRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Prism.Features")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Prism.Features");
    }
}
