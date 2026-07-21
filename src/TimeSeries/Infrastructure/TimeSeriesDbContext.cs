using Microsoft.EntityFrameworkCore;

namespace TimeSeries.Infrastructure;

public class TimeSeriesDbContext : DbContext
{
    public TimeSeriesDbContext(DbContextOptions<TimeSeriesDbContext> options) 
        : base(options)
    {
        
    }
}
