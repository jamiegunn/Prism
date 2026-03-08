using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Evaluation.Application.CancelEvaluation;
using Prism.Features.Evaluation.Application.ExportResults;
using Prism.Features.Evaluation.Application.GetEvaluation;
using Prism.Features.Evaluation.Application.GetEvaluationResults;
using Prism.Features.Evaluation.Application.GetLeaderboard;
using Prism.Features.Evaluation.Application.GetResultRecords;
using Prism.Features.Evaluation.Application.ListEvaluations;
using Prism.Features.Evaluation.Application.StartEvaluation;
using Prism.Features.Evaluation.Domain;
using Prism.Features.Evaluation.Domain.Scorers;

namespace Prism.Features.Evaluation;

/// <summary>
/// Dependency injection module for the Evaluation feature.
/// Registers all handlers and scoring methods.
/// </summary>
public static class EvaluationModule
{
    /// <summary>
    /// Registers all Evaluation feature services in the dependency injection container.
    /// </summary>
    public static IServiceCollection AddEvaluationFeature(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<StartEvaluationHandler>();
        services.AddScoped<ListEvaluationsHandler>();
        services.AddScoped<GetEvaluationHandler>();
        services.AddScoped<CancelEvaluationHandler>();
        services.AddScoped<GetEvaluationResultsHandler>();
        services.AddScoped<GetResultRecordsHandler>();
        services.AddScoped<ExportResultsHandler>();
        services.AddScoped<GetLeaderboardHandler>();

        // Scoring methods
        services.AddSingleton<IScoringMethod, ExactMatchScorer>();
        services.AddSingleton<IScoringMethod, ContainsScorer>();
        services.AddSingleton<IScoringMethod, RougeLScorer>();
        services.AddSingleton<IScoringMethod, BleuScorer>();
        services.AddSingleton<IScoringMethod, LengthRatioScorer>();

        return services;
    }
}
