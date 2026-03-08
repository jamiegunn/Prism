using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prism.Features.PromptLab.Application.CreateTemplate;
using Prism.Features.PromptLab.Application.CreateVersion;
using Prism.Features.PromptLab.Application.DeleteTemplate;
using Prism.Features.PromptLab.Application.DiffVersions;
using Prism.Features.PromptLab.Application.GetTemplate;
using Prism.Features.PromptLab.Application.GetVersion;
using Prism.Features.PromptLab.Application.ListTemplates;
using Prism.Features.PromptLab.Application.AbTest;
using Prism.Features.PromptLab.Application.ListVersions;
using Prism.Features.PromptLab.Application.Rendering;
using Prism.Features.PromptLab.Application.TestPrompt;
using Prism.Features.PromptLab.Application.UpdateTemplate;

namespace Prism.Features.PromptLab;

/// <summary>
/// Dependency injection module for the Prompt Lab feature.
/// Registers all handlers and validators.
/// </summary>
public static class PromptLabModule
{
    /// <summary>
    /// Registers all Prompt Lab feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPromptLabFeature(this IServiceCollection services)
    {
        // Template handlers
        services.AddScoped<CreateTemplateHandler>();
        services.AddScoped<ListTemplatesHandler>();
        services.AddScoped<GetTemplateHandler>();
        services.AddScoped<UpdateTemplateHandler>();
        services.AddScoped<DeleteTemplateHandler>();

        // Version handlers
        services.AddScoped<CreateVersionHandler>();
        services.AddScoped<ListVersionsHandler>();
        services.AddScoped<GetVersionHandler>();
        services.AddScoped<DiffVersionsHandler>();

        // Engine
        services.AddScoped<TemplateRenderer>();
        services.AddScoped<TestPromptHandler>();
        services.AddScoped<AbTestHandler>();

        // Validators
        services.AddScoped<IValidator<CreateTemplateCommand>, CreateTemplateValidator>();

        return services;
    }
}
