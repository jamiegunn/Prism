using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Prism.Common.Database.Seeders;
using Prism.Features.Playground.Application.DeleteConversation;
using Prism.Features.Playground.Application.ExportConversation;
using Prism.Features.Playground.Application.GetConversation;
using Prism.Features.Playground.Application.ListConversations;
using Prism.Features.Playground.Application.StreamChat;
using Prism.Features.Playground.Infrastructure;

namespace Prism.Features.Playground;

/// <summary>
/// Dependency injection module for the Playground feature.
/// Registers all handlers and validators.
/// </summary>
public static class PlaygroundModule
{
    /// <summary>
    /// Registers all Playground feature services in the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPlaygroundFeature(this IServiceCollection services)
    {
        // Handlers
        services.AddScoped<StreamChatHandler>();
        services.AddScoped<GetConversationHandler>();
        services.AddScoped<ListConversationsHandler>();
        services.AddScoped<DeleteConversationHandler>();
        services.AddScoped<ExportConversationHandler>();

        // Validators
        services.AddScoped<IValidator<StreamChatCommand>, StreamChatValidator>();

        // Seeders
        services.AddScoped<IDataSeeder, PlaygroundSeeder>();

        return services;
    }
}
