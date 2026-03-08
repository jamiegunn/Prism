using Microsoft.Extensions.DependencyInjection;
using Prism.Features.StructuredOutput.Application.CreateSchema;
using Prism.Features.StructuredOutput.Application.DeleteSchema;
using Prism.Features.StructuredOutput.Application.GetSchema;
using Prism.Features.StructuredOutput.Application.ListSchemas;
using Prism.Features.StructuredOutput.Application.StructuredInference;

namespace Prism.Features.StructuredOutput;

/// <summary>
/// Registers all Structured Output services in the dependency injection container.
/// </summary>
public static class StructuredOutputModule
{
    /// <summary>
    /// Adds Structured Output feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddStructuredOutputFeature(this IServiceCollection services)
    {
        services.AddScoped<CreateSchemaHandler>();
        services.AddScoped<ListSchemasHandler>();
        services.AddScoped<GetSchemaHandler>();
        services.AddScoped<DeleteSchemaHandler>();
        services.AddScoped<StructuredInferenceHandler>();

        return services;
    }
}
