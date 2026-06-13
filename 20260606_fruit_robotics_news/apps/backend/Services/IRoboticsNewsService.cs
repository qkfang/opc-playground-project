using backend.Models;

namespace backend.Services;

public interface IRoboticsNewsService
{
    Task<IReadOnlyList<RoboticsNewsItem>> GetNewsAsync(int count, CancellationToken cancellationToken);
}
