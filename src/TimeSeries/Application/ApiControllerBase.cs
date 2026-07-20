using Microsoft.AspNetCore.Mvc;

namespace TimeSeries.Application;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase;