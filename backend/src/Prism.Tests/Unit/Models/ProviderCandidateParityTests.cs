using System.Text.RegularExpressions;
using Prism.Common.Inference;
using Prism.Features.Models.Application.DiscoverProviders;

namespace Prism.Tests.Unit.Models;

/// <summary>
/// Holds the launcher's list of local servers in step with the app's.
/// </summary>
/// <remarks>
/// <para>
/// Two lists of "where a local model might be listening" exist: <c>PRISM_PROVIDER_CANDIDATES</c>
/// in <c>dev.sh</c>, and <see cref="DiscoverProvidersHandler.Candidates"/> here. They drifted —
/// the backend knew about LM Studio and the launcher did not, so setup could not offer a
/// provider the platform fully supported.
/// </para>
/// <para>
/// Nothing can share a literal across bash and C#, so this reads the shell file and compares.
/// A drift fails here rather than becoming a provider someone cannot select.
/// </para>
/// </remarks>
public sealed class ProviderCandidateParityTests
{
    /// <summary>
    /// Every endpoint the app probes must be one the launcher can offer, and vice versa.
    /// </summary>
    [Fact]
    public void The_Launcher_Looks_Where_The_App_Looks()
    {
        Dictionary<string, string> fromShell = ParseShellCandidates();

        Dictionary<string, string> fromApp = DiscoverProvidersHandler.Candidates
            .ToDictionary(c => c.Endpoint, c => c.Type.ToString(), StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            fromApp.Keys.OrderBy(k => k, StringComparer.Ordinal),
            fromShell.Keys.OrderBy(k => k, StringComparer.Ordinal));

        // The provider type has to agree too: probing LM Studio's port but registering it as a
        // generic OpenAI-compatible server loses the distinction the enum exists to make.
        foreach ((string endpoint, string appType) in fromApp)
        {
            Assert.True(
                fromShell[endpoint].Equals(appType, StringComparison.OrdinalIgnoreCase),
                $"dev.sh calls {endpoint} a '{fromShell[endpoint]}'; the app calls it '{appType}'.");
        }
    }

    /// <summary>
    /// Each declared type must be a real member of the enum the app registers against.
    /// </summary>
    [Fact]
    public void The_Launcher_Only_Names_Provider_Types_That_Exist()
    {
        foreach ((string endpoint, string type) in ParseShellCandidates())
        {
            Assert.True(
                Enum.TryParse(type, ignoreCase: true, out InferenceProviderType _),
                $"dev.sh declares {endpoint} as '{type}', which is not an InferenceProviderType.");
        }
    }

    /// <summary>
    /// Reads <c>PRISM_PROVIDER_CANDIDATES</c> out of dev.sh.
    /// </summary>
    /// <returns>Endpoint to provider-type name.</returns>
    private static Dictionary<string, string> ParseShellCandidates()
    {
        string devScript = Path.Combine(FindRepositoryRoot(), "dev.sh");

        Assert.True(File.Exists(devScript), $"Could not find dev.sh at {devScript}");

        string contents = File.ReadAllText(devScript);

        Match block = Regex.Match(
            contents,
            @"PRISM_PROVIDER_CANDIDATES=\((?<body>[^)]*)\)",
            RegexOptions.Singleline);

        Assert.True(block.Success, "dev.sh no longer declares PRISM_PROVIDER_CANDIDATES.");

        Dictionary<string, string> candidates = new(StringComparer.OrdinalIgnoreCase);

        foreach (Match entry in Regex.Matches(block.Groups["body"].Value, @"""(?<value>[^""]+)"""))
        {
            // port|id|type|endpoint|label
            string[] fields = entry.Groups["value"].Value.Split('|');

            Assert.True(
                fields.Length == 5,
                $"Expected port|id|type|endpoint|label, got '{entry.Groups["value"].Value}'.");

            candidates[fields[3]] = fields[2];
        }

        Assert.NotEmpty(candidates);

        return candidates;
    }

    /// <summary>
    /// Walks up from the test assembly until the repository root is recognisable.
    /// </summary>
    /// <returns>The absolute repository root path.</returns>
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "dev.sh")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"No repository root containing dev.sh above {AppContext.BaseDirectory}");
    }
}
