using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Caching.Distributed;
using TimeSeries.Infrastructure;

namespace TimeSeries.IntegrationTests;

public sealed class IntegrationTestFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"TimeSeriesTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeSeriesDbContext>();
            services.RemoveAll<DbContextOptions<TimeSeriesDbContext>>();
            services.RemoveAll<DbContextOptions>();
            services.RemoveAll<IDbContextOptionsConfiguration<TimeSeriesDbContext>>();

            services.AddDbContext<TimeSeriesDbContext>(options =>
            {
                options.UseInMemoryDatabase(databaseName)
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });

            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
        });
    }
}
