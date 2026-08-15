using System.Net.Sockets;
using Npgsql;
using OpenTelemetry.Trace;
using Prism.Api;
using Prism.Api.Extensions;
using Prism.Api.Middleware;
using Prism.Common.Database;
using Prism.Common.Database.Seeders;
using Prism.Common.Telemetry;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

// `--export-openapi <path>` writes the OpenAPI document and exits. It is read before the
// host is built so that the database and seeding work below can be skipped: CI runs this on
// a runner with no PostgreSQL, and an export that needed one would not be runnable there.
string? openApiExportPath = OpenApiExport.TryGetExportPath(args);

try
{
    var builder = WebApplication.CreateBuilder(args);


    builder.Host.UseSerilog((context, config) => config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.File("logs/prism-.log", rollingInterval: RollingInterval.Day));

    // Register services
    builder.Services.AddCommonServices(builder.Configuration);
    builder.Services.AddFeatureServices(builder.Configuration);

    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddHealthChecks();

    // Tracing: the gen_ai.* inference spans (Prism.Inference) plus inbound HTTP, in the
    // standard OTel shape so Jaeger, Langfuse or Phoenix can read them. Registering the
    // source is what makes ActivitySource.StartActivity return a live span — without it the
    // trace/span ids History records would all be null. The console exporter is opt-in
    // (Prism:Telemetry:ConsoleExporter) because it is very loud.
    PrismTelemetry.CaptureContent =
        builder.Configuration.GetValue<bool>("Prism:Telemetry:CaptureContent");

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing =>
        {
            tracing
                .AddSource(PrismTelemetry.InferenceSourceName)
                .AddAspNetCoreInstrumentation();

            if (builder.Configuration.GetValue<bool>("Prism:Telemetry:ConsoleExporter"))
            {
                tracing.AddConsoleExporter();
            }
        });

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? ["http://localhost:5173"];

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    if (openApiExportPath is not null)
    {
        // After every feature has registered, so that the background workers they add are
        // removed too. See OpenApiExport.PrepareForExport.
        OpenApiExport.PrepareForExport(builder.Services);
    }

    var app = builder.Build();

    // Middleware pipeline — order matters
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseCors();

    if (app.Environment.IsDevelopment() && openApiExportPath is null)
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        try
        {
            using var scope = app.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SchemaBootstrapper.EnsureSchemaAsync(db, CancellationToken.None);

            SeedDataRunner seeder = app.Services.GetRequiredService<SeedDataRunner>();
            await seeder.SeedAsync(CancellationToken.None);
            Log.Information("Data seeding completed successfully");
        }
        catch (Exception ex) when (ex is NpgsqlException or SocketException or TimeoutException)
        {
            // The database server is unreachable. Degraded startup is acceptable in development:
            // the operator gets an actionable message and the API still serves non-database routes.
            Log.Warning(ex, "Could not reach PostgreSQL. Start it with: docker compose up -d");
        }
        catch (Exception ex)
        {
            // Anything else means the schema itself is wrong - a stale database, a bad entity
            // configuration, a seeding bug. Serving traffic against a schema that does not match
            // the model corrupts data silently, so this is fatal rather than a warning.
            Log.Fatal(ex, "Database schema is invalid. Refusing to start. This is NOT a connectivity problem");
            throw;
        }
    }

    app.MapHealthChecks("/health");
    app.MapFeatureEndpoints();

    if (openApiExportPath is not null)
    {
        int pathCount = await OpenApiExport.WriteAsync(app, openApiExportPath, CancellationToken.None);
        Log.Information("Wrote {PathCount} paths to {ExportPath}", pathCount, openApiExportPath);
        return;
    }

    Log.Information("Starting Prism API");
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>
/// Entry point marker, made visible so integration tests can boot the real application
/// through <c>WebApplicationFactory&lt;Program&gt;</c>. Without this the endpoint layer
/// (61 files, 123 routes) cannot be tested at all.
/// </summary>
public partial class Program { }
