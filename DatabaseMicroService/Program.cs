using ApplicationLayer.Interfaces;
using ApplicationLayer.Services;
using Azure.Identity;
using Domain.Interfaces;
using Infrastructure.Health;
using Infrastructure.Data;
using Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;


public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Register services needed for application setup
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddHttpClient();
        builder.Services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = builder.Configuration["ApplicationInsights:InstrumentationKey"];
        });


        builder.Services.AddSingleton<IKeyVaultSecretManager, KeyVaultSecretManager>();

        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

        });

        builder.Configuration
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<Program>(); // This adds user secrets


        var keyVaultManager = builder.Services.BuildServiceProvider().GetRequiredService<IKeyVaultSecretManager>();
        var vaultSecret = await keyVaultManager.GetSecretAsync();
        var dbConnectionString = vaultSecret.DbConnectionString;
        // Register the DbContext with the connection string fetched from Key Vault
        builder.Services.AddDbContext<ElectricityDbContext>(options =>
            options.UseSqlServer(dbConnectionString));

        // Add remaining service registrations
        builder.Services.AddScoped<IElectrictyService, ElectrictyService>();
        builder.Services.AddScoped<IElectricityRepository, ElectricityRepository>();
        builder.Services.AddScoped<ISaveHistoryDataService, SaveHistoryDataService>();
        builder.Services.AddScoped<IDateRangeDataService, DateRangeDataService>();
        builder.Services.AddScoped<ICalculateFingridConsumptionPrice, CalculateFinGridConsumptionPriceService>();
        builder.Services.AddHostedService<ElectricityPriceFetchingBackgroundService>();
        builder.Services.AddScoped<IElectricityPriceService, ElectricityPriceService>();
        builder.Services.AddMemoryCache();

        // Health checks setup
        builder.Services.AddHttpClient();
        builder.Services.AddHealthChecks()
            .AddCheck<ElectricityServiceHealthCheck>("ElectricityServiceHealthCheck");

        // Build the app
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets<Program>();
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables();

            var keyVaultUrl = builder.Configuration["KeyVault:BaseUrl"];
            builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential());
        }


        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Application starting up");

        app.Lifetime.ApplicationStarted.Register(() => logger.LogInformation("Application started"));
        app.Lifetime.ApplicationStopping.Register(() => logger.LogInformation("Application stopping"));
        app.Lifetime.ApplicationStopped.Register(() => logger.LogInformation("Application stopped"));

        app.UseHttpsRedirection();
     
        app.MapControllers();
        app.Run();
    }
}