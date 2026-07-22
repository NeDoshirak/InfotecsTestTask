using Microsoft.EntityFrameworkCore;
using TimeSeries.Domain;

namespace TimeSeries.Infrastructure;

public class TimeSeriesDbContext : DbContext
{
    public DbSet<UploadedFile> UploadedFiles { get; set; } 
    public DbSet<Value> Values  { get; set; }
    public DbSet<ProcessingResult> ProcessingResults { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadedFile>(entity =>
        {
            entity.HasKey(x => x.Id);
            
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            
            entity.HasIndex(x => x.NormalizedFileName).IsUnique();
            
            entity.HasMany(x => x.Values)
                .WithOne(x => x.UploadedFile)
                .HasForeignKey(x => x.UploadedFileId)
                .OnDelete(DeleteBehavior.Cascade);;
            
            entity.HasOne(x => x.ProcessingResult)
                .WithOne(x => x.UploadedFile)
                .HasForeignKey<ProcessingResult>(x => x.UploadedFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Value>(entity =>
        {
            entity.HasKey(x => x.Id);
            
            entity.HasIndex(x => new
            {
                x.UploadedFileId,
                x.Date
            });

            entity.HasOne(x => x.UploadedFile)
                .WithMany(x => x.Values)
                .HasForeignKey(x => x.UploadedFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProcessingResult>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new
            {
                x.UploadedFileId,
            }).IsUnique();


            entity.HasIndex(x => x.FirstOperationDate);

            entity.HasIndex(x => x.AverageValue);

            entity.HasIndex(x => x.AverageExecutionTime);
        });
        
        base.OnModelCreating(modelBuilder);
    }

    public TimeSeriesDbContext(DbContextOptions<TimeSeriesDbContext> options) 
        : base(options)
    {
    }
}
