namespace TimeSeries.Application.Exceptions;

public class CsvValidationException : Exception
{
    public int? LineNumber { get; }

    public string? Details { get; }
    
    public string Reason { get; }

    public CsvValidationException(
        string reason,
        string? details = null,
        int? lineNumber = null)
        : base(BuildMessage(reason, details, lineNumber))
    {
        Reason = reason;
        Details = details;
        LineNumber = lineNumber;
    }

    private static string BuildMessage(
        string reason,
        string? details,
        int? lineNumber)
    {
        var line = lineNumber is null
            ? string.Empty
            : $"Строка {lineNumber}: ";

        return $"{line}{reason}. {details}";
    }
}