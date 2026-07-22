namespace TimeSeries.Domain;


public class UploadedFile
{
    public Guid Id { get; init; }

    public string FileName { get; set; } = null!;

    public string NormalizedFileName { get; set; } = null!;
    
    public Guid IdempotencyKey { get; set; }
    
    public string ContentHash { get; set; } = null!;
    
    public int RowsCount { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; }
    
    public ICollection<Value> Values { get; set; } = [];
    
    public ProcessingResult ProcessingResult { get; set; } = null!;
}