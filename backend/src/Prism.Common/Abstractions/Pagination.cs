namespace Prism.Common.Abstractions;

/// <summary>
/// Normalizes user-supplied paging parameters into a safe (skip, take) pair.
/// </summary>
/// <remarks>
/// Page and page size arrive straight from the query string, so they can be negative, zero, or
/// large enough to overflow. Fed unclamped into <c>Skip((page - 1) * size)</c> / <c>Take(size)</c>
/// they reached Postgres as a negative LIMIT or OFFSET, which is a 500 (<c>2201W</c> / <c>2201X</c>)
/// — a caller's bad query reported as a server crash. Every paged handler routes through here so
/// the clamp is defined once and cannot drift between endpoints.
/// </remarks>
public static class Pagination
{
    /// <summary>The largest page size any endpoint will serve in a single request.</summary>
    public const int MaxPageSize = 200;

    /// <summary>The page size used when the request does not ask for a valid one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>
    /// Clamps a requested page and page size, and computes the row offset without overflowing.
    /// Page floors at 1; page size is clamped to [1, <see cref="MaxPageSize"/>], with a
    /// non-positive size falling back to <see cref="DefaultPageSize"/>. The offset is computed in
    /// 64-bit and saturated to <see cref="int.MaxValue"/> so a page number past the end asks for
    /// an empty tail rather than wrapping negative.
    /// </summary>
    /// <param name="page">The requested one-based page number.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <returns>The normalized page, size, and a non-negative skip and take.</returns>
    public static (int Page, int PageSize, int Skip, int Take) Normalize(int page, int pageSize)
    {
        int normalizedPage = page < 1 ? 1 : page;
        int normalizedSize = pageSize <= 0
            ? DefaultPageSize
            : Math.Min(pageSize, MaxPageSize);

        long skip = (long)(normalizedPage - 1) * normalizedSize;
        int safeSkip = skip > int.MaxValue ? int.MaxValue : (int)skip;

        return (normalizedPage, normalizedSize, safeSkip, normalizedSize);
    }
}
