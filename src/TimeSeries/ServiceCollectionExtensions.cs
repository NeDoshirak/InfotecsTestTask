using TimeSeries.Infrastructure;

namespace TimeSeries;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection ConfigureTimeSeries(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTimeSeriesDatabase(configuration);
        return services;
    }
}
