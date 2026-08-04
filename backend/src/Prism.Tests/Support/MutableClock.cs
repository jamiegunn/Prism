namespace Prism.Tests.Support;

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when a test moves it.
/// </summary>
/// <remarks>
/// Lease expiry is a function of elapsed time. Testing it against the real clock would mean
/// sleeping for the lease duration, which makes the suite slow and flaky in equal measure.
/// Hand-rolled rather than taking Microsoft.Extensions.TimeProvider.Testing, which is not
/// available in this environment's package feed.
/// </remarks>
public sealed class MutableClock : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>
    /// Initializes a new instance of the <see cref="MutableClock"/> class.
    /// </summary>
    /// <param name="start">The initial instant.</param>
    public MutableClock(DateTimeOffset start) => _now = start;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="delta">How far to advance.</param>
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
}
