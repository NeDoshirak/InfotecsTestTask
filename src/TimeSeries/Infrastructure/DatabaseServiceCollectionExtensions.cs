using Microsoft.EntityFrameworkCore;

namespace TimeSeries.Infrastructure;

public static class DatabaseServiceCollectionExtensions
{
    public static IServiceCollection AddTimeSeriesDatabase(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        
        var connectionString =
            configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' not found.");

        services.AddDbContext<TimeSeriesDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });
        
        return services;
    }
}