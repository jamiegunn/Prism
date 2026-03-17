using Microsoft.Extensions.DependencyInjection;
using Prism.Features.Agents.Application;
using Prism.Features.Agents.Application.CreateWorkflow;
using Prism.Features.Agents.Application.DeleteWorkflow;
using Prism.Features.Agents.Application.GetRun;
using Prism.Features.Agents.Application.GetWorkflow;
using Prism.Features.Agents.Application.ListRuns;
using Prism.Features.Agents.Application.ListTools;
using Prism.Features.Agents.Application.ListWorkflows;
using Prism.Features.Agents.Application.RunAgent;
using Prism.Features.Agents.Application.UpdateWorkflow;
using Prism.Common.Database.Seeders;
using Prism.Features.Agents.Domain.Tools;
using Prism.Features.Agents.Infrastructure;

namespace Prism.Features.Agents;

/// <summary>
/// Registers all Agent Builder services in the dependency injection container.
/// </summary>
public static class AgentsModule
{
    /// <summary>
    /// Adds Agent feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentsFeature(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<CreateWorkflowHandler>();
        services.AddScoped<ListWorkflowsHandler>();
        services.AddScoped<GetWorkflowHandler>();
        services.AddScoped<UpdateWorkflowHandler>();
        services.AddScoped<DeleteWorkflowHandler>();
        services.AddScoped<RunAgentHandler>();
        services.AddScoped<GetRunHandler>();
        services.AddScoped<ListRunsHandler>();
        services.AddScoped<ListToolsHandler>();

        // ReAct executor
        services.AddScoped<ReActExecutor>();

        // Tool registry (singleton — tools are stateless)
        services.AddSingleton<AgentToolRegistry>(sp =>
        {
            var registry = new AgentToolRegistry();

            // Register built-in tools
            registry.Register(new CalculatorTool());
            registry.Register(new EchoTool());
            registry.Register(new ApiCallTool(sp.GetRequiredService<IHttpClientFactory>()));
            registry.Register(new RagQueryTool(sp));

            return registry;
        });

        // Seeders
        services.AddScoped<IDataSeeder, AgentsSeeder>();

        return services;
    }
}
