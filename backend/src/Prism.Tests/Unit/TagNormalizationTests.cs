using Prism.Features.History.Application.TagRecord;

namespace Prism.Tests.Unit;

/// <summary>
/// Proofs for server-side tag normalization. The tag endpoint used to assign the caller's
/// list straight onto a required non-nullable column, so <c>{"tags":null}</c> was a 500 and
/// null/blank/duplicate entries persisted whenever the caller was not the UI.
/// </summary>
public sealed class TagNormalizationTests
{
    /// <summary>
    /// A null list clears the tags rather than throwing — the crash the fix targets.
    /// </summary>
    [Fact]
    public void Null_List_Becomes_Empty()
    {
        Assert.Empty(TagRecordHandler.NormalizeTags(null));
    }

    /// <summary>
    /// Null and blank entries are dropped; nothing empty reaches the column.
    /// </summary>
    [Fact]
    public void Null_And_Blank_Entries_Are_Dropped()
    {
        List<string> result = TagRecordHandler.NormalizeTags([null, "", "   ", "\t", "keep"]);

        Assert.Equal(["keep"], result);
    }

    /// <summary>
    /// Entries are trimmed and lowercased to the form the tag filter matches — so a tag
    /// written here is findable by the same string the list shows.
    /// </summary>
    [Fact]
    public void Entries_Are_Trimmed_And_Lowercased()
    {
        List<string> result = TagRecordHandler.NormalizeTags(["  Prod  ", "GPT-4"]);

        Assert.Equal(["prod", "gpt-4"], result);
    }

    /// <summary>
    /// Duplicates — including ones that only collide after normalization — are dropped, and
    /// first-seen order is preserved.
    /// </summary>
    [Fact]
    public void Duplicates_Are_Dropped_Preserving_Order()
    {
        List<string> result = TagRecordHandler.NormalizeTags(["a", "B", "a", " b ", "c", "A"]);

        Assert.Equal(["a", "b", "c"], result);
    }

    /// <summary>
    /// An all-junk list normalizes to empty, never to a list of empty strings.
    /// </summary>
    [Fact]
    public void All_Junk_Becomes_Empty()
    {
        Assert.Empty(TagRecordHandler.NormalizeTags([null, "", "  ", null]));
    }
}
