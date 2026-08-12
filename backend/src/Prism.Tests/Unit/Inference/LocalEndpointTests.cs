using Prism.Common.Inference;
using Prism.Features.Models.Application.DiscoverProviders;

namespace Prism.Tests.Unit.Inference;

/// <summary>
/// Proofs for translating host-written endpoints into ones the running process can reach.
/// </summary>
/// <remarks>
/// Written after a containerised API reported an empty machine on a machine with Ollama up:
/// discovery probed <c>localhost:11434</c> from inside the container, and the seeded instances
/// pointed there too. Both were accurate about the address they asked and wrong about which
/// address to ask.
/// </remarks>
public sealed class LocalEndpointTests
{
    /// <summary>
    /// A loopback endpoint keeps its scheme, port and path when re-addressed — LM Studio's
    /// <c>/v1</c> is part of where it is, not decoration.
    /// </summary>
    /// <param name="endpoint">The endpoint written from the host's point of view.</param>
    /// <param name="expected">The endpoint as a container must address it.</param>
    [Theory]
    [InlineData("http://localhost:11434", "http://host.docker.internal:11434")]
    [InlineData("http://127.0.0.1:11434", "http://host.docker.internal:11434")]
    [InlineData("http://localhost:1234/v1", "http://host.docker.internal:1234/v1")]
    [InlineData("http://localhost:8000/v1", "http://host.docker.internal:8000/v1")]
    [InlineData("https://localhost:8443/api", "https://host.docker.internal:8443/api")]
    public void A_Loopback_Endpoint_Is_Re_Addressed_Whole(string endpoint, string expected)
        => Assert.Equal(expected, LocalEndpoint.ThroughContainerHost(endpoint));

    /// <summary>
    /// Anything that is not loopback is left exactly as it was: a sibling container, a machine
    /// on the network and an already-translated endpoint must all survive untouched, or a second
    /// pass would mangle what the first produced.
    /// </summary>
    /// <param name="endpoint">An endpoint that does not name this machine.</param>
    [Theory]
    [InlineData("http://ollama:11434")]
    [InlineData("http://host.docker.internal:11434")]
    [InlineData("http://192.168.1.50:8000")]
    [InlineData("https://inference.example.com/v1")]
    public void A_Remote_Endpoint_Is_Left_Alone(string endpoint)
        => Assert.Equal(endpoint, LocalEndpoint.ThroughContainerHost(endpoint));

    /// <summary>
    /// A trailing slash is preserved rather than introduced or dropped. Endpoints are compared
    /// as strings to decide whether one is already registered, so a changed shape is a duplicate.
    /// </summary>
    [Fact]
    public void The_Shape_Of_The_Endpoint_Survives()
    {
        Assert.Equal("http://host.docker.internal:11434/", LocalEndpoint.ThroughContainerHost("http://localhost:11434/"));
        Assert.Equal("http://host.docker.internal:11434", LocalEndpoint.ThroughContainerHost("http://localhost:11434"));
    }

    /// <summary>
    /// Something that is not a URL at all is returned unchanged rather than throwing: this runs
    /// during seeding, and a malformed stored endpoint must not stop the API from starting.
    /// </summary>
    [Fact]
    public void A_Malformed_Endpoint_Is_Returned_Unchanged()
        => Assert.Equal("not an endpoint", LocalEndpoint.ThroughContainerHost("not an endpoint"));

    /// <summary>
    /// On the host, nothing is translated — the conventional endpoints are already correct there.
    /// </summary>
    [Fact]
    public void On_The_Host_Nothing_Is_Translated()
    {
        // The suite runs on the host; if that ever stops being true this asserts the other branch.
        string translated = LocalEndpoint.AsReachable("http://localhost:11434");

        Assert.Equal(
            LocalEndpoint.RunningInContainer ? "http://host.docker.internal:11434" : "http://localhost:11434",
            translated);
    }

    /// <summary>
    /// Discovery probes an address per candidate, and inside a container it also probes the
    /// inference containers this project starts — a sibling publishes to neither the API
    /// container's loopback nor, necessarily, the host's.
    /// </summary>
    [Fact]
    public void Discovery_Probes_Every_Candidate_And_The_Containers_We_Start()
    {
        string[] probed = [.. DiscoverProvidersHandler.AddressesToProbe().Select(a => a.Endpoint)];

        Assert.Equal(DiscoverProvidersHandler.Candidates.Length, probed.Distinct().Count(a => !a.Contains("ollama:") && !a.Contains("vllm:")));

        if (LocalEndpoint.RunningInContainer)
        {
            Assert.Contains("http://ollama:11434", probed);
            Assert.Contains("http://vllm:8000", probed);
            Assert.DoesNotContain(probed, a => a.Contains("localhost", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.Contains("http://localhost:11434", probed);
        }
    }
}
