using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/news")]
public sealed class NewsController(IRoboticsNewsService roboticsNewsService) : ControllerBase
{
    [HttpGet("robotics")]
    public async Task<ActionResult<IReadOnlyList<RoboticsNewsItem>>> GetRoboticsNews(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedCount = Math.Clamp(count, 1, 50);
        var news = await roboticsNewsService.GetNewsAsync(normalizedCount, cancellationToken);
        return Ok(news);
    }
}
