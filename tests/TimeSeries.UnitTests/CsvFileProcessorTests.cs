using System.Text;
using Microsoft.AspNetCore.Http;
using TimeSeries.Application.Exceptions;
using TimeSeries.Features.Files;

namespace TimeSeries.UnitTests;

public sealed class CsvFileProcessorTests
{
    private readonly CsvFileProcessor processor = new();

    [Fact]
    public async Task ProcessAsync_ValidCsv_ReturnsRowsAndStatistics()
    {
        var file = CreateFile("valid.csv", """
            Date;ExecutionTime;Value
            2024-01-01T10:00:00.0000Z;1;10
            2024-01-01T10:05:00.0000Z;3;20
            2024-01-01T10:10:00.0000Z;5;30
            """);

        var result = await processor.ProcessAsync(file, CancellationToken.None);

        Assert.Equal(3, result.Values.Count);
        Assert.Equal(600, result.Statistics.DateDeltaSeconds);
        Assert.Equal(3, result.Statistics.AverageExecutionTime);
        Assert.Equal(20, result.Statistics.AverageValue);
        Assert.Equal(20, result.Statistics.MedianValue);
        Assert.Equal(30, result.Statistics.MaxValue);
        Assert.Equal(10, result.Statistics.MinValue);
    }

    [Fact]
    public async Task ProcessAsync_InvalidHeader_ThrowsValidationException()
    {
        var file = CreateFile("invalid.csv", """
            Wrong;ExecutionTime;Value
            2024-01-01T10:00:00.0000Z;1;10
            """);

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Некорректный заголовок CSV", exception.Reason);
    }

    [Fact]
    public async Task ProcessAsync_EmptyData_ThrowsValidationException()
    {
        var file = CreateFile("empty.csv", "Date;ExecutionTime;Value\n");

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Файл не содержит строк данных", exception.Reason);
    }

    [Theory]
    [InlineData("2024-01-01T10:00:00.0000Z;-1;10")]
    [InlineData("2024-01-01T10:00:00.0000Z;1;-10")]
    [InlineData("2024-01-01T10:00:00.0000Z;not-a-number;10")]
    [InlineData("not-a-date;1;10")]
    public async Task ProcessAsync_InvalidRow_ThrowsValidationException(string row)
    {
        var file = CreateFile(
            "invalid.csv",
            $"Date;ExecutionTime;Value\n{row}\n");

        await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));
    }

    private static IFormFile CreateFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}
