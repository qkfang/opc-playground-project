using Microsoft.AspNetCore.Mvc;
using RoboticsNews.Api.Models;
using RoboticsNews.Api.Services;

namespace RoboticsNews.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class NewsController(INewsService newsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NewsItemDto>>> Get(CancellationToken cancellationToken)
    {
        var items = await newsService.GetLatestAsync(cancellationToken);
        return Ok(items);
    }
}
