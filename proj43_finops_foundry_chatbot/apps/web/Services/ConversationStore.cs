using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// In-memory, per-conversation history with a sliding window. Zero-dependency POC multi-turn memory;
/// production-upgradeable to Redis/Cosmos. Also (optionally) persists transcripts to the local data
/// folder for audit/demo. Keyed by an opaque conversation id minted on first message.
/// </summary>
public sealed class ConversationStore
{
    private readonly ChatOptions _chat;
    private readonly StorageOptions _storage;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ConversationStore> _logger;
    private readonly ConcurrentDictionary<string, List<ChatTurn>> _conversations = new();

    public ConversationStore(IOptions<ChatOptions> chat, IOptions<StorageOptions> storage,
        IWebHostEnvironment env, ILogger<ConversationStore> logger)
    {
        _chat = chat.Value;
        _storage = storage.Value;
        _env = env;
        _logger = logger;
    }

    public string EnsureConversation(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id) && _conversations.ContainsKey(id)) return id!;
        var newId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("n") : id!;
        _conversations.TryAdd(newId, new List<ChatTurn>());
        return newId;
    }

    public IReadOnlyList<ChatTurn> History(string id) =>
        _conversations.TryGetValue(id, out var list) ? list : Array.Empty<ChatTurn>();

    /// <summary>Most recent N turns for prompt context (oldest→newest).</summary>
    public IReadOnlyList<ChatTurn> RecentContext(string id)
    {
        var all = History(id);
        int take = Math.Max(2, _chat.MaxHistoryTurns * 2);
        return all.Count <= take ? all : all.Skip(all.Count - take).ToList();
    }

    public void Add(string id, string role, string content)
    {
        var list = _conversations.GetOrAdd(id, _ => new List<ChatTurn>());
        lock (list)
        {
            list.Add(new ChatTurn { Role = role, Content = content });
        }
        if (_chat.PersistTranscripts) TryPersist(id);
    }

    private void TryPersist(string id)
    {
        try
        {
            var dir = Path.IsPathRooted(_storage.LocalDataFolder)
                ? _storage.LocalDataFolder
                : Path.Combine(_env.ContentRootPath, _storage.LocalDataFolder);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"conversation-{id}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(History(id),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Transcript persistence skipped for {Id}", id);
        }
    }
}
