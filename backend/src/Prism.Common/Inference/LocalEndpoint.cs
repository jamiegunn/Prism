namespace Prism.Common.Inference;

/// <summary>
/// Translates "local" endpoints into addresses that work from wherever this process is running.
/// </summary>
/// <remarks>
/// <para>
/// Every convention about local inference servers is written from the host's point of view:
/// Ollama is on <c>localhost:11434</c>, vLLM on <c>localhost:8000</c>. That is true of the
/// machine and false inside a container, where <c>localhost</c> is the container itself.
/// </para>
/// <para>
/// Since the API began running containerised by default, that difference was the whole
/// first-run experience: the seeded instances pointed at <c>localhost</c> and were permanently
/// offline, and discovery probed the same three addresses and always found nothing — on a
/// machine with Ollama running and reachable. Both reported the situation accurately and both
/// were asking the wrong address.
/// </para>
/// </remarks>
public static class LocalEndpoint
{
    /// <summary>
    /// The name a container uses to reach a service on its host. Docker Desktop provides it;
    /// on Linux <c>docker-compose.yml</c> maps it through <c>extra_hosts</c>.
    /// </summary>
    public const string ContainerHostAlias = "host.docker.internal";

    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1", "[::1]"];

    /// <summary>
    /// Gets a value indicating whether this process is running inside a container.
    /// </summary>
    /// <remarks>
    /// The .NET base images set <c>DOTNET_RUNNING_IN_CONTAINER</c>; <c>/.dockerenv</c> is the
    /// fallback for an image that does not, and for a process started outside those images.
    /// </remarks>
    public static bool RunningInContainer { get; } =
        string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase)
        || File.Exists("/.dockerenv");

    /// <summary>
    /// Rewrites a loopback endpoint to one this process can actually reach.
    /// </summary>
    /// <param name="endpoint">An endpoint written from the host's point of view.</param>
    /// <returns>
    /// The endpoint unchanged when it is not loopback or when this process is on the host;
    /// otherwise the same endpoint addressed through <see cref="ContainerHostAlias"/>.
    /// </returns>
    public static string AsReachable(string endpoint) =>
        RunningInContainer ? ThroughContainerHost(endpoint) : endpoint;

    /// <summary>
    /// Rewrites a loopback endpoint to address the container's host, regardless of where this
    /// process is running. Use <see cref="AsReachable"/> unless you specifically mean "the host".
    /// </summary>
    /// <param name="endpoint">An endpoint written from the host's point of view.</param>
    /// <returns>The endpoint addressed through the container host alias, or unchanged when it is not loopback.</returns>
    public static string ThroughContainerHost(string endpoint)
    {
        if (!IsLoopback(endpoint))
        {
            return endpoint;
        }

        // Rebuilt through UriBuilder rather than by string replacement so a port, a path and a
        // scheme all survive: "http://127.0.0.1:1234/v1" has to stay pointed at /v1.
        var builder = new UriBuilder(endpoint) { Host = ContainerHostAlias };

        string rebuilt = builder.Uri.ToString();

        // UriBuilder appends a trailing slash to an empty path. Endpoints are compared as
        // strings in several places — including "is this already registered" — so giving one
        // back in a different shape than it arrived would register a duplicate.
        return endpoint.EndsWith('/') ? rebuilt : rebuilt.TrimEnd('/');
    }

    /// <summary>
    /// Determines whether an endpoint addresses the machine it is evaluated on.
    /// </summary>
    /// <param name="endpoint">The endpoint to inspect.</param>
    /// <returns>True when the host part is a loopback address.</returns>
    public static bool IsLoopback(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        return LoopbackHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
    }
}
