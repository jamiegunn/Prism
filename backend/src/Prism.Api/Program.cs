using Prism.Api.Extensions;
using Prism.Api.Middleware;
using Prism.Common.Database;
using Prism.Common.Database.Seeders;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

    var app = builder.Build();

    // Middleware pipeline — order matters
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        try
        {
            using var scope = app.Services.CreateScope();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            Log.Information("Database migration completed successfully");

            SeedDataRunner seeder = app.Services.GetRequiredService<SeedDataRunner>();
            await seeder.SeedAsync(CancellationToken.None);
            Log.Information("Data seeding completed successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not apply database migrations. Is PostgreSQL running? Start it with: docker compose up -d");
        }
    }

    app.MapHealthChecks("/health");
    app.MapFeatureEndpoints();

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
