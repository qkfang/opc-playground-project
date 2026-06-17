namespace backend.Models;

public sealed record NewsItem(
    string Title,
    string Url,
    DateTimeOffset? Published,
    string? Summary);
