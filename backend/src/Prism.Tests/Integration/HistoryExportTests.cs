using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Parquet;
using Parquet.Data;
using Parquet.Schema;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.History.Application.ExportHistory;
using Prism.Features.History.Application.SearchHistory;
using Prism.Features.History.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Round-trip proofs for the history export: what leaves in the file is exactly what the
/// filters selected, field by field, with null surviving as null in every format.
/// </summary>
/// <remarks>
/// <para>
/// Each test seeds records under a unique <c>SourceModule</c> marker and filters on it, so
/// tests are isolated from each other and from whatever else shares the database fixture.
/// </para>
/// <para>
/// Timestamp tolerance: Parquet stores milliseconds (<c>DateTimeFormat.DateAndTime</c>), so
/// Parquet assertions compare to the millisecond. JSONL and CSV carry ISO-8601 round-trip
/// strings and are compared exactly.
/// </para>
/// </remarks>
[Collection("Database")]
public sealed class HistoryExportTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryExportTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public HistoryExportTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Builds a record with every scalar populated, so a round trip exercises each field.
    /// </summary>
    private static InferenceRecord FullRecord(string marker, int offsetMinutes) => new()
    {
        SourceModule = marker,
        ProviderName = "Test Provider",
        ProviderType = InferenceProviderType.Ollama,
        ProviderEndpoint = "http://localhost:11434",
        Model = "test-model:7b",
        RequestJson = """{"messages":[{"role":"user","content":"Hello, \"quoted\" and, comma\nand newline"}]}""",
        ResponseJson = """{"content":"Hi — unicode: héllo 你好"}""",
        PromptTokens = 12,
        CompletionTokens = 34,
        TotalTokens = 46,
        LatencyMs = 1234,
        TtftMs = 87,
        Perplexity = 3.14159265358979,
        MeanEntropy = 1.25,
        SurpriseTokenCount = 2,
        TokensPerSecond = 27.5,
        EstimatedCost = 0.00012345m,
        IsSuccess = true,
        ErrorMessage = null,
        Tags = ["alpha", "beta"],
        StartedAt = DateTime.UtcNow.AddMinutes(-offsetMinutes),
        CompletedAt = DateTime.UtcNow.AddMinutes(-offsetMinutes).AddSeconds(2),
    };

    /// <summary>
    /// Builds a failed record where every nullable is null — the row that catches a format
    /// writer that turns "not measured" into a zero or an empty string.
    /// </summary>
    private static InferenceRecord NullHeavyRecord(string marker) => new()
    {
        SourceModule = marker,
        ProviderName = "Test Provider",
        ProviderType = InferenceProviderType.Vllm,
        ProviderEndpoint = "http://localhost:8000",
        Model = "test-model:7b",
        RequestJson = """{"messages":[]}""",
        ResponseJson = null,
        PromptTokens = 0,
        CompletionTokens = 0,
        TotalTokens = 0,
        LatencyMs = 5,
        TtftMs = null,
        Perplexity = null,
        MeanEntropy = null,
        SurpriseTokenCount = null,
        TokensPerSecond = null,
        EstimatedCost = null,
        IsSuccess = false,
        ErrorMessage = "provider refused the connection",
        Tags = [],
        StartedAt = DateTime.UtcNow.AddMinutes(-90),
        CompletedAt = DateTime.UtcNow.AddMinutes(-90).AddMilliseconds(5),
    };

    private static ExportHistoryHandler Handler(AppDbContext db) =>
        new(db, NullLogger<ExportHistoryHandler>.Instance);

    private static SearchHistoryQuery MarkerFilter(string marker) =>
        new(null, marker, null, null, null, null, null, 1, 1000);

    private static async Task<byte[]> ExportBytesAsync(
        AppDbContext db, SearchHistoryQuery filters, string format)
    {
        Result<HistoryExport> result = await Handler(db).HandleAsync(
            new ExportHistoryQuery(filters, format), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : "");

        using var stream = new MemoryStream();
        await result.Value.WriteAsync(stream, CancellationToken.None);
        return stream.ToArray();
    }

    /// <summary>
    /// JSONL round trip: every scalar field of every selected row is present and equal, and a
    /// null field is a JSON null — not 0, not "", not missing.
    /// </summary>
    [Fact]
    public async Task Jsonl_RoundTrips_Every_Field_And_Preserves_Null()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string marker = $"export-jsonl-{Guid.NewGuid():N}";
        InferenceRecord full = FullRecord(marker, 10);
        InferenceRecord nullHeavy = NullHeavyRecord(marker);
        db.AddRange(full, nullHeavy);
        await db.SaveChangesAsync();

        byte[] bytes = await ExportBytesAsync(db, MarkerFilter(marker), "jsonl");

        string[] lines = System.Text.Encoding.UTF8.GetString(bytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);

        Dictionary<Guid, JsonElement> byId = lines
            .Select(l => JsonDocument.Parse(l).RootElement)
            .ToDictionary(e => e.GetProperty("id").GetGuid());

        Assert.Equal(
            new HashSet<Guid> { full.Id, nullHeavy.Id },
            byId.Keys.ToHashSet());

        JsonElement f = byId[full.Id];
        Assert.Equal(full.SourceModule, f.GetProperty("sourceModule").GetString());
        Assert.Equal(full.ProviderName, f.GetProperty("providerName").GetString());
        Assert.Equal("Ollama", f.GetProperty("providerType").GetString());
        Assert.Equal(full.ProviderEndpoint, f.GetProperty("providerEndpoint").GetString());
        Assert.Equal(full.Model, f.GetProperty("model").GetString());
        Assert.Equal(full.RequestJson, f.GetProperty("requestJson").GetString());
        Assert.Equal(full.ResponseJson, f.GetProperty("responseJson").GetString());
        Assert.Equal(full.PromptTokens, f.GetProperty("promptTokens").GetInt32());
        Assert.Equal(full.CompletionTokens, f.GetProperty("completionTokens").GetInt32());
        Assert.Equal(full.TotalTokens, f.GetProperty("totalTokens").GetInt32());
        Assert.Equal(full.LatencyMs, f.GetProperty("latencyMs").GetInt64());
        Assert.Equal(full.TtftMs!.Value, f.GetProperty("ttftMs").GetInt32());

        // Doubles: exact bit equality is the contract — the writer uses round-trip formatting
        // and System.Text.Json round-trips doubles losslessly.
        Assert.Equal(full.Perplexity!.Value, f.GetProperty("perplexity").GetDouble());
        Assert.Equal(full.MeanEntropy!.Value, f.GetProperty("meanEntropy").GetDouble());
        Assert.Equal(full.SurpriseTokenCount!.Value, f.GetProperty("surpriseTokenCount").GetInt32());
        Assert.Equal(full.TokensPerSecond!.Value, f.GetProperty("tokensPerSecond").GetDouble());
        Assert.Equal(full.EstimatedCost!.Value, f.GetProperty("estimatedCost").GetDecimal());
        Assert.True(f.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(JsonValueKind.Null, f.GetProperty("errorMessage").ValueKind);
        Assert.Equal(
            full.Tags,
            f.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToList());
        Assert.Equal(full.StartedAt, f.GetProperty("startedAt").GetDateTime(), TimeSpan.FromMilliseconds(1));
        Assert.Equal(full.CompletedAt, f.GetProperty("completedAt").GetDateTime(), TimeSpan.FromMilliseconds(1));

        // The null-heavy row: null must be JSON null, present, and never a zero.
        JsonElement n = byId[nullHeavy.Id];
        foreach (string nullField in new[]
                 {
                     "responseJson", "ttftMs", "perplexity", "meanEntropy",
                     "surpriseTokenCount", "tokensPerSecond", "estimatedCost",
                 })
        {
            Assert.True(n.TryGetProperty(nullField, out JsonElement value),
                $"{nullField} must be present on the null-heavy row");
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }

        Assert.False(n.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(nullHeavy.ErrorMessage, n.GetProperty("errorMessage").GetString());
    }

    /// <summary>
    /// CSV round trip: nulls are empty fields, empty strings are quoted empty fields, and
    /// embedded quotes, commas and newlines survive RFC-4180 escaping.
    /// </summary>
    [Fact]
    public async Task Csv_RoundTrips_And_Distinguishes_Null_From_Empty()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string marker = $"export-csv-{Guid.NewGuid():N}";
        InferenceRecord full = FullRecord(marker, 10);
        InferenceRecord nullHeavy = NullHeavyRecord(marker);

        // A third record with an empty (not null) response — the case CSV must distinguish.
        InferenceRecord emptyResponse = FullRecord(marker, 20);
        emptyResponse.ResponseJson = "";

        db.AddRange(full, nullHeavy, emptyResponse);
        await db.SaveChangesAsync();

        byte[] bytes = await ExportBytesAsync(db, MarkerFilter(marker), "csv");
        List<List<string?>> rows = ParseCsv(System.Text.Encoding.UTF8.GetString(bytes));

        Assert.Equal(4, rows.Count); // header + 3 rows
        Assert.Equal(ExportHistoryHandler.CsvColumns, rows[0].Select(c => c!).ToArray());

        int idCol = 0;
        int responseCol = Array.IndexOf(ExportHistoryHandler.CsvColumns, "responseJson");
        int perplexityCol = Array.IndexOf(ExportHistoryHandler.CsvColumns, "perplexity");
        int requestCol = Array.IndexOf(ExportHistoryHandler.CsvColumns, "requestJson");

        Dictionary<Guid, List<string?>> byId = rows.Skip(1)
            .ToDictionary(r => Guid.Parse(r[idCol]!));

        Assert.Equal(
            new HashSet<Guid> { full.Id, nullHeavy.Id, emptyResponse.Id },
            byId.Keys.ToHashSet());

        // Null response: the parser reports an unquoted empty field as null.
        Assert.Null(byId[nullHeavy.Id][responseCol]);

        // Empty-but-present response: quoted, so the parser reports an empty string.
        Assert.Equal("", byId[emptyResponse.Id][responseCol]);

        // Null numeric renders as empty — never "0".
        Assert.Null(byId[nullHeavy.Id][perplexityCol]);

        // Round-trip double survives the "R" format exactly.
        Assert.Equal(
            full.Perplexity!.Value,
            double.Parse(byId[full.Id][perplexityCol]!, CultureInfo.InvariantCulture));

        // The embedded quote/comma/newline request string survives.
        Assert.Equal(full.RequestJson, byId[full.Id][requestCol]);
    }

    /// <summary>
    /// Parquet round trip: ids and every scalar match, and null survives as null.
    /// </summary>
    [Fact]
    public async Task Parquet_RoundTrips_Every_Field_And_Preserves_Null()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string marker = $"export-parquet-{Guid.NewGuid():N}";
        InferenceRecord full = FullRecord(marker, 10);
        InferenceRecord nullHeavy = NullHeavyRecord(marker);
        db.AddRange(full, nullHeavy);
        await db.SaveChangesAsync();

        byte[] bytes = await ExportBytesAsync(db, MarkerFilter(marker), "parquet");

        using var stream = new MemoryStream(bytes);
        using ParquetReader reader = await ParquetReader.CreateAsync(stream);

        Assert.Equal(1, reader.RowGroupCount);

        Dictionary<string, Array> columns = await ReadAllColumnsAsync(reader);

        string[] ids = (string[])columns["id"];
        int fullIdx = Array.IndexOf(ids, full.Id.ToString());
        int nullIdx = Array.IndexOf(ids, nullHeavy.Id.ToString());
        Assert.True(fullIdx >= 0 && nullIdx >= 0, "both seeded rows must be present");

        Assert.Equal(full.SourceModule, ((string[])columns["sourceModule"])[fullIdx]);
        Assert.Equal("Ollama", ((string[])columns["providerType"])[fullIdx]);
        Assert.Equal(full.RequestJson, ((string[])columns["requestJson"])[fullIdx]);
        Assert.Equal(full.ResponseJson, ((string?[])columns["responseJson"])[fullIdx]);
        Assert.Equal(full.PromptTokens, ((int[])columns["promptTokens"])[fullIdx]);
        Assert.Equal(full.LatencyMs, ((long[])columns["latencyMs"])[fullIdx]);
        Assert.Equal(full.TtftMs, ((int?[])columns["ttftMs"])[fullIdx]);
        Assert.Equal(full.Perplexity!.Value, ((double?[])columns["perplexity"])[fullIdx]!.Value, 12);
        Assert.Equal(full.EstimatedCost!.Value, ((decimal?[])columns["estimatedCost"])[fullIdx]!.Value);
        Assert.True(((bool[])columns["isSuccess"])[fullIdx]);
        Assert.Equal(
            JsonSerializer.Serialize(full.Tags),
            ((string[])columns["tags"])[fullIdx]);

        // Parquet stores milliseconds — compare at that precision, and no finer.
        DateTime storedStart = ((DateTime[])columns["startedAt"])[fullIdx];
        Assert.Equal(
            new DateTimeOffset(full.StartedAt).ToUnixTimeMilliseconds(),
            new DateTimeOffset(DateTime.SpecifyKind(storedStart, DateTimeKind.Utc)).ToUnixTimeMilliseconds());

        // Null survives as null in every nullable column.
        Assert.Null(((string?[])columns["responseJson"])[nullIdx]);
        Assert.Null(((int?[])columns["ttftMs"])[nullIdx]);
        Assert.Null(((double?[])columns["perplexity"])[nullIdx]);
        Assert.Null(((double?[])columns["meanEntropy"])[nullIdx]);
        Assert.Null(((int?[])columns["surpriseTokenCount"])[nullIdx]);
        Assert.Null(((double?[])columns["tokensPerSecond"])[nullIdx]);
        Assert.Null(((decimal?[])columns["estimatedCost"])[nullIdx]);
        Assert.Equal("provider refused the connection", ((string?[])columns["errorMessage"])[nullIdx]);
    }

    /// <summary>
    /// The Parquet schema mirrors <see cref="HistoryExportRow"/> field by field: same names,
    /// same order, and nullability matching the DTO's nullability exactly.
    /// </summary>
    [Fact]
    public void Parquet_Schema_Matches_The_Export_Row_Field_By_Field()
    {
        // (name, clr type, nullable) triples derived by hand from HistoryExportRow. Guid and
        // Tags are strings by design (documented on the writer); decimal is a DecimalDataField.
        (string Name, Type ClrType, bool Nullable)[] expected =
        [
            ("id", typeof(string), false),
            ("sourceModule", typeof(string), false),
            ("providerName", typeof(string), false),
            ("providerType", typeof(string), false),
            ("providerEndpoint", typeof(string), false),
            ("model", typeof(string), false),
            ("requestJson", typeof(string), false),
            ("responseJson", typeof(string), true),
            ("promptTokens", typeof(int), false),
            ("completionTokens", typeof(int), false),
            ("totalTokens", typeof(int), false),
            ("latencyMs", typeof(long), false),
            ("ttftMs", typeof(int), true),
            ("perplexity", typeof(double), true),
            ("meanEntropy", typeof(double), true),
            ("surpriseTokenCount", typeof(int), true),
            ("tokensPerSecond", typeof(double), true),
            ("estimatedCost", typeof(decimal), true),
            ("isSuccess", typeof(bool), false),
            ("errorMessage", typeof(string), true),
            ("tags", typeof(string), false),
            ("startedAt", typeof(DateTime), false),
            ("completedAt", typeof(DateTime), false),
        ];

        DataField[] fields = ExportHistoryHandler.BuildParquetSchema().DataFields;

        Assert.Equal(expected.Length, fields.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Name, fields[i].Name);
            Assert.Equal(expected[i].ClrType, fields[i].ClrType);
            Assert.Equal(expected[i].Nullable, fields[i].IsNullable);
        }
    }

    /// <summary>
    /// The export selects exactly the set of ids the search endpoint returns for the same
    /// filters — including the tag filter, which used to throw before Tags became text[].
    /// </summary>
    [Fact]
    public async Task Export_Selects_Exactly_What_Search_Selects_Including_Tag_Filter()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string marker = $"export-filters-{Guid.NewGuid():N}";

        InferenceRecord tagged = FullRecord(marker, 5);
        tagged.Tags = ["needle", "other"];
        InferenceRecord untagged = FullRecord(marker, 6);
        untagged.Tags = ["hay"];
        InferenceRecord failed = NullHeavyRecord(marker);
        failed.Tags = ["needle"];

        db.AddRange(tagged, untagged, failed);
        await db.SaveChangesAsync();

        var filters = new SearchHistoryQuery(
            null, marker, null, null, null, ["needle"], IsSuccess: true, 1, 1000);

        var searchHandler = new SearchHistoryHandler(db, NullLogger<SearchHistoryHandler>.Instance);
        var searchResult = await searchHandler.HandleAsync(filters, CancellationToken.None);
        Assert.True(searchResult.IsSuccess,
            searchResult.IsFailure ? $"search failed: {searchResult.Error.Message}" : "");

        List<Guid> searchIds = searchResult.Value.Items.Select(i => i.Id).ToList();
        Assert.Equal([tagged.Id], searchIds);

        byte[] bytes = await ExportBytesAsync(db, filters, "jsonl");
        List<Guid> exportIds = System.Text.Encoding.UTF8.GetString(bytes)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.GetProperty("id").GetGuid())
            .ToList();

        Assert.Equal(searchIds.ToHashSet(), exportIds.ToHashSet());
    }

    /// <summary>
    /// More rows than one row group holds must produce multiple row groups — the proof that
    /// the writer batches instead of buffering the whole export.
    /// </summary>
    [Fact]
    public async Task Parquet_Writes_Multiple_Row_Groups_When_Rows_Exceed_The_Batch()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string marker = $"export-groups-{Guid.NewGuid():N}";

        var rows = new List<InferenceRecord>();
        for (int i = 0; i < ExportHistoryHandler.ParquetRowGroupSize + 1; i++)
        {
            InferenceRecord r = FullRecord(marker, 10);
            r.StartedAt = DateTime.UtcNow.AddSeconds(-i);
            rows.Add(r);
        }

        db.AddRange(rows);
        await db.SaveChangesAsync();

        byte[] bytes = await ExportBytesAsync(db, MarkerFilter(marker), "parquet");

        using var stream = new MemoryStream(bytes);
        using ParquetReader reader = await ParquetReader.CreateAsync(stream);

        Assert.Equal(2, reader.RowGroupCount);

        long total = 0;
        for (int g = 0; g < reader.RowGroupCount; g++)
        {
            using ParquetRowGroupReader group = reader.OpenRowGroupReader(g);
            total += group.RowCount;
        }

        Assert.Equal(ExportHistoryHandler.ParquetRowGroupSize + 1, total);
    }

    /// <summary>
    /// An unknown format is a 400-class validation error naming the supported formats — not a
    /// 500, and not a silent default.
    /// </summary>
    [Fact]
    public async Task Unknown_Format_Is_A_Validation_Error()
    {
        await using AppDbContext db = _fixture.CreateContext();

        Result<HistoryExport> result = await Handler(db).HandleAsync(
            new ExportHistoryQuery(MarkerFilter("none"), "xlsx"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("jsonl", result.Error.Message);
    }

    /// <summary>
    /// Zero matching rows still yields a well-formed file: empty JSONL, header-only CSV, and
    /// the reported row count is zero.
    /// </summary>
    [Fact]
    public async Task Empty_Selection_Exports_Valid_Empty_Files()
    {
        await using AppDbContext db = _fixture.CreateContext();
        SearchHistoryQuery filters = MarkerFilter($"no-such-module-{Guid.NewGuid():N}");

        Result<HistoryExport> jsonl = await Handler(db).HandleAsync(
            new ExportHistoryQuery(filters, "jsonl"), CancellationToken.None);
        Assert.True(jsonl.IsSuccess);
        Assert.Equal(0, jsonl.Value.RowCount);

        byte[] jsonlBytes = await ExportBytesAsync(db, filters, "jsonl");
        Assert.Empty(jsonlBytes);

        byte[] csvBytes = await ExportBytesAsync(db, filters, "csv");
        string csv = System.Text.Encoding.UTF8.GetString(csvBytes).TrimEnd('\r', '\n');
        Assert.Equal(string.Join(",", ExportHistoryHandler.CsvColumns), csv);
    }

    /// <summary>
    /// Reads every column of every row group into one array per column, concatenated in order.
    /// </summary>
    /// <param name="reader">The open Parquet reader.</param>
    /// <returns>Column name to concatenated data array.</returns>
    private static async Task<Dictionary<string, Array>> ReadAllColumnsAsync(ParquetReader reader)
    {
        var result = new Dictionary<string, Array>();
        DataField[] fields = reader.Schema.DataFields;

        for (int g = 0; g < reader.RowGroupCount; g++)
        {
            using ParquetRowGroupReader group = reader.OpenRowGroupReader(g);

            foreach (DataField field in fields)
            {
                DataColumn column = await group.ReadColumnAsync(field);

                if (!result.TryGetValue(field.Name, out Array? existing))
                {
                    result[field.Name] = column.Data;
                }
                else
                {
                    var combined = Array.CreateInstance(
                        existing.GetType().GetElementType()!, existing.Length + column.Data.Length);
                    existing.CopyTo(combined, 0);
                    column.Data.CopyTo(combined, existing.Length);
                    result[field.Name] = combined;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// A small RFC-4180 parser that reports an unquoted empty field as null and a quoted
    /// empty field as the empty string — the distinction the writer promises.
    /// </summary>
    /// <param name="text">The CSV text.</param>
    /// <returns>Rows of fields; null for unquoted-empty fields.</returns>
    private static List<List<string?>> ParseCsv(string text)
    {
        var rows = new List<List<string?>>();
        var current = new List<string?>();
        var field = new System.Text.StringBuilder();
        bool quoted = false;
        bool fieldWasQuoted = false;
        bool fieldStarted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    fieldWasQuoted = true;
                    fieldStarted = true;
                    break;
                case ',':
                    current.Add(Finish(field, fieldWasQuoted, fieldStarted));
                    fieldWasQuoted = false;
                    fieldStarted = false;
                    break;
                case '\r':
                    break;
                case '\n':
                    current.Add(Finish(field, fieldWasQuoted, fieldStarted));
                    rows.Add(current);
                    current = [];
                    fieldWasQuoted = false;
                    fieldStarted = false;
                    break;
                default:
                    field.Append(c);
                    fieldStarted = true;
                    break;
            }
        }

        if (fieldStarted || current.Count > 0)
        {
            current.Add(Finish(field, fieldWasQuoted, fieldStarted));
            rows.Add(current);
        }

        return rows;

        static string? Finish(System.Text.StringBuilder sb, bool wasQuoted, bool started)
        {
            string value = sb.ToString();
            sb.Clear();

            if (value.Length == 0 && !wasQuoted)
            {
                return null; // unquoted empty field = null
            }

            return value;
        }
    }
}
