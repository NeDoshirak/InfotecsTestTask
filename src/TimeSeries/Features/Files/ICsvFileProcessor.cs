namespace TimeSeries.Features.Files;

public interface ICsvFileProcessor
{
    Task<ProcessedCsvFile> ProcessAsync(IFormFile file, CancellationToken cancellationToken);
}