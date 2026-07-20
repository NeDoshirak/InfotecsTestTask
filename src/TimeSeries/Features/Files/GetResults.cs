using Microsoft.AspNetCore.Mvc;
using TimeSeries.Application;

namespace TimeSeries.Features.Files;

[ApiController]
[Route("files")]
public class GetResults : ApiControllerBase
{
    [HttpGet("results")]
    public IActionResult GetResult()
    {
        return Ok();
    }
}