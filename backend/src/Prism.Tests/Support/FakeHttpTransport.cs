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
