using System.Text;
using TimeSeries.Application.Exceptions;

namespace TimeSeries.Features.Files;

public class CsvFileProcessor : ICsvFileProcessor
{
    private const string ExpectedHeader = "Date;ExecutionTime;Value";
    
    public async Task<ProcessedCsvFile> ProcessAsync(IFormFile file, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        
        if (await reader.ReadLineAsync(cancellationToken) != ExpectedHeader)
        {
            throw new CsvValidationException(
                reason: "Некорректный заголовок CSV",
                details: $"Ожидался: {ExpectedHeader}");
        }
        
        List<ProcessedCsvRow> processedCsvRows = new List<ProcessedCsvRow>();
        string? line;
        int сurrentLineNumber = 1;
 
        while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
        {
            сurrentLineNumber++;

            if (processedCsvRows.Count >= 10_000)
            {
                throw new CsvValidationException(
                    reason: "Некорректное количество строк",
                    details: "CSV-файл не может содержать больше 10000 строк данных.",
                    lineNumber: сurrentLineNumber);
            }
            
            var row = ParseRow(line, сurrentLineNumber);
            
            processedCsvRows.Add(row);
        }

        if (processedCsvRows.Count == 0)
        {
            throw new CsvValidationException(
                reason: "Файл не содержит строк данных",
                details: "CSV-файл должен содержать от 1 до 10000 строк данных.");
        }
        
        var statistic = CalculateStatistics(processedCsvRows);

        return new ProcessedCsvFile(processedCsvRows, statistic);
    }
    
    private ProcessedCsvRow ParseRow(string line, int lineNumber)
    {
        if (String.IsNullOrWhiteSpace(line))
        {
            throw new CsvValidationException(
                reason: "Файл содержит пустую строку.",
                lineNumber: lineNumber);
        }
        
        var columns = line.Split(';');

        if (columns.Length != 3)
        {
            throw new CsvValidationException(
                reason: "Некорректный формат строки.",
                lineNumber: lineNumber);
        }
        
        if (!DateTimeOffset.TryParse(columns[0], out var date))
        {
            throw new CsvValidationException(
                "Некорректная дата",
                $"Получено значение: '{columns[0]}'.",
                lineNumber);
        }
        
        if (!double.TryParse(columns[1], out var executionTime))
        {
            throw new CsvValidationException(
                "Некорректное время выполнения",
                $"Получено значение: '{columns[1]}'.",
                lineNumber);
        }
        
        if (!double.TryParse(columns[2], out var indicatorValue))
        {
            throw new CsvValidationException(
                "Некорректные показатели прибора.",
                $"Получено значение: '{columns[2]}'.",
                lineNumber);
        }

        var result = new ProcessedCsvRow(date, executionTime, indicatorValue);
        ValidateRow(result, lineNumber);

        return result;
    }

    private static void ValidateRow(ProcessedCsvRow row, int lineNumber)
    {
        var minDate = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        if (row.Date < minDate || row.Date > DateTimeOffset.UtcNow)
        {
            throw new CsvValidationException(
                reason: "Некорректная дата",
                details: "Дата должна быть между 01.01.2000 и текущим моментом.",
                lineNumber: lineNumber);
        }

        if (!double.IsFinite(row.ExecutionTime) || row.ExecutionTime < 0)
        {
            throw new CsvValidationException(
                reason: "Некорректное время выполнения",
                details: "ExecutionTime должно быть конечным числом и не может быть отрицательным.",
                lineNumber: lineNumber);
        }

        if (!double.IsFinite(row.IndicatorValue) ||
            row.IndicatorValue < 0)
        {
            throw new CsvValidationException(
                reason: "Некорректное значение показателя",
                details: "Value должно быть конечным числом и не может быть отрицательным.",
                lineNumber: lineNumber);
        }
    }
    
    private CsvStatistics CalculateStatistics(List<ProcessedCsvRow> rows)
    {
        var dates = rows.Select(x => x.Date).ToList();
        var executionTimes = rows.Select(x => x.ExecutionTime).ToList();
        var values = rows
            .Select(x => x.IndicatorValue)
            .OrderBy(x => x)
            .ToList();

        var dateDeltaSeconds =
            (dates.Max() - dates.Min()).TotalSeconds;

        var medianValue = values.Count % 2 == 1
            ? values[values.Count / 2]
            : (
                values[values.Count / 2 - 1] +
                values[values.Count / 2]
            ) / 2;

        return new CsvStatistics(
            DateDeltaSeconds: dateDeltaSeconds,
            FirstOperationDate: dates.Min(),
            AverageExecutionTime: executionTimes.Average(),
            AverageValue: values.Average(),
            MedianValue: medianValue,
            MaxValue: values.Max(),
            MinValue: values.Min());
    }
}

public record ProcessedCsvFile(
    IReadOnlyCollection<ProcessedCsvRow> Values,
    CsvStatistics Statistics);
    
public record ProcessedCsvRow(
    DateTimeOffset Date,
    double ExecutionTime,
    double IndicatorValue);
    
public record CsvStatistics(
    double DateDeltaSeconds,
    DateTimeOffset FirstOperationDate,
    double AverageExecutionTime,
    double AverageValue,
    double MedianValue,
    double MaxValue,
    double MinValue);
