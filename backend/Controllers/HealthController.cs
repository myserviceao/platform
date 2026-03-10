using Microsoft.AspNetCore.Mvc;

namespace MyServiceAO.Controllers;

[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "ok",
            app = "MyServiceAO",
            version = "1.0.0",
            timestamp = DateTime.UtcNow
        });
    }
}
