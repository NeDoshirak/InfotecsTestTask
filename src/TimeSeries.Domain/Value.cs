namespace TimeSeries.Domain;

public class Value
{
    public Guid Id { get; init; }
    
    public Guid UploadedFileId { get; set; }
    
    public DateTimeOffset Date {get; set; }
    
    public double ExecutionTime { get; set; }
    
    public double IndicatorValue  { get; set; }

    public UploadedFile UploadedFile { get; set; } = null!;
}