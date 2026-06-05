using Microsoft.AspNetCore.Mvc;
using RoboticsNews.Api.Models;
using RoboticsNews.Api.Services;

namespace RoboticsNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 50;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NewsItemDto>>> Get([FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var requestedLimit = limit.GetValueOrDefault(DefaultLimit);
        var normalizedLimit = requestedLimit <= 0 ? DefaultLimit : Math.Min(requestedLimit, MaxLimit);

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
