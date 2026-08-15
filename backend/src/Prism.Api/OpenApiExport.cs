using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;

namespace Prism.Api;

/// <summary>
/// Writes the API's OpenAPI document to a file so CI can compare it against the copy
/// committed at <c>frontend/openapi.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exists because the frontend client is hand-written. Nothing else in the build
/// notices when an endpoint's request or response shape stops matching the TypeScript that
/// calls it, and that mismatch is the single largest historical source of defects in this
/// project. The committed document records what the API promised last time anyone looked;
/// the CI job re-exports and diffs against it.
/// </para>
/// <para>
/// The document is sorted before it is written. Endpoint discovery order is not contractual
/// — adding a route to one feature can reorder unrelated entries — and an unsorted document
/// would produce diffs that are noise. A drift check that cries wolf gets deleted, which is
/// how the previous version of this job ended up removed.
/// </para>
/// </remarks>
internal static class OpenApiExport
{
    /// <summary>The command-line flag that triggers an export instead of a normal run.</summary>
    internal const string Flag = "--export-openapi";

    /// <summary>The Swagger document name registered by <c>AddSwaggerGen</c>.</summary>
    private const string DocumentName = "v1";

    /// <summary>
    /// Reads the export destination from the command line.
    /// </summary>
    /// <param name="args">The process command-line arguments.</param>
    /// <returns>
    /// The path to write to, or <see langword="null"/> when the flag is absent and the
    /// process should start normally.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="args"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The flag was supplied without a following path. Failing here rather than defaulting
    /// keeps a mistyped CI invocation from silently starting a web server that never exits —
    /// the behaviour that hung this repository's CI until GitHub's six-hour ceiling.
    /// </exception>
    internal static string? TryGetExportPath(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        int index = Array.IndexOf(args, Flag);
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new ArgumentException($"{Flag} requires a file path, for example: {Flag} frontend/openapi.json", nameof(args));
        }

        return args[index + 1];
    }

    /// <summary>
    /// Strips everything from the service collection that would make an export need a
    /// running environment: the network server, and every hosted background service.
    /// </summary>
    /// <param name="services">The service collection to modify, before the host is built.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Export starts the host for real, so without this the background workers start too and
    /// immediately dial PostgreSQL and the inference providers. On a CI runner that means a
    /// connection error in the log at best and a multi-second timeout at worst, for work whose
    /// output is discarded. Removing them keeps the export dependent on nothing but the code.
    /// </remarks>
    internal static void PrepareForExport(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IHostedService))
            {
                services.RemoveAt(i);
            }
        }

        services.AddSingleton<IServer, NullServer>();
    }

    /// <summary>
    /// Starts the application without a network listener, generates the OpenAPI document,
    /// and writes it to <paramref name="path"/>.
    /// </summary>
    /// <param name="app">The built application, with all endpoints already mapped.</param>
    /// <param name="path">The destination file path. Parent directories are created.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The number of paths written, for the caller to log.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="app"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The generated document contains no paths, which means endpoint discovery did not run
    /// and the export would overwrite the committed baseline with an empty document.
    /// </exception>
    /// <remarks>
    /// The host has to start for ApiExplorer to see anything: mapped endpoints reach the
    /// <c>EndpointDataSource</c> ApiExplorer reads only when the request pipeline is wired up
    /// during startup. Building the inner <c>IApplicationBuilder</c> by hand is not enough —
    /// it yields a document with zero paths. Startup is therefore real, but the server is
    /// <see cref="NullServer"/>, so nothing binds a port and nothing can outlive this method.
    /// </remarks>
    internal static async Task<int> WriteAsync(WebApplication app, string path, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        await app.StartAsync(ct).ConfigureAwait(false);

        try
        {
            ISwaggerProvider provider = app.Services.GetRequiredService<ISwaggerProvider>();
            OpenApiDocument document = provider.GetSwagger(DocumentName);

            // An empty document is always a bug in this method, never a legitimate state: the
            // API has 16 endpoint groups. Writing it out would make the drift job pass against
            // nothing, which is exactly the failure the job exists to prevent.
            if (document.Paths.Count == 0)
            {
                throw new InvalidOperationException(
                    "The OpenAPI document contains no paths. Endpoint discovery did not run before the export.");
            }

            Sort(document);

            string json = document.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);

            string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // An explicit "\n" and a trailing newline keep the file byte-identical across
            // operating systems and stop `diff` reporting "\ No newline at end of file" on
            // every comparison. Environment.NewLine would make the check fail on Windows.
            await File.WriteAllTextAsync(path, json.ReplaceLineEndings("\n") + "\n", new UTF8Encoding(false), ct)
                .ConfigureAwait(false);

            return document.Paths.Count;
        }
        finally
        {
            await app.StopAsync(ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Orders the document's paths and component schemas so that repeated exports of
    /// unchanged code produce byte-identical output.
    /// </summary>
    /// <param name="document">The document to sort in place.</param>
    private static void Sort(OpenApiDocument document)
    {
        var sortedPaths = new OpenApiPaths();
        foreach (KeyValuePair<string, OpenApiPathItem> entry in document.Paths.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sortedPaths.Add(entry.Key, entry.Value);
        }

        document.Paths = sortedPaths;

        if (document.Components?.Schemas is { Count: > 0 } schemas)
        {
            document.Components.Schemas = schemas
                .OrderBy(s => s.Key, StringComparer.Ordinal)
                .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        }

        if (document.Components?.SecuritySchemes is { Count: > 0 } securitySchemes)
        {
            document.Components.SecuritySchemes = securitySchemes
                .OrderBy(s => s.Key, StringComparer.Ordinal)
                .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// An <see cref="IServer"/> that accepts no connections.
    /// </summary>
    /// <remarks>
    /// Substituted for Kestrel during an export so that starting the host wires up routing
    /// and endpoint metadata without binding a socket. Without it the export would need a
    /// free port on the CI runner, and a failure to shut down would hang the job — which is
    /// precisely how the previous drift job burned six hours per push.
    /// </remarks>
    internal sealed class NullServer : IServer
    {
        /// <summary>Gets the server's feature collection, which is empty.</summary>
        public IFeatureCollection Features { get; } = new FeatureCollection();

        /// <summary>Releases resources held by the server. There are none.</summary>
        public void Dispose()
        {
            // Nothing to release: this server owns no sockets, threads or unmanaged handles.
        }

        /// <summary>Starts the server, which accepts no connections.</summary>
        /// <typeparam name="TContext">The request context type.</typeparam>
        /// <param name="application">The application that would handle requests.</param>
        /// <param name="ct">A token to cancel startup.</param>
        /// <returns>A completed task.</returns>
        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken ct)
            where TContext : notnull => Task.CompletedTask;

        /// <summary>Stops the server.</summary>
        /// <param name="ct">A token to cancel shutdown.</param>
        /// <returns>A completed task.</returns>
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
