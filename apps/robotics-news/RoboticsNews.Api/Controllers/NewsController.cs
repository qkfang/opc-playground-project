using Microsoft.AspNetCore.Mvc;
using RoboticsNews.Api.Models;
using RoboticsNews.Api.Services;

namespace RoboticsNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NewsItemDto>>> Get([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var requestedLimit = limit.GetValueOrDefault(20);
        var normalizedLimit = requestedLimit <= 0 ? 20 : Math.Min(requestedLimit, 50);

        try
        {
            var items = await newsService.GetLatestAsync(normalizedLimit, cancellationToken);
            return Ok(items);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(title: "Unable to load robotics news.", detail: ex.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
