using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.CreateVersion;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Application.ForkTemplate;
using Prism.Features.PromptLab.Domain;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs for the two ways a prompt template can be branched: forking and versioning.
/// </summary>
/// <remarks>
/// Both accepted requests they could not honour. A fork that did not name a version looked for
/// version 0 and reported <c>Version 0 of template … not found</c>, naming a version the caller
/// never asked for. A new version could be created whose template referenced variables it did
/// not declare — accepted with a 201, then rejected by every attempt to run it with
/// <c>Undeclared variables in template</c>. In both cases the complaint arrives somewhere other
/// than the mistake.
/// </remarks>
[Collection("Database")]
public sealed class PromptLabVersioningTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptLabVersioningTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public PromptLabVersioningTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// Forking without naming a version takes the latest one.
    /// </summary>
    /// <remarks>
    /// "Fork this template" has an obvious meaning — the version in front of you — and version 0
    /// has never existed, so the old 404 could not be right for any input.
    /// </remarks>
    [Fact]
    public async Task A_Fork_With_No_Version_Named_Takes_The_Latest()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 3);

        Result<PromptTemplateWithVersionDto> result = await CreateForkHandler(db).HandleAsync(
            new ForkTemplateCommand(templateId, SourceVersion: 0), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        // v3's text, carried into v1 of the fork.
        Assert.Equal("template v3 {{topic}}", result.Value.LatestVersionContent!.UserTemplate);
        Assert.Equal(1, result.Value.LatestVersionContent!.Version);
    }

    /// <summary>
    /// Naming a version still forks that one, not the latest.
    /// </summary>
    [Fact]
    public async Task A_Fork_That_Names_A_Version_Gets_That_Version()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 3);

        Result<PromptTemplateWithVersionDto> result = await CreateForkHandler(db).HandleAsync(
            new ForkTemplateCommand(templateId, SourceVersion: 2), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("template v2 {{topic}}", result.Value.LatestVersionContent!.UserTemplate);
    }

    /// <summary>
    /// A version that does not exist is still a not-found, and says which one.
    /// </summary>
    [Fact]
    public async Task A_Fork_Of_A_Version_That_Does_Not_Exist_Is_Not_Found()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 2);

        Result<PromptTemplateWithVersionDto> result = await CreateForkHandler(db).HandleAsync(
            new ForkTemplateCommand(templateId, SourceVersion: 9), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Contains("9", result.Error.Message);
    }

    /// <summary>
    /// A version whose template uses variables it does not declare is refused at creation.
    /// </summary>
    /// <remarks>
    /// This was a 201 followed by a version that could never be tested or rendered. Refusing it
    /// here puts the message where the author can act on it, and names the placeholders rather
    /// than leaving them to be found by trial.
    /// </remarks>
    [Fact]
    public async Task A_Version_Cannot_Use_Variables_It_Does_Not_Declare()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 1);

        Result<PromptVersionDto> result = await CreateVersionHandlerFor(db).HandleAsync(
            new CreateVersionCommand(
                templateId,
                SystemPrompt: null,
                UserTemplate: "Review this {{language}} code:\n{{code}}",
                Variables: [],
                FewShotExamples: null,
                Notes: null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains("language", result.Error.Message);
        Assert.Contains("code", result.Error.Message);
    }

    /// <summary>
    /// Declaring the variables it uses is accepted, and the version number advances.
    /// </summary>
    [Fact]
    public async Task A_Version_That_Declares_Its_Variables_Is_Created()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 1);

        Result<PromptVersionDto> result = await CreateVersionHandlerFor(db).HandleAsync(
            new CreateVersionCommand(
                templateId,
                SystemPrompt: "You are terse.",
                UserTemplate: "Review this {{language}} code.",
                Variables: [new PromptVariable { Name = "language", Required = true }],
                FewShotExamples: null,
                Notes: "shorter"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(2, result.Value.Version);
    }

    /// <summary>
    /// A template with no placeholders needs no declarations.
    /// </summary>
    [Fact]
    public async Task A_Version_With_No_Placeholders_Needs_No_Variables()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid templateId = await SeedTemplateAsync(db, versions: 1);

        Result<PromptVersionDto> result = await CreateVersionHandlerFor(db).HandleAsync(
            new CreateVersionCommand(
                templateId, null, "Summarise the conversation so far.", null, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
    }

    private static ForkTemplateHandler CreateForkHandler(AppDbContext db)
        => new(db, NullLogger<ForkTemplateHandler>.Instance);

    private static CreateVersionHandler CreateVersionHandlerFor(AppDbContext db)
        => new(db, NullLogger<CreateVersionHandler>.Instance);

    private static async Task<Guid> SeedTemplateAsync(AppDbContext db, int versions)
    {
        var template = new PromptTemplate
        {
            Name = $"fork-source-{Guid.NewGuid():N}",
            Category = "test",
            Description = "seeded",
            Tags = ["seed"],
            LatestVersion = versions,
        };

        db.Set<PromptTemplate>().Add(template);

        for (int v = 1; v <= versions; v++)
        {
            db.Set<PromptVersion>().Add(new PromptVersion
            {
                TemplateId = template.Id,
                Version = v,
                UserTemplate = $"template v{v} {{{{topic}}}}",
                Variables = [new PromptVariable { Name = "topic", Required = true }],
            });
        }

        await db.SaveChangesAsync();
        return template.Id;
    }
}
