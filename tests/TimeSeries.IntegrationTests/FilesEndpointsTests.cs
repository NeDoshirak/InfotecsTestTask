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
    public async Task Upload_ReturnsCreatedFile()
    {
        using var request = CreateUploadRequest(
            "upload-test.csv",
            "11111111-1111-1111-1111-111111111111");

        var response = await client.PostAsync("/files/upload", request);

        var responseText = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            responseText);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("fileId").GetGuid() == Guid.Empty);
        Assert.False(body.GetProperty("alreadyProcessed").GetBoolean());
    }

    [Fact]
    public async Task Results_ReturnsPagedResults()
    {
        using var request = CreateUploadRequest(
            "results-test.csv",
            "22222222-2222-2222-2222-222222222222");

        await client.PostAsync("/files/upload", request);

        var response = await client.GetAsync(
            "/files/results?averageValueFrom=10&limit=1");

        var responseText = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            responseText);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("items").GetArrayLength());
        Assert.True(body.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Equal(1, body.GetProperty("limit").GetInt32());
    }

    [Fact]
    public async Task Latest_ReturnsTenNewestValues()
    {
        using var request = CreateUploadRequest(
            "latest-test.csv",
            "33333333-3333-3333-3333-333333333333");

        await client.PostAsync("/files/upload", request);

        var response = await client.GetAsync(
            "/files/latest/latest-test.csv");

        var responseText = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            responseText);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");

        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal(
            "2024-01-01T10:10:00+00:00",
            items[0].GetProperty("date").GetString());
    }

    private static MultipartFormDataContent CreateUploadRequest(
        string fileName,
        string idempotencyKey)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new StringContent(
            "Date;ExecutionTime;Value\n" +
            "2024-01-01T10:00:00.0000Z;1;10\n" +
            "2024-01-01T10:05:00.0000Z;3;20\n" +
            "2024-01-01T10:10:00.0000Z;5;30\n",
            Encoding.UTF8);

        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue("text/csv");

        content.Add(fileContent, "file", fileName);
        content.Headers.Add("Idempotency-Key", idempotencyKey);

        return content;
    }
}
