using Prism.Common.Abstractions;

namespace Prism.Tests.Unit;

/// <summary>
/// Proofs that user-supplied paging can never reach the database as a negative or overflowing
/// LIMIT/OFFSET — the defect that turned a caller's bad query string into a Postgres 500
/// (2201W "LIMIT must not be negative" / 2201X "OFFSET must not be negative").
/// </summary>
public sealed class PaginationTests
{
    /// <summary>
    /// A normal request is passed through unchanged.
    /// </summary>
    [Fact]
    public void Normal_Paging_Is_Unchanged()
    {
        (int page, int pageSize, int skip, int take) = Pagination.Normalize(3, 20);

        Assert.Equal(3, page);
        Assert.Equal(20, pageSize);
        Assert.Equal(40, skip);
        Assert.Equal(20, take);
    }

    /// <summary>
    /// Page below one floors at one, so skip is zero — never negative.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Page_Below_One_Floors_At_One(int requestedPage)
    {
        (int page, _, int skip, _) = Pagination.Normalize(requestedPage, 20);

        Assert.Equal(1, page);
        Assert.Equal(0, skip);
    }

    /// <summary>
    /// A non-positive page size falls back to the default rather than producing a zero or
    /// negative LIMIT.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(int.MinValue)]
    public void Non_Positive_Page_Size_Falls_Back_To_Default(int requestedSize)
    {
        (_, int pageSize, _, int take) = Pagination.Normalize(1, requestedSize);

        Assert.Equal(Pagination.DefaultPageSize, pageSize);
        Assert.Equal(Pagination.DefaultPageSize, take);
    }

    /// <summary>
    /// Page size is capped, so one request cannot ask the server to materialize the whole table.
    /// </summary>
    [Theory]
    [InlineData(201)]
    [InlineData(1_000_000)]
    [InlineData(int.MaxValue)]
    public void Oversized_Page_Size_Is_Capped(int requestedSize)
    {
        (_, int pageSize, _, int take) = Pagination.Normalize(1, requestedSize);

        Assert.Equal(Pagination.MaxPageSize, pageSize);
        Assert.Equal(Pagination.MaxPageSize, take);
    }

    /// <summary>
    /// A huge page number computes an offset in 64-bit and saturates instead of overflowing to
    /// a negative int — the exact path that produced "OFFSET must not be negative".
    /// </summary>
    [Fact]
    public void Huge_Page_Saturates_The_Offset_Non_Negative()
    {
        (_, _, int skip, _) = Pagination.Normalize(int.MaxValue, Pagination.MaxPageSize);

        Assert.True(skip >= 0, $"Skip overflowed to {skip}.");
        Assert.Equal(int.MaxValue, skip);
    }

    /// <summary>
    /// Every skip and take the helper returns is non-negative across a hostile input grid —
    /// the invariant the database depends on.
    /// </summary>
    [Fact]
    public void Skip_And_Take_Are_Always_Non_Negative()
    {
        int[] values = [int.MinValue, -1000, -1, 0, 1, 20, 1000, int.MaxValue];

        foreach (int p in values)
        {
            foreach (int s in values)
            {
                (_, _, int skip, int take) = Pagination.Normalize(p, s);
                Assert.True(skip >= 0, $"skip < 0 for page={p}, size={s}");
                Assert.True(take >= 1, $"take < 1 for page={p}, size={s}");
                Assert.True(take <= Pagination.MaxPageSize, $"take > max for page={p}, size={s}");
            }
        }
    }
}
