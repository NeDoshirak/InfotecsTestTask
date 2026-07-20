using Microsoft.AspNetCore.Mvc;
using TimeSeries.Application;

namespace TimeSeries.Features.Files;

[ApiController]
[Route("files")]
public class GetLatestValues : ApiControllerBase
{
    [HttpGet("/latest")]
    public IActionResult GetLatest()
    {
        return Ok();
    }
}