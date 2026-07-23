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
public class GetResults(ISender sender) : ApiControllerBase
{
    [HttpGet("results")]
    public async Task<IActionResult> GetResult(
        [FromQuery] string? fileName,
        [FromQuery] DateTimeOffset? firstOperationDateFrom,
        [FromQuery] DateTimeOffset? firstOperationDateTo,
        [FromQuery] double? averageValueFrom,
        [FromQuery] double? averageValueTo,
        [FromQuery] double? averageExecutionTimeFrom,
        [FromQuery] double? averageExecutionTimeTo,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 50)
    {
        if ((averageValueFrom is not null && averageValueTo is not null) 
            && (averageValueFrom > averageValueTo))
        {
            return BadRequest("Неверный диапазон значений.");
        }
        
        if ((averageExecutionTimeFrom is not null && averageExecutionTimeTo is not null) 
            && (averageExecutionTimeFrom > averageExecutionTimeTo))
        {
            return BadRequest("Неверный диапазон значений.");
        }
        
        if ((firstOperationDateFrom is not null && firstOperationDateTo is not null) 
            && (firstOperationDateFrom > firstOperationDateTo))
        {
            return BadRequest("Неверный диапазон дат.");
        }

        var result = await sender.Send(new GetResultsCommand(fileName, 
            firstOperationDateFrom, 
            firstOperationDateTo,
            averageValueFrom,
            averageValueTo, 
            averageExecutionTimeFrom, 
            averageExecutionTimeTo, 
            offset, 
            limit));
        
        return Ok(result);
    }
}

public sealed record GetResultsCommand(
    string? FileName,
    DateTimeOffset? FirstOperationDateFrom,
    DateTimeOffset? FirstOperationDateTo,
    double? AverageValueFrom,
    double? AverageValueTo,
    double? AverageExecutionTimeFrom,
    double? AverageExecutionTimeTo,
    int Offset,
    int Limit) : IRequest<PagedResponse>;

public sealed class GetResultsCommandHandler(
    TimeSeriesDbContext dbContext,
    IDistributedCache cache) 
    : IRequestHandler<GetResultsCommand, PagedResponse>
{
    public async Task<PagedResponse> Handle(GetResultsCommand request, CancellationToken cancellationToken)
    {
        var cacheKey = $"results:{JsonSerializer.Serialize(request)}";

        var cachedResult = await cache.GetStringAsync(
            cacheKey,
            cancellationToken);

        if (cachedResult is not null)
        {
            return JsonSerializer.Deserialize<PagedResponse>(cachedResult)!;
        }
        
        var query = dbContext.ProcessingResults
            .AsNoTracking()
            .Select(x => new ResultDto(
                FileName: x.UploadedFile.FileName,
                DateDeltaSeconds: x.DateDeltaSeconds,
                FirstOperationDate: x.FirstOperationDate,
                AverageExecutionTime: x.AverageExecutionTime,
                AverageValue: x.AverageValue,
                MedianValue: x.MedianValue,
                MaxValue: x.MaxValue,
                MinValue: x.MinValue
                ));
        
        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            query = query.Where(x =>
                EF.Functions.ILike(
                    x.FileName,
                    $"%{request.FileName.Trim()}%"));
        }
        
        if (request.FirstOperationDateFrom is not null)
        {
            query = query.Where(x =>
                x.FirstOperationDate >=
                request.FirstOperationDateFrom.Value);
        }

        if (request.FirstOperationDateTo is not null)
        {
            query = query.Where(x =>
                x.FirstOperationDate <=
                request.FirstOperationDateTo.Value);
        }

        if (request.AverageValueFrom is not null)
        {
            query = query.Where(x =>
                x.AverageValue >=
                request.AverageValueFrom.Value);
        }

        if (request.AverageValueTo is not null)
        {
            query = query.Where(x =>
                x.AverageValue <=
                request.AverageValueTo.Value);
        }

        if (request.AverageExecutionTimeFrom is not null)
        {
            query = query.Where(x =>
                x.AverageExecutionTime >=
                request.AverageExecutionTimeFrom.Value);
        }

        if (request.AverageExecutionTimeTo is not null)
        {
            query = query.Where(x =>
                x.AverageExecutionTime <=
                request.AverageExecutionTimeTo.Value);
        }
        
        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderBy(x => x.FirstOperationDate)
            .Skip(request.Offset)
            .Take(request.Limit)
            .ToListAsync(cancellationToken);

        var response = new PagedResponse(
            Items: items,
            Offset: request.Offset,
            Limit: request.Limit,
            TotalCount: totalCount);
        
        await cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(response),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromSeconds(30)
            },
            cancellationToken);

        return response;
    }
}

public sealed record PagedResponse(
    List<ResultDto> Items,
    int Offset,
    int Limit,
    int TotalCount);

public sealed record ResultDto(
    string FileName,
    double DateDeltaSeconds,
    DateTimeOffset FirstOperationDate,
    double AverageExecutionTime,
    double AverageValue,
    double MedianValue,
    double MaxValue,
    double MinValue);