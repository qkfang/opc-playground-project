using RoboticsNews.Api.Models;

namespace RoboticsNews.Api.Services;

public interface INewsService
{
    Task<IReadOnlyList<NewsItemDto>> GetLatestAsync(CancellationToken cancellationToken);
}
