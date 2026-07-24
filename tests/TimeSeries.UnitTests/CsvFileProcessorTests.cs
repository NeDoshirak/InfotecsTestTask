using System.Text;
using Microsoft.AspNetCore.Http;
using TimeSeries.Application.Exceptions;
using TimeSeries.Features.Files;

namespace TimeSeries.UnitTests;

public sealed class CsvFileProcessorTests
{
    private readonly CsvFileProcessor processor = new();

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_ValidCsv_ReturnsRowsAndStatistics()
    {
        var file = CreateFile("valid.csv", """
            Date;ExecutionTime;Value
            2024-01-01T10:00:00.0000Z;1;10
            2024-01-01T10:05:00.0000Z;3;20
            2024-01-01T10:10:00.0000Z;5;30
            """);

        var result = await processor.ProcessAsync(file, CancellationToken.None);

        Assert.Equal(3, result.Values.Count);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero),
            result.Statistics.FirstOperationDate);
        Assert.Equal(600, result.Statistics.DateDeltaSeconds);
        Assert.Equal(3, result.Statistics.AverageExecutionTime);
        Assert.Equal(20, result.Statistics.AverageValue);
        Assert.Equal(20, result.Statistics.MedianValue);
        Assert.Equal(30, result.Statistics.MaxValue);
        Assert.Equal(10, result.Statistics.MinValue);
    }

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_EvenNumberOfValues_ReturnsAverageMedian()
    {
        var file = CreateFile("even.csv", """
            Date;ExecutionTime;Value
            2024-01-01T10:00:00Z;1;10
            2024-01-01T10:01:00Z;2;20
            2024-01-01T10:02:00Z;3;30
            2024-01-01T10:03:00Z;4;40
            """);

        var result = await processor.ProcessAsync(file, CancellationToken.None);

        Assert.Equal(25, result.Statistics.MedianValue);
        Assert.Equal(25, result.Statistics.AverageValue);
    }

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_InvalidHeader_ThrowsValidationException()
    {
        var file = CreateFile("invalid.csv", """
            Wrong;ExecutionTime;Value
            2024-01-01T10:00:00Z;1;10
            """);

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Некорректный заголовок CSV", exception.Reason);
        Assert.Contains("Date;ExecutionTime;Value", exception.Details);
    }

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_EmptyData_ThrowsValidationException()
    {
        var file = CreateFile("empty.csv", "Date;ExecutionTime;Value\n");

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Файл не содержит строк данных", exception.Reason);
        Assert.Null(exception.LineNumber);
    }

    [Theory]
    [InlineData("\n", "Файл содержит пустую строку.", 2)]
    [InlineData("2024-01-01T10:00:00Z;1", "Некорректный формат строки.", 2)]
    [InlineData("2024-01-01T10:00:00Z;1;10;extra", "Некорректный формат строки.", 2)]
    [InlineData("not-a-date;1;10", "Некорректная дата", 2)]
    [InlineData("1999-12-31T23:59:59Z;1;10", "Некорректная дата", 2)]
    [InlineData("2024-01-01T10:00:00Z;not-a-number;10", "Некорректное время выполнения", 2)]
    [InlineData("2024-01-01T10:00:00Z;-1;10", "Некорректное время выполнения", 2)]
    [InlineData("2024-01-01T10:00:00Z;1;not-a-number", "Некорректные показатели прибора.", 2)]
    [InlineData("2024-01-01T10:00:00Z;1;-10", "Некорректное значение показателя", 2)]
    public async Task CsvFileProcessor_ProcessAsync_InvalidRow_ThrowsValidationException(
        string row,
        string expectedReason,
        int expectedLineNumber)
    {
        var file = CreateFile(
            "invalid.csv",
            $"Date;ExecutionTime;Value\n{row}");

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal(expectedReason, exception.Reason);
        Assert.Equal(expectedLineNumber, exception.LineNumber);
    }

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_FutureDate_ThrowsValidationException()
    {
        var futureDate = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O");
        var file = CreateFile(
            "future.csv",
            $"Date;ExecutionTime;Value\n{futureDate};1;10");

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Некорректная дата", exception.Reason);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public async Task CsvFileProcessor_ProcessAsync_MoreThan10000Rows_ThrowsValidationException()
    {
        var rows = Enumerable.Repeat(
            "2024-01-01T10:00:00Z;1;10",
            10_001);
        var file = CreateFile(
            "too-many-rows.csv",
            "Date;ExecutionTime;Value\n" + string.Join('\n', rows));

        var exception = await Assert.ThrowsAsync<CsvValidationException>(() =>
            processor.ProcessAsync(file, CancellationToken.None));

        Assert.Equal("Некорректное количество строк", exception.Reason);
        Assert.Equal(10_002, exception.LineNumber);
    }

    private static IFormFile CreateFile(string fileName, string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}
