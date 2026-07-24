using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TimeSeries.IntegrationTests;

public sealed class FilesEndpointsTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient client;

    public FilesEndpointsTests(IntegrationTestFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task UploadEndpoint_ValidFile_ReturnsCreatedResponse()
    {
        var fileName = UniqueFileName("upload");
        var response = await UploadAsync(fileName);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);

        Assert.NotEqual(Guid.Empty, body.GetProperty("fileId").GetGuid());
        Assert.False(body.GetProperty("alreadyProcessed").GetBoolean());
    }

    [Fact]
    public async Task UploadEndpoint_SameKeyAndSameContent_ReturnsAlreadyProcessed()
    {
        var fileName = UniqueFileName("idempotent");
        var key = Guid.NewGuid().ToString();

        var firstResponse = await UploadAsync(fileName, key);
        var firstBody = await ReadJsonAsync(firstResponse);
        var secondResponse = await UploadAsync(fileName, key);
        var secondBody = await ReadJsonAsync(secondResponse);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstBody.GetProperty("fileId").GetGuid(),
            secondBody.GetProperty("fileId").GetGuid());
        Assert.True(secondBody.GetProperty("alreadyProcessed").GetBoolean());
    }

    [Fact]
    public async Task UploadEndpoint_SameKeyAndDifferentContent_ReturnsConflict()
    {
        var fileName = UniqueFileName("conflict");
        var key = Guid.NewGuid().ToString();

        using var firstRequest = CreateUploadRequest(fileName, key, CsvContent(10));
        await client.PostAsync("/files/upload", firstRequest);

        using var secondRequest = CreateUploadRequest(fileName, key, CsvContent(99));
        var response = await client.PostAsync("/files/upload", secondRequest);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("Конфликт", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task UploadEndpoint_MissingIdempotencyKey_ReturnsBadRequest()
    {
        using var request = CreateUploadRequest(UniqueFileName("missing-key"), null, CsvContent(10));
        var response = await client.PostAsync("/files/upload", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadEndpoint_InvalidFileExtension_ReturnsBadRequest()
    {
        using var request = CreateUploadRequest("invalid-extension.txt", Guid.NewGuid().ToString(), CsvContent(10));
        var response = await client.PostAsync("/files/upload", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadEndpoint_EmptyFile_ReturnsBadRequest()
    {
        using var request = CreateUploadRequest(UniqueFileName("empty"), Guid.NewGuid().ToString(), string.Empty);
        var response = await client.PostAsync("/files/upload", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadEndpoint_InvalidCsv_ReturnsStructuredValidationError()
    {
        var content = "Date;ExecutionTime;Value\n2024-01-01T10:00:00Z;not-a-number;10";
        using var request = CreateUploadRequest(UniqueFileName("invalid-csv"), Guid.NewGuid().ToString(), content);
        var response = await client.PostAsync("/files/upload", request);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, body.GetProperty("status").GetInt32());
        Assert.Equal("Некорректное время выполнения", body.GetProperty("reason").GetString());
        Assert.Equal(2, body.GetProperty("lineNumber").GetInt32());
    }

    [Fact]
    public async Task UploadEndpoint_SameFileNameWithNewContent_ReplacesPreviousFile()
    {
        var fileName = UniqueFileName("replacement");

        await UploadAsync(fileName, Guid.NewGuid().ToString(), CsvContent(10));
        await UploadAsync(fileName, Guid.NewGuid().ToString(), CsvContent(100));

        var response = await client.GetAsync($"/files/results?fileName={Uri.EscapeDataString(fileName)}");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(50, body.GetProperty("items")[0].GetProperty("averageValue").GetDouble());
    }

    [Fact]
    public async Task ResultsEndpoint_ValidRequest_ReturnsProcessingResult()
    {
        var fileName = UniqueFileName("results");
        await UploadAsync(fileName);

        var response = await client.GetAsync($"/files/results?fileName={Uri.EscapeDataString(fileName)}");
        var body = await ReadJsonAsync(response);
        var item = body.GetProperty("items")[0];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(fileName, item.GetProperty("fileName").GetString());
        Assert.Equal(20, item.GetProperty("averageValue").GetDouble());
        Assert.Equal(3, item.GetProperty("averageExecutionTime").GetDouble());
        Assert.Equal(600, item.GetProperty("dateDeltaSeconds").GetDouble());
    }

    [Fact]
    public async Task ResultsEndpoint_OffsetAndLimit_ReturnPagedResultWithTotalCount()
    {
        var prefix = $"page-{Guid.NewGuid():N}";
        await UploadAsync($"{prefix}-one.csv");
        await UploadAsync($"{prefix}-two.csv");

        var response = await client.GetAsync($"/files/results?fileName={prefix}&offset=1&limit=1");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.Equal(1, body.GetProperty("offset").GetInt32());
        Assert.Equal(1, body.GetProperty("limit").GetInt32());
        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ResultsEndpoint_FileNameFilter_IsCaseInsensitivePartialMatch()
    {
        var token = Guid.NewGuid().ToString("N");
        var fileName = $"CaseSensitive-{token}.csv";
        await UploadAsync(fileName);

        var response = await client.GetAsync(
            $"/files/results?fileName={Uri.EscapeDataString(token[..12].ToLowerInvariant())}");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task ResultsEndpoint_AllRangeFilters_ReturnMatchingResult()
    {
        var fileName = UniqueFileName("filters");
        await UploadAsync(fileName);

        var response = await client.GetAsync(
            $"/files/results?fileName={Uri.EscapeDataString(fileName)}" +
            "&firstOperationDateFrom=2024-01-01T09:00:00Z" +
            "&firstOperationDateTo=2024-01-01T11:00:00Z" +
            "&averageValueFrom=19&averageValueTo=21" +
            "&averageExecutionTimeFrom=2&averageExecutionTimeTo=4");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("averageValueFrom=21&averageValueTo=20")]
    [InlineData("averageExecutionTimeFrom=4&averageExecutionTimeTo=2")]
    [InlineData("firstOperationDateFrom=2024-01-02T00:00:00Z&firstOperationDateTo=2024-01-01T00:00:00Z")]
    public async Task ResultsEndpoint_InvalidRange_ReturnsBadRequest(string query)
    {
        var response = await client.GetAsync($"/files/results?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResultsEndpoint_NoMatches_ReturnsEmptyItems()
    {
        var response = await client.GetAsync(
            $"/files/results?fileName={Uri.EscapeDataString(UniqueFileName("does-not-exist"))}");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task LatestEndpoint_ValidFile_ReturnsTenNewestValuesInDescendingOrder()
    {
        var fileName = UniqueFileName("latest");
        await UploadAsync(fileName, Guid.NewGuid().ToString(), CsvContentWithRows(12));

        var response = await client.GetAsync($"/files/latest/{Uri.EscapeDataString(fileName)}");
        var body = await ReadJsonAsync(response);
        var items = body.GetProperty("items");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(10, items.GetArrayLength());
        Assert.Equal("2024-01-01T10:11:00+00:00", items[0].GetProperty("date").GetString());
        Assert.Equal("2024-01-01T10:02:00+00:00", items[9].GetProperty("date").GetString());
    }

    [Fact]
    public async Task LatestEndpoint_UnknownFile_ReturnsEmptyItems()
    {
        var response = await client.GetAsync($"/files/latest/{Uri.EscapeDataString(UniqueFileName("unknown"))}");
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task LatestEndpoint_WhitespaceFileName_ReturnsBadRequest()
    {
        var response = await client.GetAsync("/files/latest/%20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpResponseMessage> UploadAsync(
        string fileName,
        string? idempotencyKey = null,
        string? csv = null)
    {
        using var request = CreateUploadRequest(
            fileName,
            idempotencyKey ?? Guid.NewGuid().ToString(),
            csv ?? CsvContent());

        return await client.PostAsync("/files/upload", request);
    }

    private static MultipartFormDataContent CreateUploadRequest(
        string fileName,
        string? idempotencyKey,
        string csv)
    {
        var request = new MultipartFormDataContent();
        var fileContent = new StringContent(csv, Encoding.UTF8);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        request.Add(fileContent, "file", fileName);

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        return request;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(content), $"Ответ пустой. HTTP {(int)response.StatusCode}.");
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private static string UniqueFileName(string prefix) =>
        $"{prefix}-{Guid.NewGuid():N}.csv";

    private static string CsvContent(double value = 10) =>
        $"Date;ExecutionTime;Value\n" +
        $"2024-01-01T10:00:00.0000Z;1;{value}\n" +
        "2024-01-01T10:05:00.0000Z;3;20\n" +
        "2024-01-01T10:10:00.0000Z;5;30\n";

    private static string CsvContentWithRows(int count)
    {
        var builder = new StringBuilder("Date;ExecutionTime;Value\n");

        for (var index = 0; index < count; index++)
        {
            builder.AppendLine(
                $"2024-01-01T10:{index:00}:00Z;1;{(index + 1).ToString(CultureInfo.InvariantCulture)}");
        }

        return builder.ToString();
    }
}
