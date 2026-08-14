using System.Net;
using System.Text;

namespace Prism.Tests.Support;

/// <summary>
/// An <see cref="IHttpClientFactory"/> whose clients return scripted responses, including
/// responses whose body fails partway through.
/// </summary>
/// <remarks>
/// Streaming failures are only observable when the transport can deliver some bytes and then
/// break. Nothing in the suite could express that before, which is why the SSE fault path went
/// untested long enough for <c>streamError</c> to be declared, checked, and never assigned.
/// </remarks>
public sealed class FakeHttpTransport : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    private FakeHttpTransport(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    /// <summary>
    /// Gets the requests the transport has received, in order.
    /// </summary>
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>
    /// Gets the request bodies as sent, in order.
    /// </summary>
    /// <remarks>
    /// Captured at send time rather than read from the request afterwards: some providers
    /// dispose their request content once the call completes, so reading it later throws.
    /// </remarks>
    public List<string> RequestBodies { get; } = [];

    /// <summary>
    /// Creates a transport that emits the given Server-Sent Events frames and then throws
    /// while the body is still being read.
    /// </summary>
    /// <param name="framesBeforeFailure">Complete SSE frames to deliver before failing.</param>
    /// <param name="failureMessage">Message carried by the thrown <see cref="IOException"/>.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport SseThatFailsMidStream(
        IEnumerable<string> framesBeforeFailure,
        string failureMessage = "connection reset by peer")
    {
        string prefix = string.Concat(framesBeforeFailure.Select(f => f + "\n\n"));

        return new FakeHttpTransport(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(
                    new FailingStream(Encoding.UTF8.GetBytes(prefix), failureMessage)),
            };
            response.Content.Headers.ContentType = new("text/event-stream");
            return response;
        });
    }

    /// <summary>
    /// Creates a transport that emits the given SSE frames and then completes normally.
    /// </summary>
    /// <param name="frames">Complete SSE frames to deliver.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport Sse(IEnumerable<string> frames)
    {
        string body = string.Concat(frames.Select(f => f + "\n\n"));

        return new FakeHttpTransport(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8),
            };
            response.Content.Headers.ContentType = new("text/event-stream");
            return response;
        });
    }


    /// <summary>
    /// Creates a transport returning a fixed JSON body for every request.
    /// </summary>
    /// <param name="json">The response body.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport Json(string json)
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });


    /// <summary>
    /// Creates a transport returning a chat completion whose assistant message is the given text.
    /// </summary>
    /// <param name="content">The assistant reply.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport ChatCompletion(string content)
        => Json(ChatCompletionBody(content));

    /// <summary>
    /// The body of a chat completion carrying the given assistant message.
    /// </summary>
    /// <param name="content">The assistant reply.</param>
    /// <returns>The JSON body.</returns>
    private static string ChatCompletionBody(string content)
        => $$"""
            {
              "id": "chatcmpl-1",
              "object": "chat.completion",
              "model": "test-model",
              "choices": [
                {"index": 0, "message": {"role": "assistant", "content": {{System.Text.Json.JsonSerializer.Serialize(content)}}}, "finish_reason": "stop"}
              ],
              "usage": {"prompt_tokens": 5, "completion_tokens": 3, "total_tokens": 8}
            }
            """;

    /// <summary>
    /// Creates a transport that answers each successive request with the next reply, so a
    /// multi-turn exchange — an agent thinking, calling a tool, then answering — can be scripted.
    /// The last reply is repeated once the script runs out.
    /// </summary>
    /// <param name="replies">Assistant message contents, in the order they should be returned.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport ChatCompletions(params string[] replies)
    {
        int call = -1;

        return new FakeHttpTransport(_ =>
        {
            int index = Math.Min(Interlocked.Increment(ref call), replies.Length - 1);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ChatCompletionBody(replies[index]), Encoding.UTF8, "application/json"),
            };
        });
    }

    /// <summary>
    /// Creates a transport that refuses every request with the given status and body, the way a
    /// server does when it is asked for a model it does not have.
    /// </summary>
    /// <param name="status">The status to answer with.</param>
    /// <param name="body">The response body.</param>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport Refuses(HttpStatusCode status, string body)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });

    /// <summary>
    /// Creates a transport that answers each request according to the first route whose fragment
    /// appears in the request URL.
    /// </summary>
    /// <param name="routes">
    /// Pairs of fragment and JSON body, tried in order. A fragment matches when it appears in
    /// either the request URL or the request body — <c>/api/show</c> names its model in the body,
    /// so routing on the URL alone cannot tell one model's answer from another's. A request
    /// matching no route is answered with 404, which is what a server does for a path it does
    /// not serve.
    /// </param>
    /// <returns>A configured transport.</returns>
    /// <remarks>
    /// Choosing a model takes two calls that must disagree — <c>/api/tags</c> for what exists and
    /// <c>/api/show</c> for what each one can do — so a transport with a single canned body cannot
    /// express the case at all.
    /// </remarks>
    public static FakeHttpTransport JsonByPath(params (string Fragment, string Json)[] routes)
        => new(request =>
        {
            string url = request.RequestUri?.ToString() ?? "";
            string body = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? "";
            string target = url + " " + body;

            foreach ((string fragment, string json) in routes)
            {
                if (target.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    };
                }
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"error\":\"not found\"}", Encoding.UTF8, "application/json"),
            };
        });

    /// <summary>
    /// Creates a transport that fails every request, for exercising error paths.
    /// </summary>
    /// <returns>A configured transport.</returns>
    public static FakeHttpTransport ServerError()
        => new(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("upstream exploded", Encoding.UTF8, "text/plain"),
        });

    /// <inheritdoc />
    public HttpClient CreateClient(string name)
        => new(new ScriptedHandler(this)) { Timeout = TimeSpan.FromSeconds(30) };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly FakeHttpTransport _owner;

        public ScriptedHandler(FakeHttpTransport owner) => _owner = owner;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _owner.Requests.Add(request);
            _owner.RequestBodies.Add(
                request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult() ?? "");

            return Task.FromResult(_owner._responder(request));
        }
    }

    /// <summary>
    /// Yields its buffered content once, then throws on the next read — modelling a connection
    /// dropped after headers and part of the body have arrived.
    /// </summary>
    private sealed class FailingStream : Stream
    {
        private readonly byte[] _content;
        private readonly string _failureMessage;
        private int _position;
        private bool _contentExhausted;

        public FailingStream(byte[] content, string failureMessage)
        {
            _content = content;
            _failureMessage = failureMessage;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position < _content.Length)
            {
                int toCopy = Math.Min(count, _content.Length - _position);
                Array.Copy(_content, _position, buffer, offset, toCopy);
                _position += toCopy;
                return toCopy;
            }

            if (!_contentExhausted)
            {
                _contentExhausted = true;
            }

            throw new IOException(_failureMessage);
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position < _content.Length)
            {
                int toCopy = Math.Min(buffer.Length, _content.Length - _position);
                _content.AsSpan(_position, toCopy).CopyTo(buffer.Span);
                _position += toCopy;
                return ValueTask.FromResult(toCopy);
            }

            throw new IOException(_failureMessage);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
