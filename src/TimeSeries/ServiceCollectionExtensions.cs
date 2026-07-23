using TimeSeries.Features.Files;
using TimeSeries.Infrastructure;

namespace TimeSeries;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureTimeSeries(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTimeSeriesDatabase(configuration);

        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration =
                configuration.GetConnectionString("Valkey")
                ?? throw new InvalidOperationException(
                    "Connection string 'Valkey' not found.");

            options.InstanceName = "timeseries:";
        });
        
        services.AddMediatR(mediatrServiceConfiguration =>
        {
            mediatrServiceConfiguration.RegisterServicesFromAssemblyContaining<Program>();
        });

        services.AddScoped<ICsvFileProcessor, CsvFileProcessor>();
        
        return services;
    }
}
