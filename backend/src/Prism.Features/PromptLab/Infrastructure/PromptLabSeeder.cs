using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Infrastructure;

/// <summary>
/// Seeds sample prompt templates with versioned content, variables, and few-shot examples
/// to demonstrate the Prompt Lab feature on first launch.
/// </summary>
public sealed class PromptLabSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. Prompt Lab seeds at order 130, after experiments.
    /// </summary>
    public int Order => 130;

    /// <summary>
    /// Seeds sample prompt templates if none exist.
    /// Creates a structured data extractor template (2 versions) and a code review assistant template (1 version).
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasTemplates = await context.Set<PromptTemplate>().AnyAsync(ct);

        if (hasTemplates)
        {
            return;
        }

        List<PromptTemplate> templates =
        [
            CreateStructuredDataExtractorTemplate(),
            CreateCodeReviewAssistantTemplate()
        ];

        context.Set<PromptTemplate>().AddRange(templates);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates the "Structured Data Extractor" template with two versions.
    /// Version 1 is a basic extraction template; version 2 adds a system prompt and few-shot example.
    /// </summary>
    /// <returns>A prompt template entity with two versions.</returns>
    private static PromptTemplate CreateStructuredDataExtractorTemplate()
    {
        Guid templateId = Guid.NewGuid();
        Guid v1Id = Guid.NewGuid();
        Guid v2Id = Guid.NewGuid();

        return new PromptTemplate
        {
            Id = templateId,
            Name = "Structured Data Extractor",
            Category = "extraction",
            Description = "Extracts structured JSON data from unstructured text input using configurable field definitions.",
            Tags = ["json", "extraction", "structured"],
            LatestVersion = 2,
            Versions =
            [
                new PromptVersion
                {
                    Id = v1Id,
                    TemplateId = templateId,
                    Version = 1,
                    UserTemplate = "Extract the following information from the text:\n\n{{text}}\n\nReturn JSON with fields: {{fields}}",
                    Variables =
                    [
                        new PromptVariable
                        {
                            Name = "text",
                            Type = "string",
                            Required = true,
                            Description = "Input text to extract from"
                        },
                        new PromptVariable
                        {
                            Name = "fields",
                            Type = "string",
                            Required = true,
                            Description = "Comma-separated list of fields",
                            DefaultValue = "name, date, location"
                        }
                    ],
                    Notes = "Initial version"
                },
                new PromptVersion
                {
                    Id = v2Id,
                    TemplateId = templateId,
                    Version = 2,
                    SystemPrompt = "You are a precise data extraction assistant. Output only valid JSON, no explanations.",
                    UserTemplate = "Extract the following information from the text:\n\n{{text}}\n\nReturn JSON with fields: {{fields}}",
                    Variables =
                    [
                        new PromptVariable
                        {
                            Name = "text",
                            Type = "string",
                            Required = true,
                            Description = "Input text to extract from"
                        },
                        new PromptVariable
                        {
                            Name = "fields",
                            Type = "string",
                            Required = true,
                            Description = "Comma-separated list of fields",
                            DefaultValue = "name, date, location"
                        }
                    ],
                    FewShotExamples =
                    [
                        new FewShotExample
                        {
                            Input = "John Smith visited Paris on March 5, 2024 for the AI Summit.",
                            Output = "{\"name\":\"John Smith\",\"date\":\"2024-03-05\",\"location\":\"Paris\"}",
                            Label = "basic extraction"
                        }
                    ],
                    Notes = "Added system prompt and few-shot example for improved accuracy"
                }
            ]
        };
    }

    /// <summary>
    /// Creates the "Code Review Assistant" template with one version.
    /// Includes a system prompt, language and code variables.
    /// </summary>
    /// <returns>A prompt template entity with one version.</returns>
    private static PromptTemplate CreateCodeReviewAssistantTemplate()
    {
        Guid templateId = Guid.NewGuid();
        Guid v1Id = Guid.NewGuid();

        return new PromptTemplate
        {
            Id = templateId,
            Name = "Code Review Assistant",
            Category = "development",
            Description = "Reviews code for bugs, style issues, and potential improvements with language-aware analysis.",
            Tags = ["code", "review", "quality"],
            LatestVersion = 1,
            Versions =
            [
                new PromptVersion
                {
                    Id = v1Id,
                    TemplateId = templateId,
                    Version = 1,
                    SystemPrompt = "You are a senior software engineer reviewing code.",
                    UserTemplate = "Review this {{language}} code for bugs, style issues, and potential improvements:\n\n```{{language}}\n{{code}}\n```",
                    Variables =
                    [
                        new PromptVariable
                        {
                            Name = "language",
                            Type = "string",
                            Required = true,
                            DefaultValue = "python"
                        },
                        new PromptVariable
                        {
                            Name = "code",
                            Type = "string",
                            Required = true,
                            Description = "Code to review"
                        }
                    ]
                }
            ]
        };
    }
}
