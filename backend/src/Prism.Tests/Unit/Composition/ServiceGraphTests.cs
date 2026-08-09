using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.Api.Extensions;

namespace Prism.Tests.Unit.Composition;

/// <summary>
/// Validates the dependency injection graph the API actually composes.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core turns on <c>ValidateOnBuild</c> and <c>ValidateScopes</c> in the Development
/// environment and leaves them off everywhere else. That difference hid a real defect for as
/// long as nobody started the API in Development: <c>IEmbeddingProvider</c> was registered as a
/// singleton while taking <c>AppDbContext</c>, which is scoped.
/// </para>
/// <para>
/// A captive dependency like that is not a style problem. The context is resolved once and
/// then held forever, so it is never disposed, its change tracker grows without bound, it hands
/// every request the same instance, and <c>DbContext</c> is explicitly not thread-safe. The
/// symptom is data corruption under concurrency, a long way from the registration that caused
/// it.
/// </para>
/// <para>
/// The gap was widened by <c>dev.sh</c> passing <c>--no-launch-profile</c>, which drops the
/// environment to Production. The API started, validation never ran, and migrations — also
/// gated on Development — silently did not run either. The first visible symptom was
/// <c>relation "jobs" does not exist</c> at runtime.
/// </para>
/// <para>
/// This test applies the Development-mode checks unconditionally, so the graph is verified on
/// every run regardless of which environment anyone happens to launch in.
/// </para>
/// </remarks>
public sealed class ServiceGraphTests
{
    /// <summary>
    /// Every registration must be constructible, and no singleton may capture a scoped service.
    /// </summary>
    [Fact]
    public void The_Container_Builds_And_No_Singleton_Captures_A_Scoped_Service()
    {
        ServiceCollection services = BuildRealServiceCollection();

        // Exactly what the Development environment does. ValidateOnBuild walks every
        // registration up front rather than failing at the first request that needs it.
        var options = new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        };

        ServiceProvider provider = services.BuildServiceProvider(options);
        provider.Dispose();
    }

    /// <summary>
    /// Resolving every registered service from a scope must succeed.
    /// </summary>
    /// <remarks>
    /// <c>ValidateOnBuild</c> checks constructor call sites but skips open generics and
    /// factory-registered services. Actually resolving each descriptor covers what it cannot,
    /// which is where a bad factory lambda would otherwise survive to runtime.
    /// </remarks>
    [Fact]
    public void Every_Registered_Service_Resolves_From_A_Scope()
    {
        ServiceCollection services = BuildRealServiceCollection();

        using ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        using IServiceScope scope = provider.CreateScope();

        var failures = new List<string>();

        foreach (ServiceDescriptor descriptor in services)
        {
            // Open generics cannot be resolved without type arguments, and hosted services are
            // started by the host rather than resolved here.
            if (descriptor.ServiceType.IsGenericTypeDefinition)
            {
                continue;
            }

            try
            {
                scope.ServiceProvider.GetService(descriptor.ServiceType);
            }
            catch (Exception ex)
            {
                failures.Add($"{descriptor.ServiceType.Name} ({descriptor.Lifetime}): {ex.Message}");
            }
        }

        Assert.True(
            failures.Count == 0,
            "These registrations could not be resolved:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>
    /// Builds the same service collection <c>Program.cs</c> builds.
    /// </summary>
    /// <returns>A populated collection.</returns>
    /// <remarks>
    /// Calls the same two extension methods the API's composition root calls, so a feature
    /// registered in the application is covered here without anyone remembering to add it.
    /// </remarks>
    private static ServiceCollection BuildRealServiceCollection()
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // A syntactically valid connection string. Nothing here connects; registration
                // and validation do not open a socket.
                ["Database:ConnectionString"] =
                    "Host=localhost;Port=5438;Database=prism_graph_check;Username=postgres;Password=postgres",
            })
            .Build();

        var services = new ServiceCollection();

        // Things the framework normally supplies, which the feature modules depend on.
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddHttpClient();

        services.AddCommonServices(configuration);
        services.AddFeatureServices(configuration);

        return services;
    }
}
