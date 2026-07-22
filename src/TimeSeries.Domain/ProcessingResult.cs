namespace TimeSeries.Domain;

public class ProcessingResult
{
    public Guid Id { get; init; }
    
    public Guid UploadedFileId { get; set; }
    
    public double DateDeltaSeconds { get; set; }
    
    public DateTimeOffset FirstOperationDate  { get; set; }
    
    public double AverageExecutionTime  { get; set; }
    
    public double AverageValue  { get; set; }
    
    public double MedianValue  { get; set; }

    public double MaxValue  { get; set; }

    public double MinValue  { get; set; }

    public UploadedFile UploadedFile { get; set; } = null!;
}