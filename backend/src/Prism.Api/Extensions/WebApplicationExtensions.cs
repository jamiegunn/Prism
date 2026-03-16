using Prism.Features.Experiments.Api;
using Prism.Features.History.Api;
using Prism.Features.PromptLab.Api;
using Prism.Features.Models.Api;
using Prism.Features.Playground.Api;
using Prism.Features.Analytics.Api;
using Prism.Features.BatchInference.Api;
using Prism.Features.Datasets.Api;
using Prism.Features.Evaluation.Api;
using Prism.Features.Rag.Api;
using Prism.Features.Agents.Api;
using Prism.Features.FineTuning.Api;
using Prism.Features.Notebooks.Api;
using Prism.Features.StructuredOutput.Api;
using Prism.Features.TokenExplorer.Api;

namespace Prism.Api.Extensions;

/// <summary>
/// Extension methods for configuring the HTTP request pipeline on <see cref="WebApplication"/>.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Maps all feature endpoint groups to the application's routing table.
    /// Features are added here as they are implemented.
    /// </summary>
    /// <param name="app">The web application to configure.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        // Will be filled in as features are built
        app.MapPlaygroundEndpoints();
        app.MapModelEndpoints();
        app.MapTokenExplorerEndpoints();
        app.MapHistoryEndpoints();
        app.MapExperimentEndpoints();
        app.MapPromptLabEndpoints();
        app.MapDatasetEndpoints();
        app.MapEvaluationEndpoints();
        app.MapBatchInferenceEndpoints();
        app.MapAnalyticsEndpoints();
        app.MapRagEndpoints();
        app.MapStructuredOutputEndpoints();
        app.MapAgentEndpoints();
        app.MapFineTuningEndpoints();
        app.MapNotebookEndpoints();
        return app;
    }
}
