using Microsoft.AspNetCore.Mvc;
using TimeSeries.Application;

namespace TimeSeries.Features.Files;

[ApiController]
[Route("files")]
public class UploadFile : ApiControllerBase
{
    [HttpPost("upload")]
    public IActionResult Upload(IFormFile file)
    {
        return Ok();
    }
}
