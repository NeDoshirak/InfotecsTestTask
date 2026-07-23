using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TimeSeries.Application;
using TimeSeries.Application.Exceptions;
using TimeSeries.Domain;
using TimeSeries.Infrastructure;

namespace TimeSeries.Features.Files;

[ApiController]
[Route("files")]
public class UploadFile(ISender sender, CancellationToken cancellationToken)  : ApiControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (file is null || file.Length == 0) 
        {
            return BadRequest("Файл не передан или пуст.");
        }
        
        if (Path.GetExtension(file.FileName) != ".csv")
        {
            return BadRequest("Поддерживаются только файлы формата CSV.");
        }

        if (!Guid.TryParse(idempotencyKey, out var idempotencyKeyGuid))
        {
            return BadRequest("Заголовок Idempotency-Key должен содержать корректный Guid.");
        }
        
        var result = await sender.Send(new UploadFileCommand(file, idempotencyKeyGuid), cancellationToken);
        
        return result.AlreadyProcessed 
            ? Ok(result)
            : StatusCode(StatusCodes.Status201Created, result);
    }
}

public sealed record UploadFileCommand(IFormFile File, Guid IdempotencyKeyGuid) : IRequest<UploadFileResponse>;

public sealed class UploadFileCommandHandler(ICsvFileProcessor processor, TimeSeriesDbContext dbContext)
    : IRequestHandler<UploadFileCommand, UploadFileResponse>
{
    public async Task<UploadFileResponse> Handle(UploadFileCommand request, CancellationToken cancellationToken)
    {
        var idempotencyKeyGuid = request.IdempotencyKeyGuid; 
        var contentHash = await CalculateHashAsync(request.File, cancellationToken);
        
        var isExist = await dbContext.UploadedFiles
            .AnyAsync(
                x => x.IdempotencyKey == idempotencyKeyGuid,
                cancellationToken);

        if (isExist)
        {
            var existFileData = await dbContext.UploadedFiles
                .Where(x => x.IdempotencyKey == idempotencyKeyGuid)
                .Select(x => new
                {
                    x.Id,
                    x.ContentHash
                })
                .FirstAsync(cancellationToken);

            if (contentHash != existFileData.ContentHash)
            {
                throw new ConflictException("Файл с таким же ключём уже был обработан ранее.");
            }
            
            return new UploadFileResponse(true, existFileData.Id);
        }
        
        var file = request.File;
        var processedCsvFile = await processor.ProcessAsync(file, cancellationToken);
        var normalizedFileName = NormalizeFileName(file.FileName);

        
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            if (await dbContext.UploadedFiles
                    .AnyAsync(
                        x => x.NormalizedFileName == normalizedFileName,
                        cancellationToken))
            { 
                dbContext.UploadedFiles.Remove(await dbContext.UploadedFiles
                    .FirstAsync(
                        x => x.NormalizedFileName == normalizedFileName,
                        cancellationToken));
            }

            await dbContext.UploadedFiles.AddAsync(new UploadedFile
            {
                FileName = file.FileName,
                NormalizedFileName = normalizedFileName,
                IdempotencyKey = idempotencyKeyGuid,
                ContentHash = contentHash,
                RowsCount = processedCsvFile.Values.Count,
                CreatedAt = DateTimeOffset.UtcNow,
                
                Values = processedCsvFile.Values
                    .Select(x => new Value
                    {
                        Date = x.Date,
                        ExecutionTime = x.ExecutionTime,
                        IndicatorValue = x.IndicatorValue
                    })
                    .ToList(),
                
                ProcessingResult = new ProcessingResult
                {
                    DateDeltaSeconds = processedCsvFile.Statistics.DateDeltaSeconds,
                    AverageExecutionTime = processedCsvFile.Statistics.AverageExecutionTime,
                    AverageValue =  processedCsvFile.Statistics.AverageValue,
                    FirstOperationDate = processedCsvFile.Statistics.FirstOperationDate,
                    MedianValue = processedCsvFile.Statistics.MedianValue,
                    MaxValue = processedCsvFile.Statistics.MaxValue,
                    MinValue = processedCsvFile.Statistics.MinValue
                }
            }, cancellationToken);
            
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException postgresException &&
                  postgresException.SqlState == PostgresErrorCodes.UniqueViolation &&
                  postgresException.ConstraintName == "IX_UploadedFiles_IdempotencyKey")
        {
            await transaction.RollbackAsync(cancellationToken);
            
            var id = await dbContext.UploadedFiles
                .Where(x => x.NormalizedFileName == normalizedFileName)
                .Select(x => x.Id)
                .FirstAsync(cancellationToken);
            
            return new UploadFileResponse(true, id);
        }
        
        var guid = await dbContext.UploadedFiles
            .Where(x => x.NormalizedFileName == normalizedFileName)
            .Select(x => x.Id)
            .FirstAsync(cancellationToken);
        
        return new UploadFileResponse(false, guid);
    }
    
    private static async Task<string> CalculateHashAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();

        var hash = await SHA256.HashDataAsync(
            stream,
            cancellationToken);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
    
    private static string NormalizeFileName(string fileName)
    {
        var safeName = Path.GetFileName(fileName);

        return safeName
            .Trim()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }
}

public sealed record UploadFileResponse(
    bool AlreadyProcessed,
    Guid FileId);
