using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Experiments.Application.ArchiveExperiment;
using Prism.Features.Experiments.Application.ArchiveProject;
using Prism.Features.Experiments.Application.CreateExperiment;
using Prism.Features.Experiments.Application.CreateProject;
using Prism.Features.Experiments.Application.GetExperiment;
using Prism.Features.Experiments.Application.GetProject;
using Prism.Features.Experiments.Application.CompareRuns;
using Prism.Features.Experiments.Application.CreateRun;
using Prism.Features.Experiments.Application.DeleteRun;
using Prism.Features.Experiments.Application.ExportRuns;
using Prism.Features.Experiments.Application.GetRun;
using Prism.Features.Experiments.Application.ListExperiments;
using Prism.Features.Experiments.Application.ListProjects;
using Prism.Common.Database.Seeders;
using Prism.Features.Experiments.Application.ListRuns;
using Prism.Features.Experiments.Application.UpdateExperiment;
using Prism.Features.Experiments.Application.UpdateProject;
using Prism.Features.Experiments.Application.RunSweep;
using Prism.Features.Experiments.Infrastructure;

namespace Prism.Features.Experiments;

/// <summary>
/// Dependency injection module for the Experiments feature.
/// Registers all handlers and validators.
/// </summary>
public static class ExperimentsModule
{
    /// <summary>
    /// Registers all Experiments feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExperimentsFeature(this IServiceCollection services)
    {
        // Project handlers
        services.AddScoped<CreateProjectHandler>();
        services.AddScoped<ListProjectsHandler>();
        services.AddScoped<GetProjectHandler>();
        services.AddScoped<UpdateProjectHandler>();
        services.AddScoped<ArchiveProjectHandler>();

        // Experiment handlers
        services.AddScoped<CreateExperimentHandler>();
        services.AddScoped<ListExperimentsHandler>();
        services.AddScoped<GetExperimentHandler>();
        services.AddScoped<UpdateExperimentHandler>();
        services.AddScoped<ArchiveExperimentHandler>();

        // Run handlers
        services.AddScoped<CreateRunHandler>();
        services.AddScoped<ListRunsHandler>();
        services.AddScoped<GetRunHandler>();
        services.AddScoped<DeleteRunHandler>();
        services.AddScoped<CompareRunsHandler>();
        services.AddScoped<ExportRunsHandler>();
        services.AddScoped<RunSweepHandler>();

        // Validators
        services.AddScoped<IValidator<CreateProjectCommand>, CreateProjectValidator>();
        services.AddScoped<IValidator<CreateExperimentCommand>, CreateExperimentValidator>();

        // Seeders
        services.AddScoped<IDataSeeder, ExperimentsSeeder>();

        return services;
    }
}
