using System.Text.Json;

namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// A tool that makes HTTP GET requests to external APIs and returns the response body.
/// Input should be a valid URL.
/// </summary>
public sealed class ApiCallTool : IAgentTool
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiCallTool"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating clients.</param>
    public ApiCallTool(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public string Name => "api_call";

    /// <inheritdoc />
    public string Description => "Makes an HTTP GET request to the specified URL and returns the response body. Input should be a valid URL.";

    /// <inheritdoc />
    public string ParameterSchema => """{"type": "string", "description": "The URL to send a GET request to"}""";

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        string url = input.Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return ToolResult.Fail("Invalid URL. Must be an absolute HTTP or HTTPS URL.");
        }

        try
        {
            HttpClient client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            HttpResponseMessage response = await client.GetAsync(uri, ct);
            string body = await response.Content.ReadAsStringAsync(ct);

            // Truncate very large responses
            if (body.Length > 4000)
            {
                body = body[..4000] + "\n... [truncated]";
            }

            return ToolResult.Ok($"Status: {(int)response.StatusCode}\n{body}");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"HTTP request failed: {ex.Message}");
        }
    }
}
