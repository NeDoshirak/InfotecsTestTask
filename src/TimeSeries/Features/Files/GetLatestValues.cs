using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using TimeSeries.Application;
using TimeSeries.Infrastructure;

namespace TimeSeries.Features.Files;

[ApiController]
[Route("files")]
public class GetLatestValues(ISender sender) : ApiControllerBase
{
    [HttpGet("latest/{fileName}")]
    public async Task<IActionResult> GetLatest(
        [FromRoute] string fileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return BadRequest("Имя файла не может быть пустым.");
        }
        
        var response = await sender.Send(
            new GetLatestCommand(fileName),
            cancellationToken);

        return Ok(response);
    }
}

public sealed record GetLatestCommand(string FileName) : IRequest<Response>;

public sealed class GetLatestCommandHandler(
    TimeSeriesDbContext dbContext,
    IDistributedCache cache) : IRequestHandler<GetLatestCommand, Response>
{
    public async Task<Response> Handle(GetLatestCommand request, CancellationToken cancellationToken)
    {
        var cacheKey = $"latest:{request.FileName}";

        var cachedResponse = await cache.GetStringAsync(
            cacheKey,
            cancellationToken);

        if (cachedResponse is not null)
        {
            return JsonSerializer.Deserialize<Response>(cachedResponse)!;
        }

        var items = await dbContext.Values
            .AsNoTracking()
            .Where(x => x.UploadedFile.FileName == request.FileName)
            .OrderByDescending(x => x.Date)
            .Take(10)
            .Select(x => new LatestValueDto(
                x.Date,
                x.ExecutionTime,
                x.IndicatorValue))
            .ToListAsync(cancellationToken);

        var response = new Response(items);
        
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            },
            cancellationToken);

        return response;
    }
}

public sealed record Response(List<LatestValueDto> Items);

public sealed record LatestValueDto(
    DateTimeOffset Date,
    double ExecutionTime,
    double Value);
